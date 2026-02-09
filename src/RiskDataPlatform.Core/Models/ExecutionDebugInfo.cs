using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class ExecutionDebugInfo
{
    [Id(0)]
    public Guid ExecutionId { get; set; }

    [Id(1)]
    public string DeskId { get; set; } = string.Empty;

    [Id(2)]
    public List<string> Books { get; set; } = new();

    [Id(3)]
    public string ReportType { get; set; } = string.Empty;

    [Id(4)]
    public DateTime AsOfDate { get; set; }

    [Id(5)]
    public ExecutionStatusEnum Status { get; set; }

    [Id(6)]
    public DateTime CreatedAt { get; set; }

    [Id(7)]
    public DateTime? CompletedAt { get; set; }

    [Id(8)]
    public List<BookStatus> BookStatuses { get; set; } = new();

    [Id(9)]
    public Dictionary<string, string> Metadata { get; set; } = new();
}
