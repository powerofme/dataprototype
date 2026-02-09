using System.Runtime.CompilerServices;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Microsoft.Extensions.Logging;

namespace RiskDataPlatform.Arrow;

public class ArrowReader : IDisposable
{
    private readonly ILogger<ArrowReader>? _logger;
    private bool _disposed;

    public ArrowReader(ILogger<ArrowReader>? logger = null)
    {
        _logger = logger;
    }

    public async Task<List<RecordBatch>> ReadFromByteArrayAsync(
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(data);
        return await ReadFromStreamAsync(stream, cancellationToken);
    }

    public async Task<List<RecordBatch>> ReadFromStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        var recordBatches = new List<RecordBatch>();

        using var reader = new Apache.Arrow.Ipc.ArrowFileReader(stream);

        _logger?.LogDebug("Reading Arrow IPC file with schema: {Schema}", reader.Schema);

        for (int i = 0; i < reader.RecordBatchCountAsync().Result; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var batch = await reader.ReadRecordBatchAsync(i, cancellationToken);
            recordBatches.Add(batch);
            
            _logger?.LogDebug("Read RecordBatch {Index} with {RowCount} rows", i, batch.Length);
        }

        _logger?.LogDebug("Arrow IPC file read completed. Total batches: {Count}", recordBatches.Count);

        return recordBatches;
    }

    public async Task<Schema> ReadSchemaAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        await Task.CompletedTask;

        using var reader = new Apache.Arrow.Ipc.ArrowFileReader(stream);
        return reader.Schema;
    }

    public async Task<Schema> ReadSchemaFromByteArrayAsync(
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(data);
        return await ReadSchemaAsync(stream, cancellationToken);
    }

    public async Task<List<RecordBatch>> ReadFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        return await ReadFromStreamAsync(fileStream, cancellationToken);
    }

    public async Task<(Schema schema, List<RecordBatch> batches)> ReadFileWithSchemaAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        
        using var reader = new Apache.Arrow.Ipc.ArrowFileReader(fileStream);
        
        var schema = reader.Schema;
        var batches = new List<RecordBatch>();
        var batchCount = await reader.RecordBatchCountAsync();

        for (int i = 0; i < batchCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = await reader.ReadRecordBatchAsync(i, cancellationToken);
            batches.Add(batch);
        }

        return (schema, batches);
    }

    public async IAsyncEnumerable<RecordBatch> ReadStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        using var reader = new Apache.Arrow.Ipc.ArrowFileReader(stream);
        var batchCount = await reader.RecordBatchCountAsync();

        for (int i = 0; i < batchCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await reader.ReadRecordBatchAsync(i, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
