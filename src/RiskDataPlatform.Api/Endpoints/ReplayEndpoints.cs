using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Api.Endpoints;

public static class ReplayEndpoints
{
    public static RouteGroupBuilder MapReplayEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/sessions", CreateReplaySessionAsync)
            .WithName("CreateReplaySession")
            .WithOpenApi();

        group.MapGet("/sessions/{sessionId}", GetReplaySessionAsync)
            .WithName("GetReplaySession")
            .WithOpenApi();

        group.MapPut("/sessions/{sessionId}/control", ControlReplaySessionAsync)
            .WithName("ControlReplaySession")
            .WithOpenApi();

        group.MapDelete("/sessions/{sessionId}", DeleteReplaySessionAsync)
            .WithName("DeleteReplaySession")
            .WithOpenApi();

        group.MapGet("/available-dates", GetReplayAvailableDatesAsync)
            .WithName("GetReplayAvailableDates")
            .WithOpenApi();

        return group;
    }

    private static Task<IResult> CreateReplaySessionAsync(
        [FromBody] object request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("CreateReplaySession");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);

        return Task.FromResult(Results.StatusCode(501));
    }

    private static Task<IResult> GetReplaySessionAsync(
        Guid sessionId,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetReplaySession");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("session_id", sessionId);

        return Task.FromResult(Results.StatusCode(501));
    }

    private static Task<IResult> ControlReplaySessionAsync(
        Guid sessionId,
        [FromBody] object request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("ControlReplaySession");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("session_id", sessionId);

        return Task.FromResult(Results.StatusCode(501));
    }

    private static Task<IResult> DeleteReplaySessionAsync(
        Guid sessionId,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("DeleteReplaySession");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("session_id", sessionId);

        return Task.FromResult(Results.StatusCode(501));
    }

    private static Task<IResult> GetReplayAvailableDatesAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetReplayAvailableDates");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);

        return Task.FromResult(Results.StatusCode(501));
    }
}
