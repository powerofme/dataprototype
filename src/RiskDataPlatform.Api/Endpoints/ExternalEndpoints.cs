using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Orleans;
using RiskDataPlatform.Api.Formatters;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Models;
using RiskDataPlatform.Grains.Interfaces;

namespace RiskDataPlatform.Api.Endpoints;

public static class ExternalEndpoints
{
    public static RouteGroupBuilder MapExternalEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/desks/{deskId}/reports/{reportType}/{date}/query", QueryExternalReportAsync)
            .WithName("QueryExternalReport")
            .WithOpenApi();

        group.MapGet("/desks/{deskId}/reports/{reportType}/available-dates", GetAvailableDatesAsync)
            .WithName("GetAvailableDates")
            .WithOpenApi();

        group.MapGet("/desks/{deskId}/reports/{reportType}/{date}/summary", GetDateSummaryAsync)
            .WithName("GetDateSummary")
            .WithOpenApi();

        return group;
    }

    private static async Task<IResult> QueryExternalReportAsync(
        string deskId,
        string reportType,
        string date,
        [FromBody] ReportQueryRequest request,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("QueryExternalReport");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("desk", deskId);
        activity?.SetTag("report_type", reportType);
        activity?.SetTag("date", date);

        if (!DateTime.TryParse(date, out var reportDate))
        {
            return Results.BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_ARGUMENT",
                Message = "Invalid date format",
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier
            });
        }

        var deskGrain = grainFactory.GetGrain<IDeskGrain>($"{tenantId}:{deskId}");
        var latestExecutions = await deskGrain.GetLatestExecutionsAsync();

        if (!latestExecutions.TryGetValue(reportType, out var execution))
        {
            return Results.NotFound(new ErrorResponse
            {
                ErrorCode = "NOT_FOUND",
                Message = $"No execution found for report type {reportType}",
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier
            });
        }

        var format = ResponseFormatNegotiator.Negotiate(context.Request);
        var dataGrain = grainFactory.GetGrain<IReportDataGrain>($"{tenantId}:{execution.ExecutionId}");
        var data = await dataGrain.QueryAsync(request, (RiskDataPlatform.Core.Models.ResponseFormat)(int)format);

        context.Response.ContentType = ResponseFormatNegotiator.GetContentType(format);
        await context.Response.Body.WriteAsync(data, cancellationToken);

        return Results.Empty;
    }

    private static async Task<IResult> GetAvailableDatesAsync(
        string deskId,
        string reportType,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetAvailableDates");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("desk", deskId);
        activity?.SetTag("report_type", reportType);

        var deskGrain = grainFactory.GetGrain<IDeskGrain>($"{tenantId}:{deskId}");
        var executions = await deskGrain.GetExecutionsAsync();

        var dates = executions
            .Where(e => e.ReportType == reportType && e.Status == ExecutionStatusEnum.Completed)
            .Select(e => e.AsOfDate.ToString("yyyy-MM-dd"))
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        return Results.Ok(dates);
    }

    private static async Task<IResult> GetDateSummaryAsync(
        string deskId,
        string reportType,
        string date,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetDateSummary");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("desk", deskId);
        activity?.SetTag("report_type", reportType);
        activity?.SetTag("date", date);

        if (!DateTime.TryParse(date, out var reportDate))
        {
            return Results.BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_ARGUMENT",
                Message = "Invalid date format",
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier
            });
        }

        var summary = new
        {
            deskId,
            reportType,
            date,
            totalRecords = 10000,
            lastUpdated = DateTime.UtcNow
        };

        return Results.Ok(summary);
    }
}
