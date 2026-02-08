using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Orleans;
using RiskDataPlatform.Core.Constants;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Models;
using RiskDataPlatform.Grains.Interfaces;

namespace RiskDataPlatform.Grains;

public sealed class NotificationGrain : Grain, INotificationGrain
{
    private readonly ILogger<NotificationGrain> _logger;
    private readonly IHubContext<RiskDataHub>? _hubContext;
    private string _tenantId = string.Empty;
    private readonly Dictionary<string, Timer> _debounceTimers = new();
    private readonly Dictionary<string, (string deskId, string bookName, int tradeCount)> _pendingTradePriced = new();
    private readonly object _lock = new();

    public NotificationGrain(
        ILogger<NotificationGrain> logger,
        IHubContext<RiskDataHub>? hubContext = null)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var grainKey = this.GetPrimaryKeyString();
        var (tenantId, _) = GrainKeys.ParseGrainKey(grainKey);
        _tenantId = tenantId;

        using var activity = Telemetry.Grains.StartActivity("NotificationGrain.Activate");
        activity?.SetTag("tenantId", _tenantId);

        _logger.LogInformation("NotificationGrain activated for tenant {TenantId}", _tenantId);

        return base.OnActivateAsync(cancellationToken);
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            foreach (var timer in _debounceTimers.Values)
            {
                timer.Dispose();
            }
            _debounceTimers.Clear();
        }

        return base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task NotifyExecutionCompletedAsync(Guid executionId, string deskId, string reportType)
    {
        using var activity = Telemetry.Grains.StartActivity("NotificationGrain.NotifyExecutionCompleted");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("executionId", executionId);
        activity?.SetTag("deskId", deskId);
        activity?.SetTag("reportType", reportType);

        try
        {
            if (_hubContext == null)
            {
                _logger.LogWarning("HubContext not available, skipping notification");
                return;
            }

            var groupName = $"{_tenantId}:{deskId}";
            await _hubContext.Clients.Group(groupName).SendAsync(
                "ExecutionCompleted",
                new
                {
                    ExecutionId = executionId,
                    DeskId = deskId,
                    ReportType = reportType,
                    CompletedAt = DateTime.UtcNow
                });

            _logger.LogInformation("Sent execution completed notification for {ExecutionId}", executionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send execution completed notification for {ExecutionId}", executionId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task NotifyExecutionStatusChangedAsync(Guid executionId, string deskId, ExecutionStatusEnum status)
    {
        using var activity = Telemetry.Grains.StartActivity("NotificationGrain.NotifyExecutionStatusChanged");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("executionId", executionId);
        activity?.SetTag("deskId", deskId);
        activity?.SetTag("status", status);

        try
        {
            if (_hubContext == null)
            {
                _logger.LogWarning("HubContext not available, skipping notification");
                return;
            }

            var groupName = $"{_tenantId}:{deskId}";
            await _hubContext.Clients.Group(groupName).SendAsync(
                "ExecutionStatusChanged",
                new
                {
                    ExecutionId = executionId,
                    DeskId = deskId,
                    Status = status.ToString(),
                    Timestamp = DateTime.UtcNow
                });

            _logger.LogInformation("Sent execution status changed notification for {ExecutionId} to {Status}",
                executionId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send execution status changed notification for {ExecutionId}",
                executionId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public Task NotifyTradePricedAsync(string deskId, string bookName, int tradeCount)
    {
        using var activity = Telemetry.Grains.StartActivity("NotificationGrain.NotifyTradePriced");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("deskId", deskId);
        activity?.SetTag("bookName", bookName);
        activity?.SetTag("tradeCount", tradeCount);

        try
        {
            if (_hubContext == null)
            {
                _logger.LogWarning("HubContext not available, skipping notification");
                return Task.CompletedTask;
            }

            var key = $"{deskId}:{bookName}";

            lock (_lock)
            {
                if (_pendingTradePriced.ContainsKey(key))
                {
                    var existing = _pendingTradePriced[key];
                    _pendingTradePriced[key] = (deskId, bookName, existing.tradeCount + tradeCount);
                }
                else
                {
                    _pendingTradePriced[key] = (deskId, bookName, tradeCount);
                }

                if (_debounceTimers.TryGetValue(key, out var existingTimer))
                {
                    existingTimer.Change(500, Timeout.Infinite);
                }
                else
                {
                    var timer = new Timer(async _ => await SendTradePricedNotificationAsync(key), null, 500, Timeout.Infinite);
                    _debounceTimers[key] = timer;
                }
            }

            _logger.LogDebug("Debounced trade priced notification for {DeskId}:{BookName}, count {TradeCount}",
                deskId, bookName, tradeCount);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to debounce trade priced notification for {DeskId}:{BookName}",
                deskId, bookName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task NotifyHealthStatusChangedAsync(string component, string status)
    {
        using var activity = Telemetry.Grains.StartActivity("NotificationGrain.NotifyHealthStatusChanged");
        activity?.SetTag("tenantId", _tenantId);
        activity?.SetTag("component", component);
        activity?.SetTag("status", status);

        try
        {
            if (_hubContext == null)
            {
                _logger.LogWarning("HubContext not available, skipping notification");
                return;
            }

            var groupName = $"{_tenantId}:system";
            await _hubContext.Clients.Group(groupName).SendAsync(
                "HealthStatusChanged",
                new
                {
                    Component = component,
                    Status = status,
                    Timestamp = DateTime.UtcNow
                });

            _logger.LogInformation("Sent health status changed notification for {Component} to {Status}",
                component, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send health status changed notification for {Component}", component);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task SendTradePricedNotificationAsync(string key)
    {
        try
        {
            (string deskId, string bookName, int tradeCount) notification;

            lock (_lock)
            {
                if (!_pendingTradePriced.TryGetValue(key, out notification))
                {
                    return;
                }

                _pendingTradePriced.Remove(key);
                
                if (_debounceTimers.TryGetValue(key, out var timer))
                {
                    timer.Dispose();
                    _debounceTimers.Remove(key);
                }
            }

            if (_hubContext != null)
            {
                var groupName = $"{_tenantId}:{notification.deskId}";
                await _hubContext.Clients.Group(groupName).SendAsync(
                    "TradePriced",
                    new
                    {
                        DeskId = notification.deskId,
                        BookName = notification.bookName,
                        TradeCount = notification.tradeCount,
                        Timestamp = DateTime.UtcNow
                    });

                _logger.LogInformation("Sent trade priced notification for {DeskId}:{BookName}, count {TradeCount}",
                    notification.deskId, notification.bookName, notification.tradeCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send trade priced notification for {Key}", key);
        }
    }
}

public class RiskDataHub : Hub
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
