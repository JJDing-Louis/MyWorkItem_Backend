using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MyWorkItem.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        GetRequiredGuid(principal, JwtRegisteredClaimNames.Sub);

    public static Guid GetAccountId(this ClaimsPrincipal principal) =>
        GetRequiredGuid(principal, "account_id");

    private static Guid GetRequiredGuid(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType)
            ?? throw new InvalidOperationException($"JWT 缺少 {claimType} Claim。");
        return Guid.Parse(value);
    }
}
