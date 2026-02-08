using Apache.Arrow;
using Apache.Arrow.Ipc;
using Microsoft.Extensions.Logging;

namespace RiskDataPlatform.Arrow;

public class ArrowWriter : IDisposable
{
    private readonly ILogger<ArrowWriter>? _logger;
    private bool _disposed;

    public ArrowWriter(ILogger<ArrowWriter>? logger = null)
    {
        _logger = logger;
    }

    public async Task<byte[]> WriteToByteArrayAsync(
        RecordBatch recordBatch,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await WriteToStreamAsync(stream, recordBatch, cancellationToken);
        return stream.ToArray();
    }

    public async Task<byte[]> WriteToByteArrayAsync(
        Schema schema,
        IEnumerable<RecordBatch> recordBatches,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await WriteToStreamAsync(stream, schema, recordBatches, cancellationToken);
        return stream.ToArray();
    }

    public async Task WriteToStreamAsync(
        Stream stream,
        RecordBatch recordBatch,
        CancellationToken cancellationToken = default)
    {
        await WriteToStreamAsync(stream, recordBatch.Schema, new[] { recordBatch }, cancellationToken);
    }

    public async Task WriteToStreamAsync(
        Stream stream,
        Schema schema,
        IEnumerable<RecordBatch> recordBatches,
        CancellationToken cancellationToken = default)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));
        if (recordBatches == null)
            throw new ArgumentNullException(nameof(recordBatches));

        _logger?.LogDebug("Writing Arrow IPC file with schema: {Schema}", schema);

        using var writer = new Apache.Arrow.Ipc.ArrowFileWriter(stream, schema);

        foreach (var batch in recordBatches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (!batch.Schema.Equals(schema))
            {
                throw new InvalidOperationException(
                    $"RecordBatch schema does not match expected schema. Expected: {schema}, Got: {batch.Schema}");
            }

            await writer.WriteRecordBatchAsync(batch, cancellationToken);
            _logger?.LogDebug("Wrote RecordBatch with {RowCount} rows", batch.Length);
        }

        await writer.WriteEndAsync(cancellationToken);
        _logger?.LogDebug("Arrow IPC file write completed");
    }

    public async Task WriteFileAsync(
        string filePath,
        RecordBatch recordBatch,
        CancellationToken cancellationToken = default)
    {
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 
            bufferSize: 81920, useAsync: true);
        await WriteToStreamAsync(fileStream, recordBatch, cancellationToken);
    }

    public async Task WriteFileAsync(
        string filePath,
        Schema schema,
        IEnumerable<RecordBatch> recordBatches,
        CancellationToken cancellationToken = default)
    {
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 
            bufferSize: 81920, useAsync: true);
        await WriteToStreamAsync(fileStream, schema, recordBatches, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
