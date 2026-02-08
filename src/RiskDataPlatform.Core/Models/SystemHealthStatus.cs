using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class SystemHealthStatus
{
    [Id(0)]
    public HealthStatusEnum Status { get; set; }

    [Id(1)]
    public Dictionary<string, ComponentHealth> Components { get; set; } = new();

    [Id(2)]
    public DateTime Timestamp { get; set; }

    [Id(3)]
    public string? Message { get; set; }
}

[GenerateSerializer]
public sealed class ComponentHealth
{
    [Id(0)]
    public string Name { get; set; } = string.Empty;

    [Id(1)]
    public HealthStatusEnum Status { get; set; }

    [Id(2)]
    public string? Message { get; set; }

    [Id(3)]
    public Dictionary<string, object>? Metrics { get; set; }
}

public enum HealthStatusEnum
{
    Healthy,
    Degraded,
    Unhealthy
}
