using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Orleans;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Models;
using RiskDataPlatform.Grains.Interfaces;

namespace RiskDataPlatform.Api.Endpoints;

public static class ExecutionEndpoints
{
    public static RouteGroupBuilder MapExecutionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateExecutionAsync)
            .WithName("CreateExecution")
            .WithOpenApi();

        group.MapGet("/", GetExecutionsAsync)
            .WithName("GetExecutions")
            .WithOpenApi();

        group.MapDelete("/{id}", CancelExecutionAsync)
            .WithName("CancelExecution")
            .WithOpenApi();

        return group;
    }

    public static RouteGroupBuilder MapDeskExecutionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/desks/{deskId}/executions", GetDeskExecutionsAsync)
            .WithName("GetDeskExecutions")
            .WithOpenApi();

        group.MapGet("/desks/{deskId}/executions/latest", GetLatestDeskExecutionAsync)
            .WithName("GetLatestDeskExecution")
            .WithOpenApi();

        return group;
    }

    private static async Task<IResult> CreateExecutionAsync(
        [FromBody] Execution execution,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("CreateExecution");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("desk", execution.DeskId);

        execution.ExecutionId = Guid.NewGuid();
        execution.CreatedAt = DateTime.UtcNow;
        execution.Status = ExecutionStatusEnum.Pending;

        var grain = grainFactory.GetGrain<IExecutionGrain>($"{tenantId}:{execution.ExecutionId}");
        var result = await grain.CreateAsync(execution);

        return Results.Created($"/api/v1/executions/{result.ExecutionId}", result);
    }

    private static async Task<IResult> GetExecutionsAsync(
        [FromQuery] string? ids,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetExecutions");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);

        if (string.IsNullOrWhiteSpace(ids))
        {
            return Results.BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_ARGUMENT",
                Message = "ids query parameter is required",
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier
            });
        }

        var executionIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var results = new List<ExecutionStatus>();

        foreach (var id in executionIds)
        {
            if (Guid.TryParse(id, out var executionId))
            {
                var grain = grainFactory.GetGrain<IExecutionGrain>($"{tenantId}:{executionId}");
                var status = await grain.GetStatusAsync();
                results.Add(status);
            }
        }

        return Results.Ok(results);
    }

    private static async Task<IResult> CancelExecutionAsync(
        Guid id,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("CancelExecution");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("execution_id", id);

        var grain = grainFactory.GetGrain<IExecutionGrain>($"{tenantId}:{id}");
        await grain.CancelAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> GetDeskExecutionsAsync(
        string deskId,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetDeskExecutions");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("desk", deskId);

        var deskGrain = grainFactory.GetGrain<IDeskGrain>($"{tenantId}:{deskId}");
        var executions = await deskGrain.GetExecutionsAsync();

        return Results.Ok(executions);
    }

    private static async Task<IResult> GetLatestDeskExecutionAsync(
        string deskId,
        [FromQuery] string? reportType,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetLatestDeskExecution");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("desk", deskId);

        var deskGrain = grainFactory.GetGrain<IDeskGrain>($"{tenantId}:{deskId}");
        var latestExecutions = await deskGrain.GetLatestExecutionsAsync();

        Execution? execution = null;
        if (!string.IsNullOrWhiteSpace(reportType))
        {
            latestExecutions.TryGetValue(reportType, out execution);
        }
        else if (latestExecutions.Any())
        {
            execution = latestExecutions.Values.FirstOrDefault();
        }

        if (execution == null)
        {
            return Results.NotFound(new ErrorResponse
            {
                ErrorCode = "NOT_FOUND",
                Message = $"No executions found for desk {deskId}",
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier
            });
        }

        return Results.Ok(execution);
    }
}
