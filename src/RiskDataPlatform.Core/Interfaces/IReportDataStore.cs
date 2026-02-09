using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Core.Interfaces;

public interface IReportDataStore
{
    Task StoreReportDataAsync(
        string tenantId,
        Guid executionId,
        string deskId,
        string reportType,
        string bookName,
        byte[] arrowData,
        CancellationToken cancellationToken = default);

    Task<byte[]?> GetReportDataAsync(
        string tenantId,
        Guid executionId,
        string deskId,
        string reportType,
        string bookName,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string tenantId,
        Guid executionId,
        string deskId,
        string reportType,
        string bookName,
        CancellationToken cancellationToken = default);

    Task DeleteReportDataAsync(
        string tenantId,
        Guid executionId,
        string deskId,
        string reportType,
        string? bookName = null,
        CancellationToken cancellationToken = default);

    Task<List<string>> ListBooksAsync(
        string tenantId,
        Guid executionId,
        string deskId,
        string reportType,
        CancellationToken cancellationToken = default);
}
