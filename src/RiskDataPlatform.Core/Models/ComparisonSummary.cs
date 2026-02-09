using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class ComparisonSummary
{
    [Id(0)]
    public Guid ComparisonId { get; set; }

    [Id(1)]
    public Guid BaseExecutionId { get; set; }

    [Id(2)]
    public Guid CompareExecutionId { get; set; }

    [Id(3)]
    public string DeskId { get; set; } = string.Empty;

    [Id(4)]
    public string ReportType { get; set; } = string.Empty;

    [Id(5)]
    public int TotalDifferences { get; set; }

    [Id(6)]
    public int SignificantDifferences { get; set; }

    [Id(7)]
    public Dictionary<string, int> DifferencesByBook { get; set; } = new();

    [Id(8)]
    public DateTime CreatedAt { get; set; }

    [Id(9)]
    public ComparisonStatusEnum Status { get; set; }
}

public enum ComparisonStatusEnum
{
    Pending,
    Running,
    Completed,
    Failed
}
