using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using RiskDataPlatform.Core.Constants;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Models;
using RiskDataPlatform.Grains.Interfaces;

namespace RiskDataPlatform.Grains;

public sealed class DeskGrain : Grain, IDeskGrain
{
    private readonly ILogger<DeskGrain> _logger;
    private readonly IPersistentState<DeskState> _state;
    private string _tenantId = string.Empty;
    private string _deskId = string.Empty;

    public DeskGrain(
        ILogger<DeskGrain> logger,
        [PersistentState("desk", "DeskStore")] IPersistentState<DeskState> state)
    {
        _logger = logger;
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var grainKey = this.GetPrimaryKeyString();
        var (tenantId, logicalKey) = GrainKeys.ParseGrainKey(grainKey);
        _tenantId = tenantId;
        _deskId = logicalKey;

        using var activity = Telemetry.Grains.StartActivity("DeskGrain.Activate");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("deskId", _deskId);

        _logger.LogInformation("DeskGrain activated for tenant {TenantId}, desk {DeskId}", _tenantId, _deskId);

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<List<Execution>> GetExecutionsAsync(int limit = 100)
    {
        using var activity = Telemetry.Grains.StartActivity("DeskGrain.GetExecutions");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("deskId", _deskId);
        activity?.SetTag("limit", limit);

        try
        {
            var executions = _state.State.Executions
                .OrderByDescending(e => e.CreatedAt)
                .Take(limit)
                .ToList();

            _logger.LogInformation("Retrieved {Count} executions for desk {DeskId}", executions.Count, _deskId);

            return executions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get executions for desk {DeskId}", _deskId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<Dictionary<string, Execution>> GetLatestExecutionsAsync()
    {
        using var activity = Telemetry.Grains.StartActivity("DeskGrain.GetLatestExecutions");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("deskId", _deskId);

        try
        {
            var latestExecutions = _state.State.Executions
                .GroupBy(e => e.ReportType)
                .Select(g => g.OrderByDescending(e => e.CreatedAt).First())
                .ToDictionary(e => e.ReportType, e => e);

            _logger.LogInformation("Retrieved {Count} latest executions for desk {DeskId}",
                latestExecutions.Count, _deskId);

            return latestExecutions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest executions for desk {DeskId}", _deskId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task StartIncrementalAsync(string reportType)
    {
        using var activity = Telemetry.Grains.StartActivity("DeskGrain.StartIncremental");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("deskId", _deskId);
        activity?.SetTag("reportType", reportType);

        try
        {
            _state.State.IncrementalStatus[reportType] = true;
            await _state.WriteStateAsync();

            _logger.LogInformation("Started incremental processing for desk {DeskId}, report type {ReportType}",
                _deskId, reportType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start incremental for desk {DeskId}, report type {ReportType}",
                _deskId, reportType);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task StopIncrementalAsync(string reportType)
    {
        using var activity = Telemetry.Grains.StartActivity("DeskGrain.StopIncremental");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("deskId", _deskId);
        activity?.SetTag("reportType", reportType);

        try
        {
            _state.State.IncrementalStatus[reportType] = false;
            await _state.WriteStateAsync();

            _logger.LogInformation("Stopped incremental processing for desk {DeskId}, report type {ReportType}",
                _deskId, reportType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop incremental for desk {DeskId}, report type {ReportType}",
                _deskId, reportType);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public Task<Dictionary<string, bool>> GetIncrementalStatusAsync()
    {
        using var activity = Telemetry.Grains.StartActivity("DeskGrain.GetIncrementalStatus");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("deskId", _deskId);

        return Task.FromResult(new Dictionary<string, bool>(_state.State.IncrementalStatus));
    }

    internal async Task AddExecutionAsync(Execution execution)
    {
        using var activity = Telemetry.Grains.StartActivity("DeskGrain.AddExecution");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("deskId", _deskId);
        activity?.SetTag("executionId", execution.ExecutionId);

        try
        {
            _state.State.Executions.Add(execution);

            if (_state.State.Executions.Count > 1000)
            {
                _state.State.Executions = _state.State.Executions
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(1000)
                    .ToList();
            }

            await _state.WriteStateAsync();

            _logger.LogInformation("Added execution {ExecutionId} to desk {DeskId}",
                execution.ExecutionId, _deskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add execution {ExecutionId} to desk {DeskId}",
                execution.ExecutionId, _deskId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

[GenerateSerializer]
public sealed class DeskState
{
    [Id(0)]
    public List<Execution> Executions { get; set; } = new();

    [Id(1)]
    public Dictionary<string, bool> IncrementalStatus { get; set; } = new();
}
