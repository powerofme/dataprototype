using Apache.Arrow;
using Microsoft.Extensions.Logging;

namespace RiskDataPlatform.Arrow;

public class RecordBatchAccumulator
{
    private readonly ILogger<RecordBatchAccumulator>? _logger;
    private readonly List<RecordBatch> _batches = new();
    private Schema? _schema;

    public RecordBatchAccumulator(ILogger<RecordBatchAccumulator>? logger = null)
    {
        _logger = logger;
    }

    public Schema? Schema => _schema;
    public int BatchCount => _batches.Count;
    public long TotalRowCount => _batches.Sum(b => (long)b.Length);

    public void AddBatch(RecordBatch batch)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));

        if (_schema == null)
        {
            _schema = batch.Schema;
            _logger?.LogDebug("Initialized accumulator with schema: {Schema}", _schema);
        }
        else if (!batch.Schema.Equals(_schema))
        {
            throw new InvalidOperationException(
                $"RecordBatch schema does not match accumulator schema. Expected: {_schema}, Got: {batch.Schema}");
        }

        _batches.Add(batch);
        _logger?.LogDebug("Added batch with {RowCount} rows. Total batches: {BatchCount}, Total rows: {TotalRows}",
            batch.Length, _batches.Count, TotalRowCount);
    }

    public void AddBatches(IEnumerable<RecordBatch> batches)
    {
        foreach (var batch in batches)
        {
            AddBatch(batch);
        }
    }

    public List<RecordBatch> GetBatches()
    {
        return new List<RecordBatch>(_batches);
    }

    public void Clear()
    {
        _batches.Clear();
        _schema = null;
        _logger?.LogDebug("Accumulator cleared");
    }
}
