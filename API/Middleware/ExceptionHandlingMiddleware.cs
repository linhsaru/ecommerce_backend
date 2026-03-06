using System.Linq;
using System.Net;
using API.Contracts;
using Application.Common;

namespace Api.Middlewares;

public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
        => _logger = logger;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", traceId);

            context.Response.ContentType = "application/json";

            var (status, response) = MapException(ex, traceId);

            context.Response.StatusCode = (int)status;
            await context.Response.WriteAsJsonAsync(response);
        }
    }

    private static (HttpStatusCode status, ApiResponse<object> response) MapException(Exception ex, string traceId)
    {
        return ex switch
        {
            NotFoundException nf => (HttpStatusCode.NotFound,
                ApiResponse<object>.Fail(nf.Message,
                    new() { new ApiError(nf.Code, nf.Message) }, traceId)),

            ValidationException ve => (HttpStatusCode.BadRequest,
                ApiResponse<object>.Fail(ve.Message,
                    ve.Failures.SelectMany(kvp => kvp.Value.Select(msg =>
                        new ApiError("FIELD_INVALID", msg, Field: kvp.Key)
                    )).ToList(),
                    traceId)),

            AppException ae => (HttpStatusCode.BadRequest,
                ApiResponse<object>.Fail(ae.Message,
                    new() { new ApiError(ae.Code, ae.Message) }, traceId)),

            _ => (HttpStatusCode.InternalServerError,
                ApiResponse<object>.Fail("Internal server error",
                    new() { new ApiError("INTERNAL_ERROR", "Unexpected error") }, traceId))
        };
    }
}
