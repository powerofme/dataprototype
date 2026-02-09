using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Orleans;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Models;
using RiskDataPlatform.Grains.Interfaces;

namespace RiskDataPlatform.Api.Endpoints;

public static class DebugInfoEndpoints
{
    public static RouteGroupBuilder MapDebugInfoEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{id}/debug-info", GetFullDebugInfoAsync)
            .WithName("GetFullDebugInfo")
            .WithOpenApi();

        group.MapGet("/{id}/debug-info/quick", GetQuickDebugInfoAsync)
            .WithName("GetQuickDebugInfo")
            .WithOpenApi();

        group.MapGet("/{id}/debug-info/market-data", GetMarketDataDebugInfoAsync)
            .WithName("GetMarketDataDebugInfo")
            .WithOpenApi();

        group.MapGet("/{id}/debug-info/runtime", GetRuntimeDebugInfoAsync)
            .WithName("GetRuntimeDebugInfo")
            .WithOpenApi();

        group.MapGet("/{id}/debug-info/grid", GetGridDebugInfoAsync)
            .WithName("GetGridDebugInfo")
            .WithOpenApi();

        group.MapGet("/{id}/debug-info/issues", GetIssuesDebugInfoAsync)
            .WithName("GetIssuesDebugInfo")
            .WithOpenApi();

        return group;
    }

    private static async Task<IResult> GetFullDebugInfoAsync(
        Guid id,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetFullDebugInfo");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("execution_id", id);

        var grain = grainFactory.GetGrain<IExecutionGrain>($"{tenantId}:{id}");
        var status = await grain.GetStatusAsync();

        var debugInfo = new ExecutionDebugInfo
        {
            ExecutionId = id,
            Status = status.OverallStatus,
            BookStatuses = status.BookStatuses
        };

        return Results.Ok(debugInfo);
    }

    private static async Task<IResult> GetQuickDebugInfoAsync(
        Guid id,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetQuickDebugInfo");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("execution_id", id);

        var grain = grainFactory.GetGrain<IExecutionGrain>($"{tenantId}:{id}");
        var status = await grain.GetStatusAsync();

        var quickInfo = new
        {
            executionId = id,
            status = status.OverallStatus.ToString(),
            bookCount = status.BookStatuses.Count
        };

        return Results.Ok(quickInfo);
    }

    private static async Task<IResult> GetMarketDataDebugInfoAsync(
        Guid id,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetMarketDataDebugInfo");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("execution_id", id);

        var marketDataInfo = new
        {
            executionId = id,
            marketDataSources = new[] { "Bloomberg", "Reuters" },
            marketDataCount = 1000,
            missingData = 0
        };

        return Results.Ok(marketDataInfo);
    }

    private static async Task<IResult> GetRuntimeDebugInfoAsync(
        Guid id,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetRuntimeDebugInfo");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("execution_id", id);

        var runtimeInfo = new
        {
            executionId = id,
            cpuUsage = 45.2,
            memoryUsageMb = 512,
            elapsedSeconds = 120
        };

        return Results.Ok(runtimeInfo);
    }

    private static async Task<IResult> GetGridDebugInfoAsync(
        Guid id,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetGridDebugInfo");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("execution_id", id);

        var gridInfo = new
        {
            executionId = id,
            totalRows = 10000,
            totalColumns = 50,
            cacheHitRate = 0.95
        };

        return Results.Ok(gridInfo);
    }

    private static async Task<IResult> GetIssuesDebugInfoAsync(
        Guid id,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetIssuesDebugInfo");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("execution_id", id);

        var grain = grainFactory.GetGrain<IExecutionGrain>($"{tenantId}:{id}");
        var status = await grain.GetStatusAsync();

        var issues = new
        {
            executionId = id,
            totalErrors = status.BookStatuses.Sum(b => b.ValuationErrorCount),
            errorsByType = new Dictionary<string, int>
            {
                ["ValuationError"] = 2,
                ["MarketDataMissing"] = 1
            }
        };

        return Results.Ok(issues);
    }
}
