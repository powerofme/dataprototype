using System.Data;
using System.Text.Json;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using RiskDataPlatform.Core.Interfaces;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Query;

public sealed class DuckDbQueryEngine : IReportQueryEngine, IDisposable
{
    private readonly ILogger<DuckDbQueryEngine> _logger;
    private readonly ArrowDataLoader _dataLoader;
    private readonly SsrmToDuckDbVisitor _ssrmVisitor;
    private readonly ComparisonQueryBuilder _comparisonBuilder;
    private readonly DuckDBConnection _connection;
    private readonly SemaphoreSlim _queryLock = new(10, 10);
    private bool _disposed;

    public DuckDbQueryEngine(ILogger<DuckDbQueryEngine> logger)
    {
        _logger = logger;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var dataLoaderLogger = loggerFactory.CreateLogger<ArrowDataLoader>();
        _dataLoader = new ArrowDataLoader(dataLoaderLogger);
        _ssrmVisitor = new SsrmToDuckDbVisitor();
        _comparisonBuilder = new ComparisonQueryBuilder();
        _connection = new DuckDBConnection("DataSource=:memory:");
        _connection.Open();
    }

    public async Task<byte[]> QueryAsync(
        string tenantId,
        Guid executionId,
        string deskId,
        string reportType,
        ReportQueryRequest request,
        ResponseFormat format = ResponseFormat.ArrowIpc,
        CancellationToken cancellationToken = default)
    {
        await _queryLock.WaitAsync(cancellationToken);
        try
        {
            var tableName = GetTableName(tenantId, executionId, deskId, reportType);

            var ssrmRequest = new SsrmRequest
            {
                FilterModel = request.FilterModel,
                SortModel = request.SortModel,
                GroupKeys = request.GroupKeys,
                StartRow = request.StartRow,
                EndRow = request.EndRow
            };

            var sql = _ssrmVisitor.BuildQuery(tableName, ssrmRequest, out var parameters);

            using var command = _connection.CreateCommand();
            command.CommandText = sql;

            for (int i = 0; i < parameters.Count; i++)
            {
                var param = command.CreateParameter();
                param.Value = parameters[i];
                command.Parameters.Add(param);
            }

            var recordBatch = await ExecuteQueryToRecordBatchAsync(command, cancellationToken);

            return await SerializeRecordBatchAsync(recordBatch, cancellationToken);
        }
        finally
        {
            _queryLock.Release();
        }
    }

    public async Task<SsrmResponse> QuerySsrmAsync(
        string tenantId,
        Guid executionId,
        string deskId,
        string reportType,
        SsrmRequest request,
        CancellationToken cancellationToken = default)
    {
        await _queryLock.WaitAsync(cancellationToken);
        try
        {
            var tableName = GetTableName(tenantId, executionId, deskId, reportType);

            var sql = _ssrmVisitor.BuildQuery(tableName, request, out var parameters);

            using var command = _connection.CreateCommand();
            command.CommandText = sql;

            for (int i = 0; i < parameters.Count; i++)
            {
                var param = command.CreateParameter();
                param.Value = parameters[i];
                command.Parameters.Add(param);
            }

            var rows = await ExecuteQueryToRowsAsync(command, cancellationToken);

            var countSql = _ssrmVisitor.BuildQuery(tableName, new SsrmRequest
            {
                FilterModel = request.FilterModel,
                GroupKeys = request.GroupKeys,
                RowGroupCols = request.RowGroupCols,
                StartRow = 0,
                EndRow = int.MaxValue
            }, out var countParameters);

            var totalCount = await GetTotalCountAsync(countSql, countParameters, cancellationToken);

            return new SsrmResponse
            {
                Rows = rows,
                LastRow = totalCount <= request.EndRow ? totalCount : null
            };
        }
        finally
        {
            _queryLock.Release();
        }
    }

    public async Task<byte[]> GetTimelineDataAsync(
        string tenantId,
        Guid executionId,
        string deskId,
        string reportType,
        TimelineRequest request,
        ResponseFormat format = ResponseFormat.ArrowIpc,
        CancellationToken cancellationToken = default)
    {
        await _queryLock.WaitAsync(cancellationToken);
        try
        {
            var tableName = GetTableName(tenantId, executionId, deskId, reportType);

            var (sql, parameters) = BuildTimelineQuery(tableName, request);

            using var command = _connection.CreateCommand();
            command.CommandText = sql;

            foreach (var param in parameters)
            {
                command.Parameters.Add(CreateParameter(command, param));
            }

            var recordBatch = await ExecuteQueryToRecordBatchAsync(command, cancellationToken);

            return await SerializeRecordBatchAsync(recordBatch, cancellationToken);
        }
        finally
        {
            _queryLock.Release();
        }
    }

    public async Task<ComparisonSummary> CompareExecutionsAsync(
        string tenantId,
        ComparisonRequest request,
        CancellationToken cancellationToken = default)
    {
        await _queryLock.WaitAsync(cancellationToken);
        try
        {
            var baseTableName = GetTableName(tenantId, request.BaseExecutionId, request.DeskId, request.ReportType);
            var compareTableName = GetTableName(tenantId, request.CompareExecutionId, request.DeskId, request.ReportType);

            var keyColumns = await GetKeyColumnsAsync(baseTableName, cancellationToken);
            var valueColumns = await GetNumericColumnsAsync(baseTableName, cancellationToken);

            var (comparisonSql, comparisonParams) = _comparisonBuilder.BuildComparisonQuery(
                baseTableName,
                compareTableName,
                keyColumns,
                valueColumns,
                request.Threshold);

            var comparisonTableName = $"comparison_{Guid.NewGuid():N}";
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = $"CREATE TEMP TABLE {SanitizeIdentifier(comparisonTableName)} AS {comparisonSql}";
                foreach (var param in comparisonParams)
                {
                    command.Parameters.Add(CreateParameter(command, param));
                }
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var totalDifferences = await GetTotalDifferencesAsync(comparisonTableName, cancellationToken);
            var significantDifferences = request.Threshold.HasValue
                ? await GetSignificantDifferencesAsync(comparisonTableName, valueColumns, request.Threshold.Value, cancellationToken)
                : totalDifferences;

            var differencesByBook = new Dictionary<string, int>();
            if (request.Books != null && request.Books.Count > 0)
            {
                differencesByBook = await GetDifferencesByBookAsync(comparisonTableName, cancellationToken);
            }

            using (var dropCommand = _connection.CreateCommand())
            {
                dropCommand.CommandText = $"DROP TABLE {SanitizeIdentifier(comparisonTableName)}";
                await dropCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            return new ComparisonSummary
            {
                ComparisonId = Guid.NewGuid(),
                BaseExecutionId = request.BaseExecutionId,
                CompareExecutionId = request.CompareExecutionId,
                DeskId = request.DeskId,
                ReportType = request.ReportType,
                TotalDifferences = totalDifferences,
                SignificantDifferences = significantDifferences,
                DifferencesByBook = differencesByBook,
                CreatedAt = DateTime.UtcNow,
                Status = ComparisonStatusEnum.Completed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing executions");
            return new ComparisonSummary
            {
                ComparisonId = Guid.NewGuid(),
                BaseExecutionId = request.BaseExecutionId,
                CompareExecutionId = request.CompareExecutionId,
                DeskId = request.DeskId,
                ReportType = request.ReportType,
                CreatedAt = DateTime.UtcNow,
                Status = ComparisonStatusEnum.Failed
            };
        }
        finally
        {
            _queryLock.Release();
        }
    }

    public async Task LoadDataAsync(
        string tenantId,
        Guid executionId,
        string deskId,
        string reportType,
        byte[] arrowIpcData,
        CancellationToken cancellationToken = default)
    {
        var tableName = GetTableName(tenantId, executionId, deskId, reportType);
        await _dataLoader.RegisterArrowDataAsync(_connection, tableName, arrowIpcData, cancellationToken);
    }

    private async Task<RecordBatch> ExecuteQueryToRecordBatchAsync(
        DuckDBCommand command,
        CancellationToken cancellationToken)
    {
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        var fieldBuilders = new List<(string Name, List<object> Values, Type Type)>();
        
        for (int i = 0; i < reader.FieldCount; i++)
        {
            fieldBuilders.Add((reader.GetName(i), new List<object>(), reader.GetFieldType(i)));
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                fieldBuilders[i].Values.Add(reader.IsDBNull(i) ? null! : reader.GetValue(i));
            }
        }

        var fields = new List<Field>();
        var arrays = new List<IArrowArray>();

        foreach (var (name, values, type) in fieldBuilders)
        {
            var (field, array) = CreateArrowFieldAndArray(name, values, type);
            fields.Add(field);
            arrays.Add(array);
        }

        var schema = new Schema(fields, null);
        var rowCount = fieldBuilders.Count > 0 ? fieldBuilders[0].Values.Count : 0;
        return new RecordBatch(schema, arrays, rowCount);
    }

    private (Field, IArrowArray) CreateArrowFieldAndArray(string name, List<object> values, Type type)
    {
        if (type == typeof(int) || type == typeof(int?))
        {
            var builder = new Int32Array.Builder();
            foreach (var value in values)
            {
                builder.Append(value as int?);
            }
            return (new Field(name, Int32Type.Default, nullable: true), builder.Build());
        }
        else if (type == typeof(long) || type == typeof(long?))
        {
            var builder = new Int64Array.Builder();
            foreach (var value in values)
            {
                builder.Append(value as long?);
            }
            return (new Field(name, Int64Type.Default, nullable: true), builder.Build());
        }
        else if (type == typeof(double) || type == typeof(double?))
        {
            var builder = new DoubleArray.Builder();
            foreach (var value in values)
            {
                builder.Append(value as double?);
            }
            return (new Field(name, DoubleType.Default, nullable: true), builder.Build());
        }
        else if (type == typeof(float) || type == typeof(float?))
        {
            var builder = new FloatArray.Builder();
            foreach (var value in values)
            {
                builder.Append(value as float?);
            }
            return (new Field(name, FloatType.Default, nullable: true), builder.Build());
        }
        else if (type == typeof(bool) || type == typeof(bool?))
        {
            var builder = new BooleanArray.Builder();
            foreach (var value in values)
            {
                if (value == null)
                {
                    builder.AppendNull();
                }
                else
                {
                    builder.Append((bool)value);
                }
            }
            return (new Field(name, BooleanType.Default, nullable: true), builder.Build());
        }
        else if (type == typeof(DateTime) || type == typeof(DateTime?))
        {
            var builder = new TimestampArray.Builder();
            foreach (var value in values)
            {
                if (value == null)
                {
                    builder.AppendNull();
                }
                else
                {
                    var dt = (DateTime)value;
                    var milliseconds = new DateTimeOffset(dt).ToUnixTimeMilliseconds();
                    builder.Append(DateTimeOffset.FromUnixTimeMilliseconds(milliseconds));
                }
            }
            return (new Field(name, new TimestampType(TimeUnit.Millisecond, TimeZoneInfo.Utc.Id), nullable: true), builder.Build());
        }
        else
        {
            var builder = new StringArray.Builder();
            foreach (var value in values)
            {
                builder.Append(value?.ToString());
            }
            return (new Field(name, StringType.Default, nullable: true), builder.Build());
        }
    }

    private async Task<List<object>> ExecuteQueryToRowsAsync(
        DuckDBCommand command,
        CancellationToken cancellationToken)
    {
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<object>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }

        return rows;
    }

    private async Task<byte[]> SerializeRecordBatchAsync(RecordBatch batch, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        using var writer = new ArrowStreamWriter(stream, batch.Schema, leaveOpen: true);
        await writer.WriteRecordBatchAsync(batch, cancellationToken);
        await writer.WriteEndAsync(cancellationToken);
        return stream.ToArray();
    }

    private async Task<int> GetTotalCountAsync(
        string sql,
        List<object> parameters,
        CancellationToken cancellationToken)
    {
        var countSql = $"SELECT COUNT(*) FROM ({sql}) AS subquery";

        using var command = _connection.CreateCommand();
        command.CommandText = countSql;

        for (int i = 0; i < parameters.Count; i++)
        {
            var param = command.CreateParameter();
            param.Value = parameters[i];
            command.Parameters.Add(param);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private (string sql, List<object> parameters) BuildTimelineQuery(string tableName, TimelineRequest request)
    {
        var parameters = new List<object>();
        var measures = request.Measures ?? new List<string> { "*" };
        var selectCols = measures.Select(m => m == "*" ? m : $"SUM({SanitizeIdentifier(m)}) AS {SanitizeIdentifier(m)}");

        var sql = $@"
SELECT 
    date_trunc('{request.Granularity}', timestamp) AS period,
    {string.Join(", ", selectCols)}
FROM {SanitizeIdentifier(tableName)}
WHERE timestamp BETWEEN ? AND ?";

        parameters.Add(request.StartDate);
        parameters.Add(request.EndDate);

        if (request.Filters != null && request.Filters.Count > 0)
        {
            var filterClauses = request.Filters.Select(f =>
            {
                parameters.Add(f.Value);
                return $"{SanitizeIdentifier(f.Key)} = ?";
            });
            sql += $"\nAND {string.Join(" AND ", filterClauses)}";
        }

        sql += $"\nGROUP BY period\nORDER BY period";

        return (sql, parameters);
    }

    private async Task<List<string>> GetKeyColumnsAsync(string tableName, CancellationToken cancellationToken)
    {
        var sql = $"PRAGMA table_info({SanitizeIdentifier(tableName)})";
        using var command = _connection.CreateCommand();
        command.CommandText = sql;

        var keyColumns = new List<string>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(1);
            if (columnName.Contains("Id") || columnName.Contains("Key") || columnName.Contains("Name"))
            {
                keyColumns.Add(columnName);
            }
        }

        return keyColumns.Count > 0 ? keyColumns : new List<string> { "rowid" };
    }

    private async Task<List<string>> GetNumericColumnsAsync(string tableName, CancellationToken cancellationToken)
    {
        var sql = $"PRAGMA table_info({SanitizeIdentifier(tableName)})";
        using var command = _connection.CreateCommand();
        command.CommandText = sql;

        var numericColumns = new List<string>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(1);
            var columnType = reader.GetString(2).ToUpper();

            if (columnType.Contains("INT") || columnType.Contains("DOUBLE") || 
                columnType.Contains("FLOAT") || columnType.Contains("DECIMAL") ||
                columnType.Contains("NUMERIC"))
            {
                numericColumns.Add(columnName);
            }
        }

        return numericColumns;
    }

    private async Task<int> GetTotalDifferencesAsync(string tableName, CancellationToken cancellationToken)
    {
        var sql = $"SELECT COUNT(*) FROM {SanitizeIdentifier(tableName)}";
        using var command = _connection.CreateCommand();
        command.CommandText = sql;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private async Task<int> GetSignificantDifferencesAsync(
        string tableName,
        List<string> valueColumns,
        double threshold,
        CancellationToken cancellationToken)
    {
        var (sql, parameters) = _comparisonBuilder.BuildSignificantDifferencesQuery(tableName, valueColumns, threshold);
        var countSql = $"SELECT COUNT(*) FROM ({sql}) AS subquery";

        using var command = _connection.CreateCommand();
        command.CommandText = countSql;

        foreach (var param in parameters)
        {
            command.Parameters.Add(CreateParameter(command, param));
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private async Task<Dictionary<string, int>> GetDifferencesByBookAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        var sql = _comparisonBuilder.BuildDifferencesByBookQuery(tableName, "Book");

        using var command = _connection.CreateCommand();
        command.CommandText = sql;

        var result = new Dictionary<string, int>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var book = reader.GetString(0);
                var count = reader.GetInt32(2);
                result[book] = count;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not get differences by book");
        }

        return result;
    }

    private static string GetTableName(string tenantId, Guid executionId, string deskId, string reportType)
    {
        return $"{tenantId}_{executionId:N}_{deskId}_{reportType}".Replace("-", "_");
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

    private static IDbDataParameter CreateParameter(DuckDBCommand command, object value)
    {
        var param = command.CreateParameter();
        param.Value = value;
        return param;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _queryLock.Dispose();
        _connection?.Dispose();
        _disposed = true;
    }
}
