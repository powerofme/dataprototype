using Apache.Arrow;
using Microsoft.Extensions.Logging;

namespace RiskDataPlatform.Arrow;

public class IncrementalWriter
{
    private readonly ILogger<IncrementalWriter>? _logger;

    public IncrementalWriter(ILogger<IncrementalWriter>? logger = null)
    {
        _logger = logger;
    }

    public async Task AppendToFileAsync(
        string filePath,
        RecordBatch newBatch,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger?.LogDebug("File does not exist, creating new file: {FilePath}", filePath);
            using var arrowWriter = new ArrowWriter(_logger as ILogger<ArrowWriter>);
            await arrowWriter.WriteFileAsync(filePath, newBatch, cancellationToken);
            return;
        }

        _logger?.LogDebug("Appending to existing file: {FilePath}", filePath);

        using var reader = new ArrowReader(_logger as ILogger<ArrowReader>);
        var existingBatches = await reader.ReadFileAsync(filePath, cancellationToken);

        if (existingBatches.Count > 0 && !existingBatches[0].Schema.Equals(newBatch.Schema))
        {
            throw new InvalidOperationException(
                $"New batch schema does not match existing file schema. Expected: {existingBatches[0].Schema}, Got: {newBatch.Schema}");
        }

        existingBatches.Add(newBatch);

        using var writer = new ArrowWriter(_logger as ILogger<ArrowWriter>);
        await writer.WriteFileAsync(filePath, newBatch.Schema, existingBatches, cancellationToken);

        _logger?.LogDebug("Successfully appended batch with {RowCount} rows to file", newBatch.Length);
    }

    public async Task AppendBatchesToFileAsync(
        string filePath,
        IEnumerable<RecordBatch> newBatches,
        CancellationToken cancellationToken = default)
    {
        var batchList = newBatches.ToList();
        
        if (batchList.Count == 0)
        {
            _logger?.LogDebug("No batches to append");
            return;
        }

        var schema = batchList[0].Schema;

        if (!File.Exists(filePath))
        {
            _logger?.LogDebug("File does not exist, creating new file: {FilePath}", filePath);
            using var arrowWriter = new ArrowWriter(_logger as ILogger<ArrowWriter>);
            await arrowWriter.WriteFileAsync(filePath, schema, batchList, cancellationToken);
            return;
        }

        _logger?.LogDebug("Appending {BatchCount} batches to existing file: {FilePath}", batchList.Count, filePath);

        using var reader = new ArrowReader(_logger as ILogger<ArrowReader>);
        var existingBatches = await reader.ReadFileAsync(filePath, cancellationToken);

        if (existingBatches.Count > 0 && !existingBatches[0].Schema.Equals(schema))
        {
            throw new InvalidOperationException(
                $"New batch schema does not match existing file schema. Expected: {existingBatches[0].Schema}, Got: {schema}");
        }

        existingBatches.AddRange(batchList);

        using var writer = new ArrowWriter(_logger as ILogger<ArrowWriter>);
        await writer.WriteFileAsync(filePath, schema, existingBatches, cancellationToken);

        _logger?.LogDebug("Successfully appended {BatchCount} batches to file", batchList.Count);
    }

    public async Task AppendToStreamAsync(
        Stream existingStream,
        Stream outputStream,
        RecordBatch newBatch,
        CancellationToken cancellationToken = default)
    {
        List<RecordBatch> existingBatches;

        if (existingStream.Length > 0)
        {
            _logger?.LogDebug("Reading existing batches from stream");
            using var reader = new ArrowReader(_logger as ILogger<ArrowReader>);
            existingBatches = await reader.ReadFromStreamAsync(existingStream, cancellationToken);

            if (existingBatches.Count > 0 && !existingBatches[0].Schema.Equals(newBatch.Schema))
            {
                throw new InvalidOperationException(
                    $"New batch schema does not match existing stream schema. Expected: {existingBatches[0].Schema}, Got: {newBatch.Schema}");
            }
        }
        else
        {
            _logger?.LogDebug("Empty stream, starting fresh");
            existingBatches = new List<RecordBatch>();
        }

        existingBatches.Add(newBatch);

        using var writer = new ArrowWriter(_logger as ILogger<ArrowWriter>);
        await writer.WriteToStreamAsync(outputStream, newBatch.Schema, existingBatches, cancellationToken);

        _logger?.LogDebug("Successfully appended batch with {RowCount} rows to stream", newBatch.Length);
    }

    public async Task<byte[]> AppendToByteArrayAsync(
        byte[] existingData,
        RecordBatch newBatch,
        CancellationToken cancellationToken = default)
    {
        List<RecordBatch> existingBatches;

        if (existingData.Length > 0)
        {
            _logger?.LogDebug("Reading existing batches from byte array");
            using var reader = new ArrowReader(_logger as ILogger<ArrowReader>);
            existingBatches = await reader.ReadFromByteArrayAsync(existingData, cancellationToken);

            if (existingBatches.Count > 0 && !existingBatches[0].Schema.Equals(newBatch.Schema))
            {
                throw new InvalidOperationException(
                    $"New batch schema does not match existing data schema. Expected: {existingBatches[0].Schema}, Got: {newBatch.Schema}");
            }
        }
        else
        {
            _logger?.LogDebug("Empty data, starting fresh");
            existingBatches = new List<RecordBatch>();
        }

        existingBatches.Add(newBatch);

        using var writer = new ArrowWriter(_logger as ILogger<ArrowWriter>);
        return await writer.WriteToByteArrayAsync(newBatch.Schema, existingBatches, cancellationToken);
    }
}
