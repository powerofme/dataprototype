using Orleans;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Grains.Interfaces;

public interface IReportDataGrain : IGrainWithStringKey
{
    Task<byte[]> QueryAsync(ReportQueryRequest request, ResponseFormat format = ResponseFormat.ArrowIpc);
    
    Task<SsrmResponse> QuerySsrmAsync(SsrmRequest request);
    
    Task<string> GetSchemaAsync();
    
    Task InvalidateCacheAsync();
}
