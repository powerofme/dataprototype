using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class ErrorResponse
{
    [Id(0)]
    public string ErrorCode { get; set; } = string.Empty;

    [Id(1)]
    public string Message { get; set; } = string.Empty;

    [Id(2)]
    public string? TraceId { get; set; }

    [Id(3)]
    public bool IsTransient { get; set; }

    [Id(4)]
    public int? RetryAfterSeconds { get; set; }

    [Id(5)]
    public Dictionary<string, object>? Details { get; set; }
}
