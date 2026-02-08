using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class RiskMeasureConfig
{
    [Id(0)]
    public string MeasureName { get; set; } = string.Empty;

    [Id(1)]
    public string DisplayName { get; set; } = string.Empty;

    [Id(2)]
    public string Unit { get; set; } = string.Empty;

    [Id(3)]
    public string DataType { get; set; } = string.Empty;

    [Id(4)]
    public int Precision { get; set; }

    [Id(5)]
    public bool IsAggregatable { get; set; }

    [Id(6)]
    public string? AggregationMethod { get; set; }

    [Id(7)]
    public Dictionary<string, object>? Metadata { get; set; }
}
