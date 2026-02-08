using Microsoft.AspNetCore.SignalR;

namespace RiskDataPlatform.Api.Hubs;

public class RiskHub : Hub
{
    private readonly ILogger<RiskHub> _logger;

    public RiskHub(ILogger<RiskHub> logger)
    {
        _logger = logger;
    }

    public async Task SubscribeToExecution(string executionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"execution:{executionId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to execution {ExecutionId}", 
            Context.ConnectionId, executionId);
    }

    public async Task UnsubscribeFromExecution(string executionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"execution:{executionId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from execution {ExecutionId}", 
            Context.ConnectionId, executionId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
