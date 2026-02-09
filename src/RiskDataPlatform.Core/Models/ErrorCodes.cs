namespace RiskDataPlatform.Core.Models;

public static class ErrorCodes
{
    public const string ExecutionNotFound = "EXECUTION_NOT_FOUND";
    public const string ExecutionAlreadyExists = "EXECUTION_ALREADY_EXISTS";
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string InvalidExecutionState = "INVALID_EXECUTION_STATE";
    public const string BookNotFound = "BOOK_NOT_FOUND";
    public const string DataNotFound = "DATA_NOT_FOUND";
    public const string StorageError = "STORAGE_ERROR";
    public const string QueryError = "QUERY_ERROR";
    public const string SerializationError = "SERIALIZATION_ERROR";
    public const string InternalError = "INTERNAL_ERROR";
    public const string TenantNotFound = "TENANT_NOT_FOUND";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
}
