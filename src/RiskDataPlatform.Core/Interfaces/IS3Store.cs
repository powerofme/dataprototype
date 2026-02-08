namespace RiskDataPlatform.Core.Interfaces;

public interface IS3Store
{
    Task PutObjectAsync(
        string bucket,
        string key,
        Stream data,
        string? contentType = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    Task<Stream?> GetObjectAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default);

    Task DeleteObjectAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default);

    Task<List<string>> ListObjectsAsync(
        string bucket,
        string prefix,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, string>?> GetObjectMetadataAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default);
}
