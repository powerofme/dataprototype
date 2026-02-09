using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Orleans;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Models;
using RiskDataPlatform.Grains.Interfaces;

namespace RiskDataPlatform.Api.Endpoints;

public static class IncrementalEndpoints
{
    public static RouteGroupBuilder MapIncrementalEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{deskId}/incremental/start", StartIncrementalAsync)
            .WithName("StartIncremental")
            .WithOpenApi();

        group.MapPost("/{deskId}/incremental/stop", StopIncrementalAsync)
            .WithName("StopIncremental")
            .WithOpenApi();

        group.MapGet("/{deskId}/incremental/status", GetIncrementalStatusAsync)
            .WithName("GetIncrementalStatus")
            .WithOpenApi();

        return group;
    }

    private static async Task<IResult> StartIncrementalAsync(
        string deskId,
        [FromQuery] string reportType,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("StartIncremental");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("desk", deskId);
        activity?.SetTag("report_type", reportType);

        var grain = grainFactory.GetGrain<IDeskGrain>($"{tenantId}:{deskId}");
        await grain.StartIncrementalAsync(reportType);

        return Results.Ok(new { status = "started", deskId, reportType });
    }

    private static async Task<IResult> StopIncrementalAsync(
        string deskId,
        [FromQuery] string reportType,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("StopIncremental");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("desk", deskId);
        activity?.SetTag("report_type", reportType);

        var grain = grainFactory.GetGrain<IDeskGrain>($"{tenantId}:{deskId}");
        await grain.StopIncrementalAsync(reportType);

        return Results.Ok(new { status = "stopped", deskId, reportType });
    }

    private static async Task<IResult> GetIncrementalStatusAsync(
        string deskId,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetIncrementalStatus");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("desk", deskId);

        var grain = grainFactory.GetGrain<IDeskGrain>($"{tenantId}:{deskId}");
        var statuses = await grain.GetIncrementalStatusAsync();

        return Results.Ok(statuses);
    }
}
