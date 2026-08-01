using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWorkItem.Application;

namespace MyWorkItem.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService, IAuthRepository repository, IAntiforgery antiforgery, IWebHostEnvironment environment) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append(CookieNames.CsrfToken, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        });
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthSessionResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await authService.LoginAsync(request, cancellationToken);
        WriteCookies(session.Tokens);
        return Ok(session.Response);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthSessionResponse>> Refresh(CancellationToken cancellationToken)
    {
        var rawToken = Request.Cookies[CookieNames.RefreshToken] ?? throw new UnauthorizedException("缺少 Refresh Token。");
        var session = await authService.RefreshAsync(rawToken, cancellationToken);
        WriteCookies(session.Tokens);
        return Ok(session.Response);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var rawToken = Request.Cookies[CookieNames.RefreshToken];
        if (!string.IsNullOrWhiteSpace(rawToken))
        {
            await authService.LogoutAsync(rawToken, cancellationToken);
        }

        DeleteCookies();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        var profile = await repository.GetAccessProfileByAccountIdAsync(User.GetAccountId(), cancellationToken)
            ?? throw new NotFoundException("找不到目前使用者。");
        return Ok(new CurrentUserResponse(profile.AccountId, profile.UserId, profile.UserName, profile.Name, profile.Roles, profile.Permissions));
    }

    private void WriteCookies(TokenPair pair)
    {
        Response.Cookies.Append(CookieNames.AccessToken, pair.AccessToken, CreateCookieOptions(pair.AccessTokenExpiresAt, "/"));
        Response.Cookies.Append(CookieNames.RefreshToken, pair.RefreshToken, CreateCookieOptions(pair.RefreshTokenExpiresAt, "/api/v1/auth"));
    }

    private CookieOptions CreateCookieOptions(DateTimeOffset expires, string path) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Expires = expires,
        Path = path
    };

    private void DeleteCookies()
    {
        Response.Cookies.Delete(CookieNames.AccessToken, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(CookieNames.RefreshToken, new CookieOptions { Path = "/api/v1/auth" });
        Response.Cookies.Delete(CookieNames.CsrfToken, new CookieOptions { Path = "/" });
    }
}
