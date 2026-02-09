using Orleans;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Grains.Interfaces;

public interface IExecutionGrain : IGrainWithStringKey
{
    Task<Execution> CreateAsync(Execution execution);
    
    Task<ExecutionStatus> GetStatusAsync();
    
    Task CancelAsync();
    
    Task CompleteAsync();
    
    Task UpdateBookStatusAsync(string bookName, BookStatus bookStatus);
}
