namespace RiskDataPlatform.Api.Formatters;

public enum ResponseFormat
{
    ArrowIpc,
    Json,
    ColumnarJson
}

public class ResponseFormatNegotiator
{
    public static ResponseFormat Negotiate(HttpRequest request)
    {
        // Check for explicit format parameter in body (for POST requests)
        if (request.HasJsonContentType() && request.Body.CanSeek)
        {
            // This would be set by endpoint logic after reading the body
            if (request.HttpContext.Items.TryGetValue("RequestedFormat", out var format) && format is string formatStr)
            {
                return formatStr.ToLowerInvariant() switch
                {
                    "json" => ResponseFormat.Json,
                    "columnar_json" or "columnarjson" => ResponseFormat.ColumnarJson,
                    "arrow" or "arrow_ipc" => ResponseFormat.ArrowIpc,
                    _ => ResponseFormat.ArrowIpc
                };
            }
        }

        // Check Accept header
        var acceptHeader = request.Headers.Accept.ToString();
        
        if (acceptHeader.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return ResponseFormat.Json;
        }
        
        if (acceptHeader.Contains("application/vnd.apache.arrow.stream", StringComparison.OrdinalIgnoreCase))
        {
            return ResponseFormat.ArrowIpc;
        }

        // Default to Arrow IPC
        return ResponseFormat.ArrowIpc;
    }

    public static string GetContentType(ResponseFormat format)
    {
        return format switch
        {
            ResponseFormat.ArrowIpc => "application/vnd.apache.arrow.stream",
            ResponseFormat.Json => "application/json",
            ResponseFormat.ColumnarJson => "application/json",
            _ => "application/vnd.apache.arrow.stream"
        };
    }
}
