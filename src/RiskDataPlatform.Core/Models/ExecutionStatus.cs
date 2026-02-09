using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class ExecutionStatus
{
    [Id(0)]
    public List<BookStatus> BookStatuses { get; set; } = new();

    [Id(1)]
    public ExecutionStatusEnum OverallStatus { get; set; }
}

public enum ExecutionStatusEnum
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
