using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class ReplaySession
{
    [Id(0)]
    public Guid SessionId { get; set; }

    [Id(1)]
    public string Name { get; set; } = string.Empty;

    [Id(2)]
    public DateTime StartTime { get; set; }

    [Id(3)]
    public DateTime? EndTime { get; set; }

    [Id(4)]
    public ReplayStatusEnum Status { get; set; }

    [Id(5)]
    public Dictionary<string, object>? Configuration { get; set; }
}

[GenerateSerializer]
public sealed class ReplayEvent
{
    [Id(0)]
    public Guid EventId { get; set; }

    [Id(1)]
    public Guid SessionId { get; set; }

    [Id(2)]
    public DateTime Timestamp { get; set; }

    [Id(3)]
    public string EventType { get; set; } = string.Empty;

    [Id(4)]
    public Dictionary<string, object>? Payload { get; set; }
}

public enum ReplayStatusEnum
{
    Created,
    Running,
    Paused,
    Completed,
    Failed
}
