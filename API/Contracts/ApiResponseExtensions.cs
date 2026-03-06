using System.Linq;
using Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace API.Contracts
{
    //Map from Result<T> to ApiResponse<T>
    public static class ApiResponseExtensions
    {
        public static ApiResponse<T> ToApiResponse<T>(this Result<T> result, string traceId)
        {
            if (result.IsSuccess && result.Value is not null)
                return ApiResponse<T>.Ok(result.Value, traceId: traceId);

            var errors = result.Errors
                .Select(e => new ApiError(e.Code, e.Message, Detail: e.Details))
                .ToList();

            return ApiResponse<T>.Fail("Request failed", errors, traceId);
        }

        public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
        {
            var traceId = controller.HttpContext.TraceIdentifier;

            if (result.IsSuccess && result.Value is not null)
                return controller.Ok(ApiResponse<T>.Ok(result.Value, traceId: traceId));

            // map basic: validation -> 400, not found -> 404, others -> 400/500
            var hasNotFound = result.Errors.Any(e => e.Code == "NOT_FOUND");
            var status = hasNotFound ? 404 : 400;

            var errors = result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList();
            return controller.StatusCode(status, ApiResponse<T>.Fail("Request failed", errors, traceId));
        }
    }
}
