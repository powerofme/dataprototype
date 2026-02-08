using System.Text.Json;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Api.Middleware;

public class RequestSanitizerMiddleware
{
    private readonly RequestDelegate _next;
    private const int MaxExecutionsPerDesk = 1;
    private const int MaxBooksPerExecution = 100;
    private const int MaxRowsPerQuery = 100000;

    public RequestSanitizerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method == "POST" && context.Request.Path.StartsWithSegments("/api/v1/executions"))
        {
            context.Request.EnableBuffering();
            
            try
            {
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                context.Request.Body.Position = 0;

                if (!string.IsNullOrWhiteSpace(body))
                {
                    var execution = JsonSerializer.Deserialize<Execution>(body, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                    if (execution != null)
                    {
                        if (execution.Books.Count > MaxBooksPerExecution)
                        {
                            context.Response.StatusCode = 400;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsJsonAsync(new ErrorResponse
                            {
                                ErrorCode = "VALIDATION_ERROR",
                                Message = $"Maximum {MaxBooksPerExecution} books allowed per execution",
                                TraceId = context.TraceIdentifier
                            });
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Let it pass through for proper validation in the endpoint
            }
        }

        await _next(context);
    }
}
