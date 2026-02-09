using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Orleans;
using RiskDataPlatform.Api.Formatters;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Models;
using RiskDataPlatform.Grains.Interfaces;

namespace RiskDataPlatform.Api.Endpoints;

public static class ComparisonEndpoints
{
    public static RouteGroupBuilder MapComparisonEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/compare", CreateComparisonAsync)
            .WithName("CreateComparison")
            .WithOpenApi();

        group.MapPost("/{id}/query", QueryComparisonAsync)
            .WithName("QueryComparison")
            .WithOpenApi();

        group.MapGet("/{id}/summary", GetComparisonSummaryAsync)
            .WithName("GetComparisonSummary")
            .WithOpenApi();

        group.MapGet("/{id}/market-data-diff", GetMarketDataDiffAsync)
            .WithName("GetMarketDataDiff")
            .WithOpenApi();

        return group;
    }

    private static async Task<IResult> CreateComparisonAsync(
        [FromBody] ComparisonRequest request,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("CreateComparison");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("base_execution", request.BaseExecutionId);
        activity?.SetTag("compare_execution", request.CompareExecutionId);

        var comparisonId = Guid.NewGuid();
        var grain = grainFactory.GetGrain<IComparisonGrain>($"{tenantId}:{comparisonId}");
        var summary = await grain.CreateComparisonAsync(request);

        return Results.Created($"/api/v1/compare/{comparisonId}", summary);
    }

    private static async Task<IResult> QueryComparisonAsync(
        Guid id,
        [FromBody] ReportQueryRequest request,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("QueryComparison");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("comparison_id", id);

        var format = ResponseFormatNegotiator.Negotiate(context.Request);
        var grain = grainFactory.GetGrain<IComparisonGrain>($"{tenantId}:{id}");
        
        var data = await grain.QueryAsync(request, (RiskDataPlatform.Core.Models.ResponseFormat)(int)format);

        context.Response.ContentType = ResponseFormatNegotiator.GetContentType(format);
        await context.Response.Body.WriteAsync(data, cancellationToken);

        return Results.Empty;
    }

    private static async Task<IResult> GetComparisonSummaryAsync(
        Guid id,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetComparisonSummary");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("comparison_id", id);

        var grain = grainFactory.GetGrain<IComparisonGrain>($"{tenantId}:{id}");
        var summary = await grain.GetSummaryAsync();

        return Results.Ok(summary);
    }

    private static async Task<IResult> GetMarketDataDiffAsync(
        Guid id,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetMarketDataDiff");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("comparison_id", id);

        var grain = grainFactory.GetGrain<IComparisonGrain>($"{tenantId}:{id}");
        var diff = await grain.GetMarketDataDiffAsync();

        return Results.Ok(diff);
    }
}
