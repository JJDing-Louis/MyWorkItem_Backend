using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Antiforgery;
using MyWorkItem.Application;

namespace MyWorkItem.Api;

public static class CookieNames
{
    public const string AccessToken = "mwi_access";
    public const string RefreshToken = "mwi_refresh";
    public const string CsrfToken = "XSRF-TOKEN";
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetAccountId(this ClaimsPrincipal principal) => ParseRequired(principal.FindFirstValue(ClaimTypes.NameIdentifier), "AccountId");
    public static Guid GetUserId(this ClaimsPrincipal principal) => ParseRequired(principal.FindFirstValue("user_id"), "UserId");

    private static Guid ParseRequired(string? value, string name) =>
        Guid.TryParse(value, out var id) ? id : throw new UnauthorizedException($"JWT 缺少有效的 {name}。");
}

public sealed class CsrfValidationFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Trace
    };

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (SafeMethods.Contains(context.HttpContext.Request.Method))
        {
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "CSRF 驗證失敗",
                Detail = "缺少或無效的 CSRF Token。",
                Instance = context.HttpContext.Request.Path
            });
        }
    }
}

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "處理 {Method} {Path} 時發生未處理例外。", httpContext.Request.Method, httpContext.Request.Path);
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "請求驗證失敗"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "驗證失敗"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "沒有操作權限"),
            NotFoundException => (StatusCodes.Status404NotFound, "找不到資源"),
            ConflictException => (StatusCodes.Status409Conflict, "資料衝突"),
            _ => (StatusCodes.Status500InternalServerError, "伺服器發生未預期錯誤")
        };
        httpContext.Response.StatusCode = status;
        var detail = status == StatusCodes.Status500InternalServerError ? "請稍後再試。" : exception.Message;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            },
            Exception = exception
        });
    }
}
