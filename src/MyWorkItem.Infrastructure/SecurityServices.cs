using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using MyWorkItem.Application;
using MyWorkItem.Domain;

namespace MyWorkItem.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}

public sealed class PasswordService : IPasswordService
{
    private static readonly Account PasswordOwner = new()
    {
        AccountId = Guid.Empty,
        UserName = "PasswordOwner",
        PasswordHash = string.Empty,
        IsEnabled = true,
        CreatedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch
    };

    private readonly PasswordHasher<Account> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(PasswordOwner, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(PasswordOwner, passwordHash, password) is not PasswordVerificationResult.Failed;
}

public sealed class JwtTokenService(JwtOptions options, IClock clock) : ITokenService
{
    public TokenPair Create(AccessProfile profile, Guid? tokenFamily = null)
    {
        var now = clock.UtcNow;
        var accessExpiresAt = now.AddMinutes(options.AccessTokenMinutes);
        var refreshExpiresAt = now.AddDays(options.RefreshTokenDays);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, profile.AccountId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, profile.UserName),
            new("user_id", profile.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, profile.AccountId.ToString()),
            new(ClaimTypes.Name, profile.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(profile.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(profile.Permissions.Select(permission => new Claim("permission", permission)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            now.UtcDateTime,
            accessExpiresAt.UtcDateTime,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return new TokenPair(
            new JwtSecurityTokenHandler().WriteToken(token),
            refreshToken,
            Guid.NewGuid(),
            tokenFamily ?? Guid.NewGuid(),
            accessExpiresAt,
            refreshExpiresAt);
    }

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
