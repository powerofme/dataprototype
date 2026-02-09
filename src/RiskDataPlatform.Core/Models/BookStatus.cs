using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class BookStatus
{
    [Id(0)]
    public string BookName { get; set; } = string.Empty;

    [Id(1)]
    public BookStatusEnum Status { get; set; }

    [Id(2)]
    public int ValuationErrorCount { get; set; }

    [Id(3)]
    public DateTime? StartTime { get; set; }

    [Id(4)]
    public DateTime? EndTime { get; set; }
}

public enum BookStatusEnum
{
    Pending,
    Running,
    Completed,
    Failed
}
