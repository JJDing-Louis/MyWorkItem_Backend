using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MyWorkItem.IntegrationTests;

public sealed class ProductionSwaggerTests
{
    [Test]
    public async Task Production即使設定Enabled也不得公開Swagger()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=127.0.0.1;Database=unused;User Id=sa;Password=Unused123!Password;TrustServerCertificate=True");
            builder.UseSetting("Jwt:SigningKey", "production-test-signing-key-with-at-least-32-bytes!");
            builder.UseSetting("Swagger:Enabled", "true");
        });
        using var client = factory.CreateClient();
        (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/swagger/index.html")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
