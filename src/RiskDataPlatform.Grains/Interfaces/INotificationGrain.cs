using Orleans;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Grains.Interfaces;

public interface INotificationGrain : IGrainWithStringKey
{
    Task NotifyExecutionCompletedAsync(Guid executionId, string deskId, string reportType);
    
    Task NotifyExecutionStatusChangedAsync(Guid executionId, string deskId, ExecutionStatusEnum status);
    
    Task NotifyTradePricedAsync(string deskId, string bookName, int tradeCount);
    
    Task NotifyHealthStatusChangedAsync(string component, string status);
}
