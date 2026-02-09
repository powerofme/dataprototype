using System.Text.Json;
using Apache.Arrow;
using Apache.Arrow.Ipc;

namespace RiskDataPlatform.Api.Formatters;

public class ResponseFormatter
{
    public static async Task WriteArrowIpcAsync(RecordBatch recordBatch, Stream outputStream, CancellationToken cancellationToken = default)
    {
        using var writer = new ArrowStreamWriter(outputStream, recordBatch.Schema, leaveOpen: true);
        await writer.WriteRecordBatchAsync(recordBatch, cancellationToken);
    }

    public static async Task WriteArrowIpcAsync(IEnumerable<RecordBatch> recordBatches, Schema schema, Stream outputStream, CancellationToken cancellationToken = default)
    {
        using var writer = new ArrowStreamWriter(outputStream, schema, leaveOpen: true);
        foreach (var batch in recordBatches)
        {
            await writer.WriteRecordBatchAsync(batch, cancellationToken);
        }
    }

    public static async Task WriteJsonAsync<T>(T data, Stream outputStream, CancellationToken cancellationToken = default)
    {
        await JsonSerializer.SerializeAsync(outputStream, data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        }, cancellationToken);
    }

    public static async Task WriteColumnarJsonAsync(RecordBatch recordBatch, Stream outputStream, CancellationToken cancellationToken = default)
    {
        var columnarData = new Dictionary<string, object>();
        
        for (int i = 0; i < recordBatch.Schema.FieldsList.Count; i++)
        {
            var field = recordBatch.Schema.GetFieldByIndex(i);
            var array = recordBatch.Column(i);
            
            var columnData = new List<object?>();
            for (int j = 0; j < array.Length; j++)
            {
                if (array.IsNull(j))
                {
                    columnData.Add(null);
                }
                else
                {
                    columnData.Add(GetValue(array, j));
                }
            }
            
            columnarData[field.Name] = columnData;
        }

        await JsonSerializer.SerializeAsync(outputStream, columnarData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        }, cancellationToken);
    }

    private static object? GetValue(IArrowArray array, int index)
    {
        return array switch
        {
            StringArray stringArray => stringArray.GetString(index),
            Int32Array int32Array => int32Array.GetValue(index),
            Int64Array int64Array => int64Array.GetValue(index),
            DoubleArray doubleArray => doubleArray.GetValue(index),
            FloatArray floatArray => floatArray.GetValue(index),
            BooleanArray boolArray => boolArray.GetValue(index),
            Date64Array dateArray => dateArray.GetValue(index).HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(dateArray.GetValue(index)!.Value).DateTime : null,
            _ => null
        };
    }
}
