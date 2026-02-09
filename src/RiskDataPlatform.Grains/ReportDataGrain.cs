using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Orleans;
using RiskDataPlatform.Arrow;
using RiskDataPlatform.Core.Constants;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Interfaces;
using RiskDataPlatform.Core.Models;
using RiskDataPlatform.Grains.Interfaces;
using RiskDataPlatform.Query;

namespace RiskDataPlatform.Grains;

public sealed class ReportDataGrain : Grain, IReportDataGrain
{
    private readonly ILogger<ReportDataGrain> _logger;
    private readonly S3ArrowStore _arrowStore;
    private readonly DuckDbQueryEngine _queryEngine;
    private readonly ArrowWriter _arrowWriter;
    private string _tenantId = string.Empty;
    private Guid _executionId;
    private string _deskId = string.Empty;
    private string _reportType = string.Empty;
    private bool _dataLoaded;

    public ReportDataGrain(
        ILogger<ReportDataGrain> logger,
        S3ArrowStore arrowStore,
        DuckDbQueryEngine queryEngine,
        ArrowWriter arrowWriter)
    {
        _logger = logger;
        _arrowStore = arrowStore;
        _queryEngine = queryEngine;
        _arrowWriter = arrowWriter;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var grainKey = this.GetPrimaryKeyString();
        var parts = grainKey.Split('/');
        
        if (parts.Length != 4)
        {
            throw new ArgumentException($"Invalid grain key format: {grainKey}. Expected: tenantId/executionId/deskId/reportType");
        }

        _tenantId = parts[0];
        _executionId = Guid.Parse(parts[1]);
        _deskId = parts[2];
        _reportType = parts[3];

        using var activity = Telemetry.Grains.StartActivity("ReportDataGrain.Activate");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("executionId", _executionId);
        activity?.SetTag("deskId", _deskId);
        activity?.SetTag("reportType", _reportType);

        _logger.LogInformation("ReportDataGrain activated for tenant {TenantId}, execution {ExecutionId}",
            _tenantId, _executionId);

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<byte[]> QueryAsync(ReportQueryRequest request, ResponseFormat format = ResponseFormat.ArrowIpc)
    {
        using var activity = Telemetry.Grains.StartActivity("ReportDataGrain.Query");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("executionId", _executionId);

        try
        {
            await EnsureDataLoadedAsync();

            var result = await _queryEngine.QueryAsync(
                _tenantId,
                _executionId,
                _deskId,
                _reportType,
                request,
                format);

            _logger.LogInformation("Query executed successfully for execution {ExecutionId}", _executionId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query for execution {ExecutionId}", _executionId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<SsrmResponse> QuerySsrmAsync(SsrmRequest request)
    {
        using var activity = Telemetry.Grains.StartActivity("ReportDataGrain.QuerySsrm");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("executionId", _executionId);

        try
        {
            await EnsureDataLoadedAsync();

            var result = await _queryEngine.QuerySsrmAsync(
                _tenantId,
                _executionId,
                _deskId,
                _reportType,
                request);

            _logger.LogInformation("SSRM query executed successfully for execution {ExecutionId}", _executionId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute SSRM query for execution {ExecutionId}", _executionId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<string> GetSchemaAsync()
    {
        using var activity = Telemetry.Grains.StartActivity("ReportDataGrain.GetSchema");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("executionId", _executionId);

        try
        {
            var bucket = $"risk-data-{_tenantId}";
            var key = $"{_reportType}/{_deskId}/{_executionId:N}/manifest.arrow";

            var schema = await _arrowStore.GetSchemaAsync(bucket, key);

            if (schema == null)
            {
                _logger.LogWarning("Schema not found for execution {ExecutionId}", _executionId);
                return string.Empty;
            }

            _logger.LogInformation("Retrieved schema for execution {ExecutionId}", _executionId);

            return schema.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schema for execution {ExecutionId}", _executionId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public Task InvalidateCacheAsync()
    {
        using var activity = Telemetry.Grains.StartActivity("ReportDataGrain.InvalidateCache");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("executionId", _executionId);

        _dataLoaded = false;

        _logger.LogInformation("Cache invalidated for execution {ExecutionId}", _executionId);

        return Task.CompletedTask;
    }

    private async Task EnsureDataLoadedAsync()
    {
        if (_dataLoaded)
        {
            return;
        }

        using var activity = Telemetry.Grains.StartActivity("ReportDataGrain.LoadData");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("executionId", _executionId);

        try
        {
            var bucket = $"risk-data-{_tenantId}";
            var key = $"{_reportType}/{_deskId}/{_executionId:N}/manifest.arrow";

            var data = await _arrowStore.GetRecordBatchesWithSchemaAsync(bucket, key);

            if (data == null)
            {
                throw new InvalidOperationException($"Data not found for execution {_executionId}");
            }

            var arrowIpcData = await _arrowWriter.WriteToByteArrayAsync(data.Value.schema, data.Value.batches);

            await _queryEngine.LoadDataAsync(
                _tenantId,
                _executionId,
                _deskId,
                _reportType,
                arrowIpcData);

            _dataLoaded = true;

            _logger.LogInformation("Data loaded for execution {ExecutionId}", _executionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load data for execution {ExecutionId}", _executionId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
