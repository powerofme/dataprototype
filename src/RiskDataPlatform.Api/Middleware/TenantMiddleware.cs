using Orleans.Runtime;

namespace RiskDataPlatform.Api.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string DefaultTenant = "default";

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.Request.Headers[TenantHeaderName].FirstOrDefault() ?? DefaultTenant;
        
        RequestContext.Set("TenantId", tenantId);
        context.Items["TenantId"] = tenantId;

        await _next(context);
    }
}
