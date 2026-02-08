using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RiskDataPlatform.Core.Diagnostics;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/tenants", CreateTenantAsync)
            .WithName("CreateTenant")
            .WithOpenApi();

        group.MapGet("/tenants", ListTenantsAsync)
            .WithName("ListTenants")
            .WithOpenApi();

        group.MapDelete("/tenants/{tenantId}", DeleteTenantAsync)
            .WithName("DeleteTenant")
            .WithOpenApi();

        return group;
    }

    private static Task<IResult> CreateTenantAsync(
        [FromBody] object request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("CreateTenant");
        
        var tenantId = Guid.NewGuid().ToString();
        var tenant = new
        {
            id = tenantId,
            name = "New Tenant",
            createdAt = DateTime.UtcNow
        };

        return Task.FromResult(Results.Created($"/api/v1/admin/tenants/{tenantId}", tenant));
    }

    private static Task<IResult> ListTenantsAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("ListTenants");

        var tenants = new[]
        {
            new { id = "default", name = "Default Tenant", createdAt = DateTime.UtcNow.AddDays(-30) },
            new { id = "tenant1", name = "Tenant 1", createdAt = DateTime.UtcNow.AddDays(-15) }
        };

        return Task.FromResult(Results.Ok(tenants));
    }

    private static Task<IResult> DeleteTenantAsync(
        string tenantId,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Api.StartActivity("DeleteTenant");
        activity?.SetTag("tenant_id", tenantId);

        return Task.FromResult(Results.NoContent());
    }
}
