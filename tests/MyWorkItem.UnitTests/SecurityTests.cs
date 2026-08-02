using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Bogus;
using FluentAssertions;
using MyWorkItem.Application.Contracts;
using MyWorkItem.Infrastructure.Security;

namespace MyWorkItem.UnitTests;

public sealed class SecurityTests
{
    [TestCase("short", false)]
    [TestCase("alllowercase123", false)]
    [TestCase("ValidPassword123!", true)]
    [TestCase("VALID-PASSWORD-123", true)]
    public void PasswordPolicy_應要求至少十二字元及四類中的三類(string password, bool expected)
    {
        PasswordPolicy.IsValid(password).Should().Be(expected);
    }

    [Test]
    public void TokenGenerator_應包含身分角色與Function且期限為十五分鐘()
    {
        var faker = new Faker("zh_TW");
        var now = new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);
        var options = new JwtOptions
        {
            SigningKey = "unit-test-signing-key-with-at-least-32-bytes!",
            AccessTokenMinutes = 15
        };
        var user = new CurrentUserResponse(
            Guid.NewGuid(), Guid.NewGuid(), faker.Internet.UserName(), faker.Name.FullName(),
            ["Manager"], ["WorkItems.Read", "WorkItems.Manage"]);

        var result = new TokenGenerator(options).CreateAccessToken(user, now);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        result.ExpiresAt.Should().Be(now.AddMinutes(15));
        jwt.Claims.Should().Contain(claim => claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == user.UserId.ToString());
        jwt.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == "Manager");
        jwt.Claims.Where(claim => claim.Type == "function").Select(claim => claim.Value)
            .Should().BeEquivalentTo(user.Functions);
    }

    [Test]
    public void 公開Auth回應不得包含Token或Password欄位()
    {
        var names = typeof(CurrentUserResponse).GetProperties().Select(property => property.Name);
        names.Should().NotContain(["AccessToken", "RefreshToken", "Password", "PasswordHash", "TokenHash"]);
    }
}
