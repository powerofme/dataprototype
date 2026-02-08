using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Orleans;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Models;
using RiskDataPlatform.Grains.Interfaces;

namespace RiskDataPlatform.Api.Endpoints;

public static class ValuationErrorEndpoints
{
    public static RouteGroupBuilder MapValuationErrorEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id}/valuation-errors", GetValuationErrorsAsync)
            .WithName("GetValuationErrors")
            .WithOpenApi();

        return group;
    }

    private static async Task<IResult> GetValuationErrorsAsync(
        Guid id,
        [FromBody] ReportQueryRequest? request,
        IGrainFactory grainFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("GetValuationErrors");
        
        var tenantId = context.Items["TenantId"]?.ToString() ?? "default";
        activity?.SetTag("tenant", tenantId);
        activity?.SetTag("execution_id", id);

        var grain = grainFactory.GetGrain<IExecutionGrain>($"{tenantId}:{id}");
        var status = await grain.GetStatusAsync();

        var errors = new List<ValuationError>();
        
        foreach (var bookStatus in status.BookStatuses)
        {
            if (bookStatus.ValuationErrorCount > 0)
            {
                errors.Add(new ValuationError
                {
                    TradeId = $"TRADE_{bookStatus.BookName}",
                    BookName = bookStatus.BookName,
                    ErrorCode = "VAL_001",
                    Message = "Sample valuation error",
                    Severity = ErrorSeverity.Error,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        return Results.Ok(errors);
    }
}
