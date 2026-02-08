using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using RiskDataPlatform.Core.Constants;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Interfaces;
using RiskDataPlatform.Core.Models;
using RiskDataPlatform.Grains.Interfaces;

namespace RiskDataPlatform.Grains;

public sealed class ExecutionGrain : Grain, IExecutionGrain
{
    private readonly ILogger<ExecutionGrain> _logger;
    private readonly IPersistentState<ExecutionState> _state;
    private string _tenantId = string.Empty;

    public ExecutionGrain(
        ILogger<ExecutionGrain> logger,
        [PersistentState("execution", "ExecutionStore")] IPersistentState<ExecutionState> state)
    {
        _logger = logger;
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var grainKey = this.GetPrimaryKeyString();
        var (tenantId, _) = GrainKeys.ParseGrainKey(grainKey);
        _tenantId = tenantId;

        using var activity = Telemetry.Grains.StartActivity("ExecutionGrain.Activate");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("grainKey", grainKey);

        _logger.LogInformation("ExecutionGrain activated for tenant {TenantId}", _tenantId);

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<Execution> CreateAsync(Execution execution)
    {
        using var activity = Telemetry.Grains.StartActivity("ExecutionGrain.Create");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("executionId", execution.ExecutionId);
        activity?.SetTag("deskId", execution.DeskId);

        try
        {
            if (_state.State.Execution != null)
            {
                _logger.LogWarning("Execution {ExecutionId} already exists", execution.ExecutionId);
                return _state.State.Execution;
            }

            execution.Status = ExecutionStatusEnum.Pending;
            execution.CreatedAt = DateTime.UtcNow;

            _state.State.Execution = execution;
            _state.State.Status = new ExecutionStatus
            {
                OverallStatus = ExecutionStatusEnum.Pending,
                BookStatuses = execution.Books.Select(b => new BookStatus
                {
                    BookName = b,
                    Status = BookStatusEnum.Pending
                }).ToList()
            };

            await _state.WriteStateAsync();

            _logger.LogInformation("Created execution {ExecutionId} for desk {DeskId}, reportType {ReportType}",
                execution.ExecutionId, execution.DeskId, execution.ReportType);

            return execution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create execution {ExecutionId}", execution.ExecutionId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public Task<ExecutionStatus> GetStatusAsync()
    {
        using var activity = Telemetry.Grains.StartActivity("ExecutionGrain.GetStatus");
        activity?.SetTag("tenantId", _tenantId);

        if (_state.State.Status == null)
        {
            return Task.FromResult(new ExecutionStatus
            {
                OverallStatus = ExecutionStatusEnum.Pending,
                BookStatuses = new List<BookStatus>()
            });
        }

        return Task.FromResult(_state.State.Status);
    }

    public async Task CancelAsync()
    {
        using var activity = Telemetry.Grains.StartActivity("ExecutionGrain.Cancel");
        activity?.SetTag("tenantId", _tenantId);

        try
        {
            if (_state.State.Execution == null)
            {
                _logger.LogWarning("Cannot cancel - execution does not exist");
                return;
            }

            _state.State.Execution.Status = ExecutionStatusEnum.Cancelled;
            _state.State.Status.OverallStatus = ExecutionStatusEnum.Cancelled;
            _state.State.Execution.CompletedAt = DateTime.UtcNow;

            await _state.WriteStateAsync();

            _logger.LogInformation("Cancelled execution {ExecutionId}", _state.State.Execution.ExecutionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel execution");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task CompleteAsync()
    {
        using var activity = Telemetry.Grains.StartActivity("ExecutionGrain.Complete");
        activity?.SetTag("tenantId", _tenantId);

        try
        {
            if (_state.State.Execution == null)
            {
                _logger.LogWarning("Cannot complete - execution does not exist");
                return;
            }

            _state.State.Execution.Status = ExecutionStatusEnum.Completed;
            _state.State.Status.OverallStatus = ExecutionStatusEnum.Completed;
            _state.State.Execution.CompletedAt = DateTime.UtcNow;

            await _state.WriteStateAsync();

            _logger.LogInformation("Completed execution {ExecutionId}", _state.State.Execution.ExecutionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete execution");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task UpdateBookStatusAsync(string bookName, BookStatus bookStatus)
    {
        using var activity = Telemetry.Grains.StartActivity("ExecutionGrain.UpdateBookStatus");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("bookName", bookName);
        activity?.SetTag("status", bookStatus.Status);

        try
        {
            if (_state.State.Status == null)
            {
                _logger.LogWarning("Cannot update book status - execution state not initialized");
                return;
            }

            var existingBookStatus = _state.State.Status.BookStatuses.FirstOrDefault(b => b.BookName == bookName);
            if (existingBookStatus != null)
            {
                _state.State.Status.BookStatuses.Remove(existingBookStatus);
            }

            _state.State.Status.BookStatuses.Add(bookStatus);

            var allCompleted = _state.State.Status.BookStatuses.All(b => b.Status == BookStatusEnum.Completed);
            var anyFailed = _state.State.Status.BookStatuses.Any(b => b.Status == BookStatusEnum.Failed);

            if (anyFailed)
            {
                _state.State.Status.OverallStatus = ExecutionStatusEnum.Failed;
                if (_state.State.Execution != null)
                {
                    _state.State.Execution.Status = ExecutionStatusEnum.Failed;
                }
            }
            else if (allCompleted)
            {
                _state.State.Status.OverallStatus = ExecutionStatusEnum.Completed;
                if (_state.State.Execution != null)
                {
                    _state.State.Execution.Status = ExecutionStatusEnum.Completed;
                    _state.State.Execution.CompletedAt = DateTime.UtcNow;
                }
            }
            else
            {
                _state.State.Status.OverallStatus = ExecutionStatusEnum.Running;
                if (_state.State.Execution != null)
                {
                    _state.State.Execution.Status = ExecutionStatusEnum.Running;
                }
            }

            await _state.WriteStateAsync();

            _logger.LogInformation("Updated book {BookName} status to {Status}", bookName, bookStatus.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update book status for {BookName}", bookName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

[GenerateSerializer]
public sealed class ExecutionState
{
    [Id(0)]
    public Execution? Execution { get; set; }

    [Id(1)]
    public ExecutionStatus Status { get; set; } = new();
}
