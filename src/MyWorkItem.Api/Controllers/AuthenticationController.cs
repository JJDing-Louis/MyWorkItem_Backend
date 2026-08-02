using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyWorkItem.Api.Infrastructure;
using MyWorkItem.Application.Abstractions;
using MyWorkItem.Application.Contracts;

namespace MyWorkItem.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Tags("Authentication")]
[EnableRateLimiting("auth")]
public sealed class AuthenticationController(
    IAuthenticationService authenticationService,
    IAntiforgery antiforgery,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append(CookieNames.CsrfToken, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = !environment.IsDevelopment() && Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
        return NoContent();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(request.LoginName, request.Password, cancellationToken);
        if (result is null)
        {
            return Unauthorized(new ProblemDetails { Status = 401, Title = "登入失敗", Detail = "帳號或密碼錯誤。" });
        }

        SetAuthenticationCookies(result);
        return Ok(result.User);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserResponse>> Refresh(CancellationToken cancellationToken)
    {
        var token = Request.Cookies[CookieNames.RefreshToken];
        var result = string.IsNullOrWhiteSpace(token)
            ? null
            : await authenticationService.RefreshAsync(token, cancellationToken);
        if (result is null)
        {
            ClearAuthenticationCookies();
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Refresh Token 無效或已失效" });
        }

        SetAuthenticationCookies(result);
        return Ok(result.User);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authenticationService.LogoutAsync(Request.Cookies[CookieNames.RefreshToken], cancellationToken);
        ClearAuthenticationCookies();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        var user = await authenticationService.GetCurrentUserAsync(
            User.GetAccountId(), User.GetUserId(), cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    private void SetAuthenticationCookies(AuthenticationResult result)
    {
        Response.Cookies.Append(CookieNames.AccessToken, result.AccessToken,
            CreateCookieOptions(result.AccessTokenExpiresAt));
        Response.Cookies.Append(CookieNames.RefreshToken, result.RefreshToken,
            CreateCookieOptions(result.RefreshTokenExpiresAt));
    }

    private CookieOptions CreateCookieOptions(DateTimeOffset expiresAt) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        Expires = expiresAt,
        Path = "/"
    };

    private void ClearAuthenticationCookies()
    {
        Response.Cookies.Delete(CookieNames.AccessToken, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(CookieNames.RefreshToken, new CookieOptions { Path = "/" });
    }
}
