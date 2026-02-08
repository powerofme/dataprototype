using Orleans;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Grains.Interfaces;

public interface IDeskGrain : IGrainWithStringKey
{
    Task<List<Execution>> GetExecutionsAsync(int limit = 100);
    
    Task<Dictionary<string, Execution>> GetLatestExecutionsAsync();
    
    Task StartIncrementalAsync(string reportType);
    
    Task StopIncrementalAsync(string reportType);
    
    Task<Dictionary<string, bool>> GetIncrementalStatusAsync();
}
