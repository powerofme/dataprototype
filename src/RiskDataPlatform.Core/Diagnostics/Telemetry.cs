using System.Diagnostics;

namespace RiskDataPlatform.Core.Diagnostics;

public static class Telemetry
{
    public const string ServiceName = "RiskDataPlatform";

    public static readonly ActivitySource Api = new(
        $"{ServiceName}.Api",
        "1.0.0");

    public static readonly ActivitySource Grains = new(
        $"{ServiceName}.Grains",
        "1.0.0");

    public static readonly ActivitySource Query = new(
        $"{ServiceName}.Query",
        "1.0.0");

    public static readonly ActivitySource Arrow = new(
        $"{ServiceName}.Arrow",
        "1.0.0");

    public static readonly ActivitySource Storage = new(
        $"{ServiceName}.Storage",
        "1.0.0");
}
