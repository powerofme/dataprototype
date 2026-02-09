using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Core.Constants;

public static class ErrorResponses
{
    public static ErrorResponse ExecutionNotFound(Guid executionId, string? traceId = null)
    {
        return new ErrorResponse
        {
            ErrorCode = ErrorCodes.ExecutionNotFound,
            Message = $"Execution {executionId} not found",
            TraceId = traceId,
            IsTransient = false
        };
    }

    public static ErrorResponse ExecutionAlreadyExists(Guid executionId, string? traceId = null)
    {
        return new ErrorResponse
        {
            ErrorCode = ErrorCodes.ExecutionAlreadyExists,
            Message = $"Execution {executionId} already exists",
            TraceId = traceId,
            IsTransient = false
        };
    }

    public static ErrorResponse InvalidRequest(string message, string? traceId = null)
    {
        return new ErrorResponse
        {
            ErrorCode = ErrorCodes.InvalidRequest,
            Message = message,
            TraceId = traceId,
            IsTransient = false
        };
    }

    public static ErrorResponse InvalidExecutionState(Guid executionId, string currentState, string? traceId = null)
    {
        return new ErrorResponse
        {
            ErrorCode = ErrorCodes.InvalidExecutionState,
            Message = $"Execution {executionId} is in invalid state: {currentState}",
            TraceId = traceId,
            IsTransient = false
        };
    }

    public static ErrorResponse DataNotFound(string message, string? traceId = null)
    {
        return new ErrorResponse
        {
            ErrorCode = ErrorCodes.DataNotFound,
            Message = message,
            TraceId = traceId,
            IsTransient = false
        };
    }

    public static ErrorResponse StorageError(string message, string? traceId = null, bool isTransient = true)
    {
        return new ErrorResponse
        {
            ErrorCode = ErrorCodes.StorageError,
            Message = message,
            TraceId = traceId,
            IsTransient = isTransient,
            RetryAfterSeconds = isTransient ? 5 : null
        };
    }

    public static ErrorResponse QueryError(string message, string? traceId = null)
    {
        return new ErrorResponse
        {
            ErrorCode = ErrorCodes.QueryError,
            Message = message,
            TraceId = traceId,
            IsTransient = false
        };
    }

    public static ErrorResponse InternalError(string message, string? traceId = null)
    {
        return new ErrorResponse
        {
            ErrorCode = ErrorCodes.InternalError,
            Message = message,
            TraceId = traceId,
            IsTransient = true,
            RetryAfterSeconds = 5
        };
    }

    public static ErrorResponse TenantNotFound(string tenantId, string? traceId = null)
    {
        return new ErrorResponse
        {
            ErrorCode = ErrorCodes.TenantNotFound,
            Message = $"Tenant {tenantId} not found",
            TraceId = traceId,
            IsTransient = false
        };
    }

    public static ErrorResponse Unauthorized(string message, string? traceId = null)
    {
        return new ErrorResponse
        {
            ErrorCode = ErrorCodes.Unauthorized,
            Message = message,
            TraceId = traceId,
            IsTransient = false
        };
    }

    public static ErrorResponse RateLimitExceeded(int retryAfterSeconds, string? traceId = null)
    {
        return new ErrorResponse
        {
            ErrorCode = ErrorCodes.RateLimitExceeded,
            Message = "Rate limit exceeded",
            TraceId = traceId,
            IsTransient = true,
            RetryAfterSeconds = retryAfterSeconds
        };
    }

    public static ErrorResponse ServiceUnavailable(string? message = null, string? traceId = null)
    {
        return new ErrorResponse
        {
            ErrorCode = ErrorCodes.ServiceUnavailable,
            Message = message ?? "Service temporarily unavailable",
            TraceId = traceId,
            IsTransient = true,
            RetryAfterSeconds = 30
        };
    }
}
