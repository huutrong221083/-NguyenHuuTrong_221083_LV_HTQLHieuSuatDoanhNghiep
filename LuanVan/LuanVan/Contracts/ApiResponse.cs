using System.Diagnostics;

namespace LuanVan.Contracts;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }
    public string TraceId { get; set; } = string.Empty;

    public static ApiResponse<T> Ok(T? data, string message = "Success", string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            ErrorCode = null,
            TraceId = ResolveTraceId(traceId)
        };
    }

    public static ApiResponse<T> Fail(string message, string errorCode = "BUSINESS_ERROR", string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Data = default,
            ErrorCode = errorCode,
            TraceId = ResolveTraceId(traceId)
        };
    }

    private static string ResolveTraceId(string? traceId)
    {
        if (!string.IsNullOrWhiteSpace(traceId))
        {
            return traceId;
        }

        return Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
    }
}
