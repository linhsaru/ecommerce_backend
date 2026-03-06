using System;
using System.Collections.Generic;

namespace API.Contracts
{
    public sealed class ApiResponse<T>
    {
        public bool Success { get; init; }

        public string? Message { get; init; }

        public T? Data { get; init; }
        public string TraceId { get; init; } = "";

        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

        public List<ApiError>? Errors { get; init; }

        public static ApiResponse<T> Ok(T data, string? message = null, string? traceId = null) => new()
        {
            Success = true,
            Data = data,
            Message = message,
            TraceId = traceId ?? "",
        };

        public static ApiResponse<T> Fail(string message, List<ApiError> errors = null, string? traceId = null) => new()
        {
            Success = false,
            Message = message,
            Errors = errors ?? new List<ApiError>(),
            TraceId = traceId ?? "",
        };

    }
    public sealed record ApiError(string Code, string Message, string? Field = null, string? Detail = null);
}
