using Orleans;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Grains.Interfaces;

public interface IComparisonGrain : IGrainWithStringKey
{
    Task<ComparisonSummary> CreateComparisonAsync(ComparisonRequest request);
    
    Task<byte[]> QueryAsync(ReportQueryRequest request, ResponseFormat format = ResponseFormat.ArrowIpc);
    
    Task<ComparisonSummary> GetSummaryAsync();
    
    Task<Dictionary<string, int>> GetMarketDataDiffAsync();
}
