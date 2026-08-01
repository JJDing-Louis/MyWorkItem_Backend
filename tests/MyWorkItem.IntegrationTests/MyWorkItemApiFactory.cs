using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MyWorkItem.IntegrationTests;

internal sealed class MyWorkItemApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("Jwt:Issuer", "MyWorkItem.IntegrationTests");
        builder.UseSetting("Jwt:Audience", "MyWorkItem.IntegrationTests.Client");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-with-at-least-32-bytes");
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenDays", "7");
        builder.UseSetting("Cors:AllowedOrigins:0", "https://localhost");
    }
}
