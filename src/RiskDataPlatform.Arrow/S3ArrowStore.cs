using Apache.Arrow;
using Apache.Arrow.Ipc;
using Microsoft.Extensions.Logging;
using RiskDataPlatform.Core.Interfaces;

namespace RiskDataPlatform.Arrow;

public class S3ArrowStore
{
    private readonly IS3Store _s3Store;
    private readonly ILogger<S3ArrowStore>? _logger;
    private readonly ArrowWriter _writer;
    private readonly ArrowReader _reader;

    public S3ArrowStore(IS3Store s3Store, ILogger<S3ArrowStore>? logger = null)
    {
        _s3Store = s3Store ?? throw new ArgumentNullException(nameof(s3Store));
        _logger = logger;
        _writer = new ArrowWriter(logger as ILogger<ArrowWriter>);
        _reader = new ArrowReader(logger as ILogger<ArrowReader>);
    }

    public async Task PutRecordBatchAsync(
        string bucket,
        string key,
        RecordBatch recordBatch,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Uploading RecordBatch to s3://{Bucket}/{Key}", bucket, key);

        var data = await _writer.WriteToByteArrayAsync(recordBatch, cancellationToken);
        
        using var stream = new MemoryStream(data);
        await _s3Store.PutObjectAsync(bucket, key, stream, "application/vnd.apache.arrow.file", metadata, cancellationToken);

        _logger?.LogInformation("Successfully uploaded RecordBatch with {RowCount} rows to s3://{Bucket}/{Key}",
            recordBatch.Length, bucket, key);
    }

    public async Task PutRecordBatchesAsync(
        string bucket,
        string key,
        Schema schema,
        IEnumerable<RecordBatch> recordBatches,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var batchList = recordBatches.ToList();
        _logger?.LogDebug("Uploading {BatchCount} RecordBatches to s3://{Bucket}/{Key}", batchList.Count, bucket, key);

        var data = await _writer.WriteToByteArrayAsync(schema, batchList, cancellationToken);
        
        using var stream = new MemoryStream(data);
        await _s3Store.PutObjectAsync(bucket, key, stream, "application/vnd.apache.arrow.file", metadata, cancellationToken);

        var totalRows = batchList.Sum(b => b.Length);
        _logger?.LogInformation("Successfully uploaded {BatchCount} RecordBatches with {TotalRows} rows to s3://{Bucket}/{Key}",
            batchList.Count, totalRows, bucket, key);
    }

    public async Task<List<RecordBatch>?> GetRecordBatchesAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Downloading RecordBatches from s3://{Bucket}/{Key}", bucket, key);

        var stream = await _s3Store.GetObjectAsync(bucket, key, cancellationToken);
        
        if (stream == null)
        {
            _logger?.LogWarning("Object not found at s3://{Bucket}/{Key}", bucket, key);
            return null;
        }

        using (stream)
        {
            var batches = await _reader.ReadFromStreamAsync(stream, cancellationToken);
            
            var totalRows = batches.Sum(b => b.Length);
            _logger?.LogInformation("Successfully downloaded {BatchCount} RecordBatches with {TotalRows} rows from s3://{Bucket}/{Key}",
                batches.Count, totalRows, bucket, key);

            return batches;
        }
    }

    public async Task<(Schema schema, List<RecordBatch> batches)?> GetRecordBatchesWithSchemaAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Downloading RecordBatches with schema from s3://{Bucket}/{Key}", bucket, key);

        var stream = await _s3Store.GetObjectAsync(bucket, key, cancellationToken);
        
        if (stream == null)
        {
            _logger?.LogWarning("Object not found at s3://{Bucket}/{Key}", bucket, key);
            return null;
        }

        using (stream)
        {
            Schema schema;
            List<RecordBatch> batches;

            using (var fileReader = new ArrowFileReader(stream))
            {
                schema = fileReader.Schema;
                batches = new List<RecordBatch>();
                var batchCount = await fileReader.RecordBatchCountAsync();

                for (int i = 0; i < batchCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var batch = await fileReader.ReadRecordBatchAsync(i, cancellationToken);
                    batches.Add(batch);
                }
            }

            var totalRows = batches.Sum(b => b.Length);
            _logger?.LogInformation("Successfully downloaded schema and {BatchCount} RecordBatches with {TotalRows} rows from s3://{Bucket}/{Key}",
                batches.Count, totalRows, bucket, key);

            return (schema, batches);
        }
    }

    public async Task<Schema?> GetSchemaAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Downloading schema from s3://{Bucket}/{Key}", bucket, key);

        var stream = await _s3Store.GetObjectAsync(bucket, key, cancellationToken);
        
        if (stream == null)
        {
            _logger?.LogWarning("Object not found at s3://{Bucket}/{Key}", bucket, key);
            return null;
        }

        using (stream)
        {
            var schema = await _reader.ReadSchemaAsync(stream, cancellationToken);
            _logger?.LogInformation("Successfully downloaded schema from s3://{Bucket}/{Key}", bucket, key);
            return schema;
        }
    }

    public async Task<bool> ExistsAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        return await _s3Store.ExistsAsync(bucket, key, cancellationToken);
    }

    public async Task DeleteAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Deleting Arrow file from s3://{Bucket}/{Key}", bucket, key);
        await _s3Store.DeleteObjectAsync(bucket, key, cancellationToken);
        _logger?.LogInformation("Successfully deleted Arrow file from s3://{Bucket}/{Key}", bucket, key);
    }

    public async Task<List<string>> ListArrowFilesAsync(
        string bucket,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Listing Arrow files in s3://{Bucket}/{Prefix}", bucket, prefix);
        var keys = await _s3Store.ListObjectsAsync(bucket, prefix, cancellationToken);
        _logger?.LogInformation("Found {Count} Arrow files in s3://{Bucket}/{Prefix}", keys.Count, bucket, prefix);
        return keys;
    }

    public async Task AppendToExistingAsync(
        string bucket,
        string key,
        RecordBatch newBatch,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Appending RecordBatch to existing file at s3://{Bucket}/{Key}", bucket, key);

        var existingBatches = await GetRecordBatchesAsync(bucket, key, cancellationToken) ?? new List<RecordBatch>();

        if (existingBatches.Count > 0 && !existingBatches[0].Schema.Equals(newBatch.Schema))
        {
            throw new InvalidOperationException(
                $"New batch schema does not match existing file schema at s3://{bucket}/{key}");
        }

        existingBatches.Add(newBatch);

        await PutRecordBatchesAsync(bucket, key, newBatch.Schema, existingBatches, cancellationToken: cancellationToken);

        _logger?.LogInformation("Successfully appended RecordBatch with {RowCount} rows to s3://{Bucket}/{Key}",
            newBatch.Length, bucket, key);
    }

    public string GenerateKey(string reportType, string tenant, DateTime asOfDate, string? bookId = null)
    {
        var dateStr = asOfDate.ToString("yyyy/MM/dd");
        var fileName = bookId != null 
            ? $"{reportType}_{tenant}_{bookId}_{asOfDate:yyyyMMdd}.arrow"
            : $"{reportType}_{tenant}_{asOfDate:yyyyMMdd}.arrow";
        
        return $"{reportType}/{tenant}/{dateStr}/{fileName}";
    }
}
