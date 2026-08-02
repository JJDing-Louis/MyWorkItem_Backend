using System.Net;
using System.Net.Http.Json;
using Bogus;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using MyWorkItem.Application;
using MyWorkItem.DatabaseMigrator;
using Testcontainers.MsSql;

namespace MyWorkItem.IntegrationTests;

[NonParallelizable]
public sealed class BackendIntegrationTests
{
    private MsSqlContainer _database = null!;
    private MyWorkItemApiFactory _factory = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
        await _database.StartAsync();
        var connectionString = _database.GetConnectionString();
        var upgrade = DatabaseUpgrade.Run(connectionString);
        upgrade.Successful.Should().BeTrue(upgrade.Error?.ToString());
        await SeedData.ApplyAsync(connectionString, "Test");
        _factory = new MyWorkItemApiFactory(connectionString);
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    [Test]
    public void Migration_重複執行仍成功()
    {
        DatabaseUpgrade.Run(_database.GetConnectionString()).Successful.Should().BeTrue();
    }

    [Test]
    public async Task Schema_對齊草稿且保留個人確認狀態設計()
    {
        await using var connection = new SqlConnection(_database.GetConnectionString());
        var columns = (await connection.QueryAsync<string>("""
            SELECT TABLE_NAME + '.' + COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME IN ('Accounts', 'Users', 'UserRoles', 'RoleFunctions', 'WorkItems', 'WorkItemStatuses', 'UserWorkItemStates');
            """)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tables = (await connection.QueryAsync<string>("""
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE';
            """)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        columns.Should().Contain([
            "Accounts.UserId",
            "UserRoles.UserId",
            "UserRoles.RoleId",
            "UserRoles.IsEnabled",
            "RoleFunctions.IsEnabled",
            "WorkItems.CreatedUserId",
            "WorkItems.AssignedUserId",
            "WorkItems.WorkItemStatusId",
            "UserWorkItemStates.IsConfirmed"
        ]);
        tables.Should().Contain("WorkItemStatuses");
        tables.Should().NotContain("AccountRoles");

        var statusCodes = (await connection.QueryAsync<string>("SELECT Code FROM WorkItemStatuses ORDER BY Code;")).ToArray();
        statusCodes.Should().BeEquivalentTo(["Active", "Closed"]);
    }

    [Test]
    public async Task WorkItem確認_不同使用者維持獨立狀態()
    {
        using var admin = CreateClient();
        var adminCsrf = await GetCsrfAsync(admin);
        adminCsrf = await LoginAsync(admin, adminCsrf, "Admin", "Admin");
        var createResponse = await SendAsync(admin, HttpMethod.Post, "/api/v1/work-items", adminCsrf, new CreateWorkItemRequest("整合測試項目", "驗證使用者隔離"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<WorkItemResponse>();
        created.Should().NotBeNull();

        using var user = CreateClient();
        var userCsrf = await GetCsrfAsync(user);
        userCsrf = await LoginAsync(user, userCsrf, "User", "User");
        var confirmResponse = await SendAsync<object>(user, HttpMethod.Put, $"/api/v1/work-items/{created!.WorkItemId}/confirmation", userCsrf);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var userItem = await user.GetFromJsonAsync<WorkItemResponse>($"/api/v1/work-items/{created.WorkItemId}");
        userItem!.IsConfirmed.Should().BeTrue();

        using var powerUser = CreateClient();
        var powerUserCsrf = await GetCsrfAsync(powerUser);
        powerUserCsrf = await LoginAsync(powerUser, powerUserCsrf, "PowerUser", "PowerUser");
        var otherItem = await powerUser.GetFromJsonAsync<WorkItemResponse>($"/api/v1/work-items/{created.WorkItemId}");
        otherItem!.IsConfirmed.Should().BeFalse();
    }

    [Test]
    public async Task 寫入API_缺少CsrfHeader時拒絕請求()
    {
        using var admin = CreateClient();
        var csrf = await GetCsrfAsync(admin);
        await LoginAsync(admin, csrf, "Admin", "Admin");

        var response = await admin.PostAsJsonAsync("/api/v1/work-items", new CreateWorkItemRequest("沒有 CSRF", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task 使用者管理_可用Bogus資料建立修改並指派角色()
    {
        using var admin = CreateClient();
        var csrf = await GetCsrfAsync(admin);
        csrf = await LoginAsync(admin, csrf, "Admin", "Admin");
        var roles = await admin.GetFromJsonAsync<IReadOnlyCollection<RoleResponse>>("/api/v1/roles");
        var userRole = roles!.Single(role => role.Code == RoleNames.User);
        var faker = new Faker("zh_TW");
        var userName = $"test_{faker.Random.AlphaNumeric(10)}";
        var createRequest = new CreateUserRequest(
            userName,
            "ValidPassword!123",
            faker.Name.FullName(),
            faker.Internet.Email(),
            faker.Lorem.Sentence(),
            [userRole.RoleId]);

        var createResponse = await SendAsync(admin, HttpMethod.Post, "/api/v1/users", csrf, createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<UserResponse>();
        created!.Roles.Should().ContainSingle(role => role.Code == RoleNames.User);

        var updateRequest = new UpdateUserRequest("修改後姓名", faker.Internet.Email(), "已更新");
        var updateResponse = await SendAsync(admin, HttpMethod.Put, $"/api/v1/users/{created.UserId}", csrf, updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<UserResponse>();
        updated!.Name.Should().Be("修改後姓名");
    }

    [Test]
    public async Task 一般使用者_無法新增WorkItem()
    {
        using var user = CreateClient();
        var csrf = await GetCsrfAsync(user);
        csrf = await LoginAsync(user, csrf, "User", "User");

        var response = await SendAsync(user, HttpMethod.Post, "/api/v1/work-items", csrf, new CreateWorkItemRequest("不應成功", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
        AllowAutoRedirect = false
    });

    private static async Task<string> GetCsrfAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/csrf");
        response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(cookie.Split(';', 2)[0].Split('=', 2)[1]);
    }

    private static async Task<string> LoginAsync(HttpClient client, string csrf, string userName, string password)
    {
        var response = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/login", csrf, new LoginRequest(userName, password));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await GetCsrfAsync(client);
    }

    private static async Task<HttpResponseMessage> SendAsync<T>(HttpClient client, HttpMethod method, string path, string csrf, T? body = default)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }
}
