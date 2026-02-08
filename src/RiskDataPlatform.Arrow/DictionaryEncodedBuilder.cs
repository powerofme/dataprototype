using Apache.Arrow;
using Apache.Arrow.Types;

namespace RiskDataPlatform.Arrow;

public class DictionaryEncodedBuilder<TValue> where TValue : notnull
{
    private readonly Dictionary<TValue, int> _valueToIndex = new();
    private readonly List<TValue> _dictionary = new();
    private readonly List<int> _indices = new();
    private readonly bool _nullable;

    public DictionaryEncodedBuilder(bool nullable = false)
    {
        _nullable = nullable;
    }

    public int Count => _indices.Count;
    public int Cardinality => _dictionary.Count;

    public void Append(TValue? value)
    {
        if (value == null)
        {
            if (!_nullable)
                throw new InvalidOperationException("Cannot append null to non-nullable dictionary builder");
            _indices.Add(-1);
            return;
        }

        if (!_valueToIndex.TryGetValue(value, out var index))
        {
            index = _dictionary.Count;
            _dictionary.Add(value);
            _valueToIndex[value] = index;
        }

        _indices.Add(index);
    }

    public void AppendRange(IEnumerable<TValue?> values)
    {
        foreach (var value in values)
        {
            Append(value);
        }
    }

    public IArrowType GetOptimalIndexType()
    {
        return Schemas.ReportSchemas.GetIndexTypeForCardinality(_dictionary.Count);
    }

    public (IArrowArray dictionary, IArrowArray indices) Build(IArrowType? indexType = null)
    {
        indexType ??= GetOptimalIndexType();

        var dictionaryArray = BuildDictionaryArray();
        var indicesArray = BuildIndicesArray(indexType);

        return (dictionaryArray, indicesArray);
    }

    private IArrowArray BuildDictionaryArray()
    {
        if (typeof(TValue) == typeof(string))
        {
            var builder = new Apache.Arrow.StringArray.Builder();
            foreach (var value in _dictionary)
            {
                builder.Append((string)(object)value);
            }
            return builder.Build();
        }
        else if (typeof(TValue) == typeof(int))
        {
            var builder = new Apache.Arrow.Int32Array.Builder();
            foreach (var value in _dictionary)
            {
                builder.Append((int)(object)value);
            }
            return builder.Build();
        }
        else if (typeof(TValue) == typeof(long))
        {
            var builder = new Apache.Arrow.Int64Array.Builder();
            foreach (var value in _dictionary)
            {
                builder.Append((long)(object)value);
            }
            return builder.Build();
        }
        else if (typeof(TValue) == typeof(double))
        {
            var builder = new Apache.Arrow.DoubleArray.Builder();
            foreach (var value in _dictionary)
            {
                builder.Append((double)(object)value);
            }
            return builder.Build();
        }
        else
        {
            throw new NotSupportedException($"Dictionary type {typeof(TValue)} is not supported");
        }
    }

    private IArrowArray BuildIndicesArray(IArrowType indexType)
    {
        if (indexType is Int8Type)
        {
            var builder = new Apache.Arrow.Int8Array.Builder();
            foreach (var index in _indices)
            {
                if (index == -1 && _nullable)
                    builder.AppendNull();
                else
                    builder.Append((sbyte)index);
            }
            return builder.Build();
        }
        else if (indexType is Int16Type)
        {
            var builder = new Apache.Arrow.Int16Array.Builder();
            foreach (var index in _indices)
            {
                if (index == -1 && _nullable)
                    builder.AppendNull();
                else
                    builder.Append((short)index);
            }
            return builder.Build();
        }
        else if (indexType is Int32Type)
        {
            var builder = new Apache.Arrow.Int32Array.Builder();
            foreach (var index in _indices)
            {
                if (index == -1 && _nullable)
                    builder.AppendNull();
                else
                    builder.Append(index);
            }
            return builder.Build();
        }
        else
        {
            throw new NotSupportedException($"Index type {indexType} is not supported");
        }
    }

    public void Clear()
    {
        _valueToIndex.Clear();
        _dictionary.Clear();
        _indices.Clear();
    }
}
