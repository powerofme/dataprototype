using System.Diagnostics;
using RiskDataPlatform.Core.Models;
using RiskDataPlatform.Core.Diagnostics;

namespace RiskDataPlatform.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            using var activity = Telemetry.Api.StartActivity("ErrorHandling");
            activity?.SetTag("error", true);
            activity?.SetTag("exception.type", ex.GetType().Name);
            
            _logger.LogError(ex, "Unhandled exception in request {Path}", context.Request.Path);

            context.Response.StatusCode = ex switch
            {
                ArgumentException => 400,
                UnauthorizedAccessException => 401,
                KeyNotFoundException => 404,
                InvalidOperationException => 409,
                TimeoutException => 504,
                _ => 500
            };

            context.Response.ContentType = "application/json";
            
            var errorResponse = new ErrorResponse
            {
                ErrorCode = ex switch
                {
                    ArgumentException => "INVALID_ARGUMENT",
                    UnauthorizedAccessException => "UNAUTHORIZED",
                    KeyNotFoundException => "NOT_FOUND",
                    InvalidOperationException => "INVALID_OPERATION",
                    TimeoutException => "TIMEOUT",
                    _ => "INTERNAL_ERROR"
                },
                Message = ex.Message,
                TraceId = Activity.Current?.Id ?? context.TraceIdentifier,
                IsTransient = ex is TimeoutException or TaskCanceledException,
                Details = new Dictionary<string, object>
                {
                    ["ExceptionType"] = ex.GetType().Name
                }
            };

            await context.Response.WriteAsJsonAsync(errorResponse);
        }
    }
}
