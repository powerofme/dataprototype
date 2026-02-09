using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class TimelineRequest
{
    [Id(0)]
    public DateTime StartDate { get; set; }

    [Id(1)]
    public DateTime EndDate { get; set; }

    [Id(2)]
    public string Granularity { get; set; } = string.Empty;

    [Id(3)]
    public List<string>? Measures { get; set; }

    [Id(4)]
    public Dictionary<string, object>? Filters { get; set; }
}
