using Apache.Arrow;
using Microsoft.Extensions.Logging;

namespace RiskDataPlatform.Arrow;

public class ResultMerger
{
    private readonly ILogger<ResultMerger>? _logger;

    public ResultMerger(ILogger<ResultMerger>? logger = null)
    {
        _logger = logger;
    }

    public async Task<RecordBatch?> MergeResultsAsync(
        IEnumerable<RecordBatch> batches,
        CancellationToken cancellationToken = default)
    {
        var batchList = batches.ToList();
        
        if (batchList.Count == 0)
        {
            _logger?.LogDebug("No batches to merge");
            return null;
        }

        if (batchList.Count == 1)
        {
            _logger?.LogDebug("Single batch, returning as-is");
            return batchList[0];
        }

        await Task.CompletedTask;

        var accumulator = new RecordBatchAccumulator(_logger as ILogger<RecordBatchAccumulator>);
        accumulator.AddBatches(batchList);
        
        var allBatches = accumulator.GetBatches();
        var mergedBatch = ConcatenateBatches(allBatches);
        
        _logger?.LogDebug("Merged {BatchCount} batches into single batch with {RowCount} rows",
            batchList.Count, mergedBatch?.Length ?? 0);

        return mergedBatch;
    }

    public async Task<List<RecordBatch>> MergeAndSplitAsync(
        IEnumerable<RecordBatch> batches,
        int maxRowsPerBatch,
        CancellationToken cancellationToken = default)
    {
        var mergedBatch = await MergeResultsAsync(batches, cancellationToken);
        
        if (mergedBatch == null || mergedBatch.Length <= maxRowsPerBatch)
        {
            return mergedBatch != null ? new List<RecordBatch> { mergedBatch } : new List<RecordBatch>();
        }

        _logger?.LogDebug("Splitting batch with {RowCount} rows into batches of max {MaxRows} rows",
            mergedBatch.Length, maxRowsPerBatch);

        var result = new List<RecordBatch>();
        var totalRows = mergedBatch.Length;
        
        for (int startRow = 0; startRow < totalRows; startRow += maxRowsPerBatch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var rowCount = Math.Min(maxRowsPerBatch, totalRows - startRow);
            var slicedBatch = SliceBatch(mergedBatch, startRow, rowCount);
            result.Add(slicedBatch);
            
            _logger?.LogDebug("Created batch slice from row {StartRow} with {RowCount} rows", startRow, rowCount);
        }

        return result;
    }

    public async Task<byte[]> MergeAndSerializeAsync(
        IEnumerable<RecordBatch> batches,
        CancellationToken cancellationToken = default)
    {
        var mergedBatch = await MergeResultsAsync(batches, cancellationToken);
        
        if (mergedBatch == null)
        {
            return System.Array.Empty<byte>();
        }

        using var writer = new ArrowWriter(_logger as ILogger<ArrowWriter>);
        return await writer.WriteToByteArrayAsync(mergedBatch, cancellationToken);
    }

    public async Task<List<RecordBatch>> MergeMultipleSourcesAsync(
        IEnumerable<IEnumerable<RecordBatch>> sources,
        CancellationToken cancellationToken = default)
    {
        var allBatches = new List<RecordBatch>();
        
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            allBatches.AddRange(source);
        }

        var mergedBatch = await MergeResultsAsync(allBatches, cancellationToken);
        
        return mergedBatch != null ? new List<RecordBatch> { mergedBatch } : new List<RecordBatch>();
    }

    private RecordBatch? ConcatenateBatches(List<RecordBatch> batches)
    {
        if (batches.Count == 0)
            return null;

        if (batches.Count == 1)
            return batches[0];

        var schema = batches[0].Schema;
        var totalRows = batches.Sum(b => b.Length);

        var columnArrays = new IArrowArray[schema.FieldsList.Count];

        for (int colIndex = 0; colIndex < schema.FieldsList.Count; colIndex++)
        {
            var columnBatches = new List<IArrowArray>();
            foreach (var batch in batches)
            {
                columnBatches.Add(batch.Column(colIndex));
            }
            columnArrays[colIndex] = ArrowArrayConcatenator.Concatenate(columnBatches);
        }

        return new RecordBatch(schema, columnArrays, totalRows);
    }

    private RecordBatch SliceBatch(RecordBatch batch, int offset, int length)
    {
        var schema = batch.Schema;
        var arrays = new IArrowArray[batch.ColumnCount];

        for (int i = 0; i < batch.ColumnCount; i++)
        {
            var column = batch.Column(i);
            arrays[i] = SliceArray(column, offset, length);
        }

        return new RecordBatch(schema, arrays, length);
    }

    private IArrowArray SliceArray(IArrowArray array, int offset, int length)
    {
        var slicedData = array.Data.Slice(offset, length);
        return ArrowArrayFactory.BuildArray(slicedData);
    }
}
