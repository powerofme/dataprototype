namespace RiskDataPlatform.Api.Middleware;

public class EventJournalMiddleware
{
    private readonly RequestDelegate _next;

    public EventJournalMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
    }
}
