using System.Diagnostics;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Api.Endpoints;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/live", GetLivenessAsync)
            .WithName("GetLiveness")
            .WithOpenApi();

        group.MapGet("/status", GetHealthStatusAsync)
            .WithName("GetHealthStatus")
            .WithOpenApi();

        return group;
    }

    private static Task<IResult> GetLivenessAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetLiveness");
        
        return Task.FromResult(Results.Ok(new { status = "ok" }));
    }

    private static Task<IResult> GetHealthStatusAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetHealthStatus");

        var healthStatus = new SystemHealthStatus
        {
            Status = HealthStatusEnum.Healthy,
            Components = new Dictionary<string, ComponentHealth>
            {
                ["API"] = new ComponentHealth { Name = "API", Status = HealthStatusEnum.Healthy, Message = "Healthy" },
                ["Orleans"] = new ComponentHealth { Name = "Orleans", Status = HealthStatusEnum.Healthy, Message = "Healthy" },
                ["Database"] = new ComponentHealth { Name = "Database", Status = HealthStatusEnum.Healthy, Message = "Healthy" },
                ["S3Storage"] = new ComponentHealth { Name = "S3Storage", Status = HealthStatusEnum.Healthy, Message = "Healthy" }
            },
            Timestamp = DateTime.UtcNow
        };

        return Task.FromResult(Results.Ok(healthStatus));
    }
}
