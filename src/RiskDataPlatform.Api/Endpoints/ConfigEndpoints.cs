using System.Diagnostics;
using RiskDataPlatform.Core.Diagnostics;

namespace RiskDataPlatform.Api.Endpoints;

public static class ConfigEndpoints
{
    public static RouteGroupBuilder MapConfigEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/desks", GetDesksAsync)
            .WithName("GetDesks")
            .WithOpenApi();

        group.MapGet("/available-measures", GetAvailableMeasuresAsync)
            .WithName("GetAvailableMeasures")
            .WithOpenApi();

        return group;
    }

    private static Task<IResult> GetDesksAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetDesks");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);

        var desks = new[]
        {
            new { id = "DESK_A", name = "Desk A", region = "NY" },
            new { id = "DESK_B", name = "Desk B", region = "LON" },
            new { id = "DESK_C", name = "Desk C", region = "TKY" }
        };

        return Task.FromResult(Results.Ok(desks));
    }

    private static Task<IResult> GetAvailableMeasuresAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetAvailableMeasures");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);

        var measures = new[]
        {
            new { name = "pv", displayName = "Present Value", type = "currency", aggregation = "sum" },
            new { name = "delta", displayName = "Delta", type = "numeric", aggregation = "sum" },
            new { name = "gamma", displayName = "Gamma", type = "numeric", aggregation = "sum" },
            new { name = "vega", displayName = "Vega", type = "numeric", aggregation = "sum" },
            new { name = "theta", displayName = "Theta", type = "numeric", aggregation = "sum" },
            new { name = "rho", displayName = "Rho", type = "numeric", aggregation = "sum" }
        };

        return Task.FromResult(Results.Ok(measures));
    }
}
