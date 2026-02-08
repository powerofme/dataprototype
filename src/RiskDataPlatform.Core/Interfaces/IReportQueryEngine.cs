using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Core.Interfaces;

public interface IReportQueryEngine
{
    Task<byte[]> QueryAsync(
        string tenantId,
        Guid executionId,
        string deskId,
        string reportType,
        ReportQueryRequest request,
        ResponseFormat format = ResponseFormat.ArrowIpc,
        CancellationToken cancellationToken = default);

    Task<SsrmResponse> QuerySsrmAsync(
        string tenantId,
        Guid executionId,
        string deskId,
        string reportType,
        SsrmRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> GetTimelineDataAsync(
        string tenantId,
        Guid executionId,
        string deskId,
        string reportType,
        TimelineRequest request,
        ResponseFormat format = ResponseFormat.ArrowIpc,
        CancellationToken cancellationToken = default);

    Task<ComparisonSummary> CompareExecutionsAsync(
        string tenantId,
        ComparisonRequest request,
        CancellationToken cancellationToken = default);
}
