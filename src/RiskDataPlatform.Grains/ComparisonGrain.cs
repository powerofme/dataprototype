using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using RiskDataPlatform.Arrow;
using RiskDataPlatform.Core.Constants;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Interfaces;
using RiskDataPlatform.Core.Models;
using RiskDataPlatform.Grains.Interfaces;
using RiskDataPlatform.Query;

namespace RiskDataPlatform.Grains;

public sealed class ComparisonGrain : Grain, IComparisonGrain
{
    private readonly ILogger<ComparisonGrain> _logger;
    private readonly IPersistentState<ComparisonState> _state;
    private readonly S3ArrowStore _arrowStore;
    private readonly DuckDbQueryEngine _queryEngine;
    private readonly ArrowWriter _arrowWriter;
    private string _tenantId = string.Empty;
    private Guid _comparisonId;
    private bool _dataLoaded;

    public ComparisonGrain(
        ILogger<ComparisonGrain> logger,
        [PersistentState("comparison", "ComparisonStore")] IPersistentState<ComparisonState> state,
        S3ArrowStore arrowStore,
        DuckDbQueryEngine queryEngine,
        ArrowWriter arrowWriter)
    {
        _logger = logger;
        _state = state;
        _arrowStore = arrowStore;
        _queryEngine = queryEngine;
        _arrowWriter = arrowWriter;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var grainKey = this.GetPrimaryKeyString();
        var (tenantId, logicalKey) = GrainKeys.ParseGrainKey(grainKey);
        _tenantId = tenantId;
        _comparisonId = Guid.Parse(logicalKey);

        using var activity = Telemetry.Grains.StartActivity("ComparisonGrain.Activate");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("comparisonId", _comparisonId);

        _logger.LogInformation("ComparisonGrain activated for tenant {TenantId}, comparison {ComparisonId}",
            _tenantId, _comparisonId);

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<ComparisonSummary> CreateComparisonAsync(ComparisonRequest request)
    {
        using var activity = Telemetry.Grains.StartActivity("ComparisonGrain.CreateComparison");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("comparisonId", _comparisonId);
        activity?.SetTag("baseExecutionId", request.BaseExecutionId);
        activity?.SetTag("compareExecutionId", request.CompareExecutionId);

        try
        {
            if (_state.State.Summary != null)
            {
                _logger.LogWarning("Comparison {ComparisonId} already exists", _comparisonId);
                return _state.State.Summary;
            }

            _state.State.Request = request;
            _state.State.Summary = new ComparisonSummary
            {
                ComparisonId = _comparisonId,
                BaseExecutionId = request.BaseExecutionId,
                CompareExecutionId = request.CompareExecutionId,
                DeskId = request.DeskId,
                ReportType = request.ReportType,
                CreatedAt = DateTime.UtcNow,
                Status = ComparisonStatusEnum.Running
            };

            await _state.WriteStateAsync();

            await LoadComparisonDataAsync(request);

            var summary = await _queryEngine.CompareExecutionsAsync(_tenantId, request);

            _state.State.Summary = summary;
            _state.State.Summary.ComparisonId = _comparisonId;
            _state.State.Summary.Status = ComparisonStatusEnum.Completed;

            await _state.WriteStateAsync();

            _logger.LogInformation("Created comparison {ComparisonId} between {BaseExecutionId} and {CompareExecutionId}",
                _comparisonId, request.BaseExecutionId, request.CompareExecutionId);

            return _state.State.Summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create comparison {ComparisonId}", _comparisonId);
            
            if (_state.State.Summary != null)
            {
                _state.State.Summary.Status = ComparisonStatusEnum.Failed;
                await _state.WriteStateAsync();
            }

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<byte[]> QueryAsync(ReportQueryRequest request, ResponseFormat format = ResponseFormat.ArrowIpc)
    {
        using var activity = Telemetry.Grains.StartActivity("ComparisonGrain.Query");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("comparisonId", _comparisonId);

        try
        {
            if (_state.State.Request == null)
            {
                throw new InvalidOperationException("Comparison not initialized");
            }

            await EnsureDataLoadedAsync();

            var result = await _queryEngine.QueryAsync(
                _tenantId,
                _comparisonId,
                _state.State.Request.DeskId,
                _state.State.Request.ReportType,
                request,
                format);

            _logger.LogInformation("Query executed successfully for comparison {ComparisonId}", _comparisonId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query for comparison {ComparisonId}", _comparisonId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public Task<ComparisonSummary> GetSummaryAsync()
    {
        using var activity = Telemetry.Grains.StartActivity("ComparisonGrain.GetSummary");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("comparisonId", _comparisonId);

        if (_state.State.Summary == null)
        {
            throw new InvalidOperationException("Comparison not initialized");
        }

        return Task.FromResult(_state.State.Summary);
    }

    public Task<Dictionary<string, int>> GetMarketDataDiffAsync()
    {
        using var activity = Telemetry.Grains.StartActivity("ComparisonGrain.GetMarketDataDiff");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("comparisonId", _comparisonId);

        if (_state.State.Summary == null)
        {
            throw new InvalidOperationException("Comparison not initialized");
        }

        return Task.FromResult(_state.State.Summary.DifferencesByBook);
    }

    private async Task EnsureDataLoadedAsync()
    {
        if (_dataLoaded)
        {
            return;
        }

        if (_state.State.Request == null)
        {
            throw new InvalidOperationException("Comparison not initialized");
        }

        await LoadComparisonDataAsync(_state.State.Request);
        _dataLoaded = true;
    }

    private async Task LoadComparisonDataAsync(ComparisonRequest request)
    {
        using var activity = Telemetry.Grains.StartActivity("ComparisonGrain.LoadData");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("comparisonId", _comparisonId);

        try
        {
            var bucket = $"risk-data-{_tenantId}";
            
            var baseKey = $"{request.ReportType}/{request.DeskId}/{request.BaseExecutionId:N}/manifest.arrow";
            var baseData = await _arrowStore.GetRecordBatchesWithSchemaAsync(bucket, baseKey);

            if (baseData == null)
            {
                throw new InvalidOperationException($"Base execution data not found: {request.BaseExecutionId}");
            }

            var compareKey = $"{request.ReportType}/{request.DeskId}/{request.CompareExecutionId:N}/manifest.arrow";
            var compareData = await _arrowStore.GetRecordBatchesWithSchemaAsync(bucket, compareKey);

            if (compareData == null)
            {
                throw new InvalidOperationException($"Compare execution data not found: {request.CompareExecutionId}");
            }

            var baseArrowIpc = await _arrowWriter.WriteToByteArrayAsync(baseData.Value.schema, baseData.Value.batches);
            await _queryEngine.LoadDataAsync(
                _tenantId,
                request.BaseExecutionId,
                request.DeskId,
                request.ReportType,
                baseArrowIpc);

            var compareArrowIpc = await _arrowWriter.WriteToByteArrayAsync(compareData.Value.schema, compareData.Value.batches);
            await _queryEngine.LoadDataAsync(
                _tenantId,
                request.CompareExecutionId,
                request.DeskId,
                request.ReportType,
                compareArrowIpc);

            _logger.LogInformation("Loaded comparison data for {ComparisonId}", _comparisonId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load comparison data for {ComparisonId}", _comparisonId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

[GenerateSerializer]
public sealed class ComparisonState
{
    [Id(0)]
    public ComparisonRequest? Request { get; set; }

    [Id(1)]
    public ComparisonSummary? Summary { get; set; }
}
