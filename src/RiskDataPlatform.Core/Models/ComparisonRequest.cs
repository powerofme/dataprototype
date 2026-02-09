using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class ComparisonRequest
{
    [Id(0)]
    public Guid BaseExecutionId { get; set; }

    [Id(1)]
    public Guid CompareExecutionId { get; set; }

    [Id(2)]
    public string DeskId { get; set; } = string.Empty;

    [Id(3)]
    public string ReportType { get; set; } = string.Empty;

    [Id(4)]
    public List<string>? Books { get; set; }

    [Id(5)]
    public double? Threshold { get; set; }
}
