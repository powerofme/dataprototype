using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class SsrmResponse
{
    [Id(0)]
    public List<object> Rows { get; set; } = new();

    [Id(1)]
    public int? LastRow { get; set; }

    [Id(2)]
    public Dictionary<string, object>? SecondaryColumnFields { get; set; }
}
