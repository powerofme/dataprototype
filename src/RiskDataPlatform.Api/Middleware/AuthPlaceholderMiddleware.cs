namespace RiskDataPlatform.Api.Middleware;

public class AuthPlaceholderMiddleware
{
    private readonly RequestDelegate _next;

    public AuthPlaceholderMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
    }
}
