using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using MyWorkItem.Application;
using MyWorkItem.Infrastructure;

namespace MyWorkItem.UnitTests;

public sealed class JwtTokenServiceTests
{
    [Test]
    public void Create_包含角色與Function且RefreshToken不重複()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        var service = new JwtTokenService(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-with-at-least-32-bytes"
        }, clock);
        var profile = TestDataFactory.AccessProfile();

        var first = service.Create(profile);
        var second = service.Create(profile);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(first.AccessToken);

        token.Claims.Should().Contain(x => x.Type == ClaimTypes.Role && x.Value == RoleNames.User);
        token.Claims.Should().Contain(x => x.Type == "permission" && x.Value == PermissionCodes.WorkItemsConfirm);
        first.RefreshToken.Should().NotBe(second.RefreshToken);
        first.AccessTokenExpiresAt.Should().Be(clock.UtcNow.AddMinutes(15));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
