using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyWorkItem.Application.Exceptions;

namespace MyWorkItem.Api.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            RequestValidationException => (StatusCodes.Status400BadRequest, "請求資料不正確"),
            NotFoundException => (StatusCodes.Status404NotFound, "找不到資源"),
            ConflictException => (StatusCodes.Status409Conflict, "資料衝突"),
            _ => (StatusCodes.Status500InternalServerError, "伺服器發生未預期錯誤")
        };
        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "處理 API 請求時發生未預期錯誤。TraceId: {TraceId}", context.TraceIdentifier);
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == 500 ? null : exception.Message,
            Instance = context.Request.Path,
            Extensions = { ["traceId"] = context.TraceIdentifier }
        }, cancellationToken);
        return true;
    }
}
