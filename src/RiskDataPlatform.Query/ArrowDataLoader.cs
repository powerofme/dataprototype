using Apache.Arrow;
using Apache.Arrow.Ipc;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;

namespace RiskDataPlatform.Query;

public sealed class ArrowDataLoader
{
    private readonly ILogger<ArrowDataLoader> _logger;

    public ArrowDataLoader(ILogger<ArrowDataLoader> logger)
    {
        _logger = logger;
    }

    public async Task RegisterArrowDataAsync(
        DuckDBConnection connection,
        string tableName,
        byte[] arrowIpcData,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(arrowIpcData);
        using var reader = new ArrowStreamReader(stream);

        var batches = new List<RecordBatch>();
        RecordBatch? batch;
        while ((batch = await reader.ReadNextRecordBatchAsync(cancellationToken)) != null)
        {
            batches.Add(batch);
        }

        if (batches.Count == 0)
        {
            _logger.LogWarning("No record batches found in Arrow IPC data for table {TableName}", tableName);
            return;
        }

        await RegisterRecordBatchesAsync(connection, tableName, batches, cancellationToken);

        foreach (var b in batches)
        {
            b.Dispose();
        }
    }

    public async Task RegisterRecordBatchesAsync(
        DuckDBConnection connection,
        string tableName,
        IReadOnlyList<RecordBatch> batches,
        CancellationToken cancellationToken = default)
    {
        if (batches.Count == 0)
        {
            return;
        }

        using var memoryStream = new MemoryStream();
        using var writer = new ArrowStreamWriter(memoryStream, batches[0].Schema, leaveOpen: true);

        foreach (var batch in batches)
        {
            await writer.WriteRecordBatchAsync(batch, cancellationToken);
        }

        await writer.WriteEndAsync(cancellationToken);
        memoryStream.Position = 0;

        var sanitizedTableName = SanitizeIdentifier(tableName);
        
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE OR REPLACE TABLE {sanitizedTableName} AS SELECT * FROM read_ipc(?)";
        
        var parameter = command.CreateParameter();
        parameter.Value = memoryStream.ToArray();
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("Registered table {TableName} with {RowCount} rows", 
            tableName, batches.Sum(b => b.Length));
    }

    public async Task RegisterMultipleTablesAsync(
        DuckDBConnection connection,
        Dictionary<string, byte[]> tableData,
        CancellationToken cancellationToken = default)
    {
        foreach (var kvp in tableData)
        {
            await RegisterArrowDataAsync(connection, kvp.Key, kvp.Value, cancellationToken);
        }
    }

    public async Task CreateTemporaryTableAsync(
        DuckDBConnection connection,
        string tableName,
        RecordBatch batch,
        CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new ArrowStreamWriter(memoryStream, batch.Schema, leaveOpen: true);
        
        await writer.WriteRecordBatchAsync(batch, cancellationToken);
        await writer.WriteEndAsync(cancellationToken);
        memoryStream.Position = 0;

        var sanitizedTableName = SanitizeIdentifier(tableName);
        
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE OR REPLACE TEMP TABLE {sanitizedTableName} AS SELECT * FROM read_ipc(?)";
        
        var parameter = command.CreateParameter();
        parameter.Value = memoryStream.ToArray();
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string SanitizeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier cannot be null or whitespace", nameof(identifier));
        }

        if (!identifier.All(c => char.IsLetterOrDigit(c) || c == '_'))
        {
            throw new ArgumentException($"Invalid identifier: {identifier}. Only alphanumeric characters and underscores are allowed.", nameof(identifier));
        }

        return $"\"{identifier}\"";
    }
}
