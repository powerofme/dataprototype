using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class ValuationError
{
    [Id(0)]
    public string TradeId { get; set; } = string.Empty;

    [Id(1)]
    public string BookName { get; set; } = string.Empty;

    [Id(2)]
    public string ErrorCode { get; set; } = string.Empty;

    [Id(3)]
    public string Message { get; set; } = string.Empty;

    [Id(4)]
    public ErrorSeverity Severity { get; set; }

    [Id(5)]
    public DateTime Timestamp { get; set; }
}

public enum ErrorSeverity
{
    Warning,
    Error,
    Critical
}
