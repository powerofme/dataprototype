using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class TenantConfiguration
{
    [Id(0)]
    public string TenantId { get; set; } = string.Empty;

    [Id(1)]
    public string TenantName { get; set; } = string.Empty;

    [Id(2)]
    public bool IsActive { get; set; }

    [Id(3)]
    public string StorageBucket { get; set; } = string.Empty;

    [Id(4)]
    public string StoragePrefix { get; set; } = string.Empty;

    [Id(5)]
    public Dictionary<string, string> Settings { get; set; } = new();

    [Id(6)]
    public List<string> AllowedReportTypes { get; set; } = new();

    [Id(7)]
    public int MaxConcurrentExecutions { get; set; }

    [Id(8)]
    public DateTime CreatedAt { get; set; }

    [Id(9)]
    public DateTime? UpdatedAt { get; set; }
}
