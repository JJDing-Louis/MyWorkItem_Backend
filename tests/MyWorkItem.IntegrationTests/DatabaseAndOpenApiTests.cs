using System.Net;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MyWorkItem.DatabaseMigrator;
using MyWorkItem.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace MyWorkItem.IntegrationTests;

[TestFixture]
public sealed class DatabaseAndOpenApiTests
{
    private MsSqlContainer sqlServer = null!;
    private string connectionString = null!;
    private WebApplicationFactory<Program> factory = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sqlServer.StartAsync();
        var builder = new SqlConnectionStringBuilder(sqlServer.GetConnectionString())
        {
            InitialCatalog = "MyWorkItemIntegration"
        };
        connectionString = builder.ConnectionString;
        DatabaseMigrationRunner.Run(connectionString).Successful.Should().BeTrue();
        DatabaseMigrationRunner.Run(connectionString).Successful.Should().BeTrue("Migration 必須可安全重跑");
        await DevelopmentSeeder.SeedAsync(connectionString);

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webBuilder =>
        {
            webBuilder.UseEnvironment("Development");
            webBuilder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            webBuilder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-with-at-least-32-bytes!");
            webBuilder.UseSetting("Swagger:Enabled", "true");
        });
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        factory?.Dispose();
        if (sqlServer is not null)
        {
            await sqlServer.DisposeAsync();
        }
    }

    [TestCase("Admin", "Admin")]
    [TestCase("Lisa1150803", "Test")]
    [TestCase("James1150803", "Test")]
    [TestCase("Emily1150803", "Test")]
    [TestCase("Daniel1150803", "Test")]
    [TestCase("Sophia1150803", "Test")]
    [TestCase("Michael1150803", "Test")]
    [TestCase("Olivia1150803", "Test")]
    [TestCase("Ethan1150803", "Test")]
    [TestCase("Ava1150803", "Test")]
    [TestCase("Noah1150803", "Test")]
    public async Task AuthenticationService_應可使用Seed帳號登入(string loginName, string password)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var result = await service.LoginAsync(loginName, password, CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Test]
    public async Task Schema_應包含V11資料表約束與靜態資料()
    {
        await using var connection = new SqlConnection(connectionString);
        var tableCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM sys.tables WHERE name IN ('Users','Accounts','Roles','Functions','UserRoles','RoleFunctions','RefreshTokens','WorkItemStatuses','Actions','WorkItems','UserWorkItemStates','WorkItemHistories')");
        var cascadeCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM sys.foreign_keys WHERE delete_referential_action <> 0");
        var assignedNullable = await connection.ExecuteScalarAsync<bool>(
            "SELECT is_nullable FROM sys.columns WHERE object_id = OBJECT_ID('dbo.WorkItems') AND name = 'AssignedUserId'");
        var roles = (await connection.QueryAsync<string>("SELECT Code FROM dbo.Roles ORDER BY Code")).ToArray();

        tableCount.Should().Be(12);
        cascadeCount.Should().Be(0);
        assignedNullable.Should().BeTrue();
        roles.Should().BeEquivalentTo(["Admin", "Manager", "Worker"]);
    }

    [Test]
    public async Task SwaggerJson_應包含CookieAuthCsrf與主要端點()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var json = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("info").GetProperty("title").GetString().Should().Be("MyWorkItem Backend API");
        root.GetProperty("paths").TryGetProperty("/api/v1/auth/login", out _).Should().BeTrue();
        root.GetProperty("paths").TryGetProperty("/api/v1/work-items", out var workItems).Should().BeTrue();
        root.GetProperty("paths").TryGetProperty("/api/v1/work-items/user-options", out _).Should().BeTrue();
        workItems.GetProperty("post").GetProperty("parameters").EnumerateArray()
            .Should().Contain(parameter => parameter.GetProperty("name").GetString() == "X-CSRF-TOKEN");
        json.Should().Contain("CookieAuthentication");
        json.Should().Contain("\"loginName\": \"Admin\"");
        json.Should().Contain("\"password\": \"Admin\"");
        json.Should().NotContain("PasswordHash");
        json.Should().NotContain("TokenHash");
    }

    [Test]
    public async Task Swagger自訂JavaScript應可載入()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger-ui/csrf.js");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("/api/v1/auth/csrf");
        var indexScript = await client.GetStringAsync("/swagger/index.js");
        indexScript.Should().Contain("X-CSRF-TOKEN");
        indexScript.Should().Contain("RequestInterceptorFunction");
    }
}
