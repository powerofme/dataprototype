using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Interfaces;

namespace RiskDataPlatform.Infrastructure.Storage;

public sealed class InMemoryS3Store : IS3Store
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, StoredObject>> _storage = new();
    private readonly ILogger<InMemoryS3Store> _logger;

    public InMemoryS3Store(ILogger<InMemoryS3Store> logger)
    {
        _logger = logger;
    }

    public async Task PutObjectAsync(
        string bucket,
        string key,
        Stream data,
        string? contentType = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.Storage.StartActivity("S3.PutObject");
        activity?.SetTag("bucket", bucket);
        activity?.SetTag("key", key);
        activity?.SetTag("contentType", contentType);

        _logger.LogInformation("PutObject: bucket={Bucket}, key={Key}, contentType={ContentType}", bucket, key, contentType);

        var bucketDict = _storage.GetOrAdd(bucket, _ => new ConcurrentDictionary<string, StoredObject>());

        using var memoryStream = new MemoryStream();
        await data.CopyToAsync(memoryStream, cancellationToken);
        var content = memoryStream.ToArray();

        var storedObject = new StoredObject
        {
            Content = content,
            ContentType = contentType,
            Metadata = metadata ?? new Dictionary<string, string>(),
            LastModified = DateTime.UtcNow
        };

        bucketDict[key] = storedObject;

        _logger.LogInformation("PutObject completed: bucket={Bucket}, key={Key}, size={Size} bytes", bucket, key, content.Length);
    }

    public Task<Stream?> GetObjectAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.Storage.StartActivity("S3.GetObject");
        activity?.SetTag("bucket", bucket);
        activity?.SetTag("key", key);

        _logger.LogInformation("GetObject: bucket={Bucket}, key={Key}", bucket, key);

        if (_storage.TryGetValue(bucket, out var bucketDict) &&
            bucketDict.TryGetValue(key, out var storedObject))
        {
            _logger.LogInformation("GetObject found: bucket={Bucket}, key={Key}, size={Size} bytes", bucket, key, storedObject.Content.Length);
            // Caller is responsible for disposing the returned stream
            Stream result = new MemoryStream(storedObject.Content, writable: false);
            return Task.FromResult<Stream?>(result);
        }

        _logger.LogWarning("GetObject not found: bucket={Bucket}, key={Key}", bucket, key);
        return Task.FromResult<Stream?>(null);
    }

    public Task<bool> ExistsAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.Storage.StartActivity("S3.Exists");
        activity?.SetTag("bucket", bucket);
        activity?.SetTag("key", key);

        _logger.LogInformation("Exists: bucket={Bucket}, key={Key}", bucket, key);

        var exists = _storage.TryGetValue(bucket, out var bucketDict) && bucketDict.ContainsKey(key);

        _logger.LogInformation("Exists result: bucket={Bucket}, key={Key}, exists={Exists}", bucket, key, exists);

        return Task.FromResult(exists);
    }

    public Task DeleteObjectAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.Storage.StartActivity("S3.DeleteObject");
        activity?.SetTag("bucket", bucket);
        activity?.SetTag("key", key);

        _logger.LogInformation("DeleteObject: bucket={Bucket}, key={Key}", bucket, key);

        if (_storage.TryGetValue(bucket, out var bucketDict))
        {
            bucketDict.TryRemove(key, out _);
            _logger.LogInformation("DeleteObject completed: bucket={Bucket}, key={Key}", bucket, key);
        }
        else
        {
            _logger.LogWarning("DeleteObject bucket not found: bucket={Bucket}, key={Key}", bucket, key);
        }

        return Task.CompletedTask;
    }

    public Task<List<string>> ListObjectsAsync(
        string bucket,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.Storage.StartActivity("S3.ListObjects");
        activity?.SetTag("bucket", bucket);
        activity?.SetTag("prefix", prefix);

        _logger.LogInformation("ListObjects: bucket={Bucket}, prefix={Prefix}", bucket, prefix);

        var result = new List<string>();

        if (_storage.TryGetValue(bucket, out var bucketDict))
        {
            result = bucketDict.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(k => k)
                .ToList();
        }

        _logger.LogInformation("ListObjects completed: bucket={Bucket}, prefix={Prefix}, count={Count}", bucket, prefix, result.Count);

        return Task.FromResult(result);
    }

    public Task<Dictionary<string, string>?> GetObjectMetadataAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.Storage.StartActivity("S3.GetObjectMetadata");
        activity?.SetTag("bucket", bucket);
        activity?.SetTag("key", key);

        _logger.LogInformation("GetObjectMetadata: bucket={Bucket}, key={Key}", bucket, key);

        if (_storage.TryGetValue(bucket, out var bucketDict) &&
            bucketDict.TryGetValue(key, out var storedObject))
        {
            _logger.LogInformation("GetObjectMetadata found: bucket={Bucket}, key={Key}", bucket, key);
            return Task.FromResult<Dictionary<string, string>?>(storedObject.Metadata);
        }

        _logger.LogWarning("GetObjectMetadata not found: bucket={Bucket}, key={Key}", bucket, key);
        return Task.FromResult<Dictionary<string, string>?>(null);
    }

    private sealed class StoredObject
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string? ContentType { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public DateTime LastModified { get; set; }
    }
}
