using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Orleans;
using RiskDataPlatform.Api.Formatters;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Models;
using RiskDataPlatform.Grains.Interfaces;

namespace RiskDataPlatform.Api.Endpoints;

public static class DataQueryEndpoints
{
    public static RouteGroupBuilder MapDataQueryEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/query", MultiExecutionQueryAsync)
            .WithName("MultiExecutionQuery")
            .WithOpenApi();

        group.MapPost("/{id}/aggregated", GetAggregatedDataAsync)
            .WithName("GetAggregatedData")
            .WithOpenApi();

        group.MapGet("/{id}/schema", GetSchemaAsync)
            .WithName("GetSchema")
            .WithOpenApi();

        group.MapPost("/{id}/export", ExportDataAsync)
            .WithName("ExportData")
            .WithOpenApi();

        group.MapGet("/{id}/measures", GetMeasuresAsync)
            .WithName("GetMeasures")
            .WithOpenApi();

        return group;
    }

    private static async Task<IResult> MultiExecutionQueryAsync(
        [FromBody] SsrmRequest request,
        [FromQuery] string? ids,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("MultiExecutionQuery");
        
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
        var format = ResponseFormatNegotiator.Negotiate(context.Request);

        // For now, return the first execution's data
        if (executionIds.Length > 0 && Guid.TryParse(executionIds[0], out var executionId))
        {
            var grain = grainFactory.GetGrain<IReportDataGrain>($"{tenantId}:{executionId}");
            var response = await grain.QuerySsrmAsync(request);
            return Results.Ok(response);
        }

        return Results.BadRequest(new ErrorResponse
        {
            ErrorCode = "INVALID_ARGUMENT",
            Message = "No valid execution IDs provided",
            TraceId = Activity.Current?.Id ?? context.TraceIdentifier
        });
    }

    private static async Task<IResult> GetAggregatedDataAsync(
        Guid id,
        [FromBody] ReportQueryRequest request,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetAggregatedData");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("execution_id", id);

        var format = ResponseFormatNegotiator.Negotiate(context.Request);
        var grain = grainFactory.GetGrain<IReportDataGrain>($"{tenantId}:{id}");
        
        var data = await grain.QueryAsync(request, (RiskDataPlatform.Core.Models.ResponseFormat)(int)format);

        context.Response.ContentType = ResponseFormatNegotiator.GetContentType(format);
        await context.Response.Body.WriteAsync(data, cancellationToken);
        
        return Results.Empty;
    }

    private static async Task<IResult> GetSchemaAsync(
        Guid id,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetSchema");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("execution_id", id);

        var grain = grainFactory.GetGrain<IReportDataGrain>($"{tenantId}:{id}");
        var schema = await grain.GetSchemaAsync();

        return Results.Ok(new { schema });
    }

    private static async Task<IResult> ExportDataAsync(
        Guid id,
        [FromBody] ReportQueryRequest request,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("ExportData");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("execution_id", id);

        var grain = grainFactory.GetGrain<IReportDataGrain>($"{tenantId}:{id}");
        var data = await grain.QueryAsync(request, RiskDataPlatform.Core.Models.ResponseFormat.ArrowIpc);

        context.Response.ContentType = "application/vnd.apache.arrow.stream";
        context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"export_{id}.arrow\"";
        await context.Response.Body.WriteAsync(data, cancellationToken);

        return Results.Empty;
    }

    private static async Task<IResult> GetMeasuresAsync(
        Guid id,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetMeasures");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("execution_id", id);

        // Return static measures for now
        var measures = new[]
        {
            new { name = "pv", displayName = "Present Value", aggregation = "sum" },
            new { name = "delta", displayName = "Delta", aggregation = "sum" },
            new { name = "gamma", displayName = "Gamma", aggregation = "sum" },
            new { name = "vega", displayName = "Vega", aggregation = "sum" }
        };

        return Results.Ok(measures);
    }
}
