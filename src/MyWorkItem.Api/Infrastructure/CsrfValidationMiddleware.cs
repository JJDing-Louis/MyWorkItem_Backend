using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MyWorkItem.Api.Infrastructure;

public sealed class CsrfValidationMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> UnsafeMethods =
        [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete];

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (!UnsafeMethods.Contains(context.Request.Method))
        {
            await next(context);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
            await next(context);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "CSRF 驗證失敗",
                Detail = "請先取得 CSRF Cookie，並在 X-CSRF-TOKEN Header 傳入相同 Token。",
                Instance = context.Request.Path
            };
            await JsonSerializer.SerializeAsync(context.Response.Body, problem, cancellationToken: context.RequestAborted);
        }
    }
}
