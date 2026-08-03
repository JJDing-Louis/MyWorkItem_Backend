using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MyWorkItem.Application.Contracts;
using MyWorkItem.DatabaseMigrator;
using MyWorkItem.Infrastructure.Security;
using Testcontainers.MsSql;

namespace MyWorkItem.WorkflowTests;

[TestFixture]
public sealed class WorkflowJourneyTests
{
    private MsSqlContainer sqlServer = null!;
    private string connectionString = null!;
    private WebApplicationFactory<Program> factory = null!;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sqlServer.StartAsync();
        connectionString = new SqlConnectionStringBuilder(sqlServer.GetConnectionString())
        {
            InitialCatalog = "MyWorkItemWorkflow"
        }.ConnectionString;
        DatabaseMigrationRunner.Run(connectionString).Successful.Should().BeTrue();
        await DevelopmentSeeder.SeedAsync(connectionString);

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            builder.UseSetting("Jwt:SigningKey", "workflow-test-signing-key-with-at-least-32-bytes!");
            builder.UseSetting("Swagger:Enabled", "true");
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

    [Test]
    public async Task WF01_缺少Csrf的Login應回傳400ProblemDetails()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("Admin", "Admin"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Test]
    public async Task WF02_CsrfLoginMe應完成且不需手動JWT()
    {
        using var session = await LoginAsync("Worker", "Worker");
        var me = await session.Client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me");
        me.Should().NotBeNull();
        me!.LoginName.Should().Be("Worker");
        me.Functions.Should().Contain(["WorkItems.Read", "WorkItems.Confirm"]);
    }

    [Test]
    public async Task WF03_Refresh應輪替且舊Token重播會撤銷Family()
    {
        using var session = await LoginAsync("Worker", "Worker");
        var oldRefreshToken = session.RefreshToken;
        (await session.PostAsync("/api/v1/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var replayClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var cookies = await GetCsrfCookiesAsync(replayClient);
        using var replay = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        replay.Headers.Add("X-CSRF-TOKEN", cookies.RequestToken);
        replay.Headers.Add("Cookie", $"mwi_refresh={oldRefreshToken}; mwi_antiforgery={cookies.AntiforgeryCookie}");
        (await replayClient.SendAsync(replay)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await session.PostAsync("/api/v1/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "舊 Token 重播後整個 Token Family 都應撤銷");
    }

    [Test]
    public async Task WF04_列表詳情與AssignedUserId篩選應保留個人狀態()
    {
        using var admin = await LoginAsync("Admin", "Admin");
        var users = await admin.GetFromJsonAsync<UserResponse[]>("/api/v1/users");
        var worker = users!.Single(user => user.LoginName == "Worker");
        var created = await CreateWorkItemAsync(admin, "WF04 指派篩選", worker.UserId);

        using var workerSession = await LoginAsync("Worker", "Worker");
        var userOptions = await workerSession.GetFromJsonAsync<WorkItemUserOptionResponse[]>(
            "/api/v1/work-items/user-options");
        userOptions.Should().Contain(user =>
            user.UserId == worker.UserId && user.LoginName == "Worker" && user.IsEnabled);
        var list = await workerSession.GetFromJsonAsync<PagedResponse<WorkItemResponse>>(
            $"/api/v1/work-items?assignedUserId={worker.UserId}");
        list!.Items.Should().ContainSingle(item => item.WorkItemId == created.WorkItemId && item.AssignedUserId == worker.UserId);
        (await workerSession.GetFromJsonAsync<WorkItemResponse>($"/api/v1/work-items/{created.WorkItemId}"))!
            .StatusCode.Should().Be("Pending");
    }

    [Test]
    public async Task WF05_不同使用者確認狀態應彼此獨立且可撤銷()
    {
        using var admin = await LoginAsync("Admin", "Admin");
        var created = await CreateWorkItemAsync(admin, "WF05 個人確認測試", null);

        using var worker = await LoginAsync("Worker", "Worker");
        var confirm = await worker.PutAsync($"/api/v1/work-items/{created.WorkItemId}/confirmation", null);
        confirm.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var workerItem = await worker.GetFromJsonAsync<WorkItemResponse>($"/api/v1/work-items/{created.WorkItemId}");
        var adminItem = await admin.GetFromJsonAsync<WorkItemResponse>($"/api/v1/work-items/{created.WorkItemId}");
        workerItem!.IsConfirmed.Should().BeTrue();
        adminItem!.IsConfirmed.Should().BeFalse();

        (await worker.DeleteAsync($"/api/v1/work-items/{created.WorkItemId}/confirmation"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await worker.GetFromJsonAsync<WorkItemResponse>($"/api/v1/work-items/{created.WorkItemId}"))!
            .IsConfirmed.Should().BeFalse();
    }

    [Test]
    public async Task WF07_CRUD應寫History且舊RowVersion回409()
    {
        using var admin = await LoginAsync("Admin", "Admin");
        var created = await CreateWorkItemAsync(admin, "WF07 原始標題", null);
        var update = new UpdateWorkItemRequest("WF07 更新標題", "更新後快照", null, created.RowVersion);
        var updatedResponse = await admin.PutAsJsonAsync($"/api/v1/work-items/{created.WorkItemId}", update);
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var conflict = await admin.PutAsJsonAsync($"/api/v1/work-items/{created.WorkItemId}", update);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.DeleteAsync($"/api/v1/work-items/{created.WorkItemId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var connection = new SqlConnection(connectionString);
        var actions = (await connection.QueryAsync<string>(
            """
            SELECT a.Code FROM dbo.WorkItemHistories h
            JOIN dbo.Actions a ON a.ActionId = h.ActionId
            WHERE h.WorkItemId = @WorkItemId ORDER BY h.HistoryId
            """, new { created.WorkItemId })).ToArray();
        actions.Should().Equal("INSERT", "UPDATE", "DELETE");
    }

    [Test]
    public async Task WF06_批次確認含無效Id時應全部Rollback()
    {
        using var admin = await LoginAsync("Admin", "Admin");
        var first = await CreateWorkItemAsync(admin, "WF06 第一筆", null);
        var second = await CreateWorkItemAsync(admin, "WF06 第二筆", null);
        using var worker = await LoginAsync("Worker", "Worker");
        var response = await worker.PostAsJsonAsync("/api/v1/work-items/confirmations/batch",
            new BatchConfirmationRequest([first.WorkItemId, second.WorkItemId, Guid.NewGuid()]));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var connection = new SqlConnection(connectionString);
        var stateCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.UserWorkItemStates WHERE WorkItemId IN @Ids",
            new { Ids = new[] { first.WorkItemId, second.WorkItemId } });
        stateCount.Should().Be(0);
    }

    [Test]
    public async Task WF08_角色權限變更應立即生效且Manager不能管理Roles()
    {
        using var admin = await LoginAsync("Admin", "Admin");
        using var manager = await LoginAsync("Manager", "manager");
        (await manager.Client.GetAsync("/api/v1/roles")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var users = await admin.GetFromJsonAsync<UserResponse[]>("/api/v1/users");
        var worker = users!.Single(user => user.LoginName == "Worker");
        var roles = await admin.GetFromJsonAsync<PermissionItemResponse[]>("/api/v1/roles");
        var managerRole = roles!.Single(role => role.Code == "Manager");
        var workerRole = roles!.Single(role => role.Code == "Worker");
        var created = await CreateWorkItemAsync(admin, "WF08 權限即時生效", null);
        using var workerSession = await LoginAsync("Worker", "Worker");

        (await admin.PutAsJsonAsync($"/api/v1/users/{worker.UserId}/roles",
            new ReplaceUserRolesRequest([managerRole.Id]))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await workerSession.Client.DeleteAsync($"/api/v1/work-items/{created.WorkItemId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent, "既有 JWT 不重登也應立即取得 Manager 權限");

        (await admin.PutAsJsonAsync($"/api/v1/users/{worker.UserId}/roles",
            new ReplaceUserRolesRequest([workerRole.Id]))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await workerSession.PostAsJsonAsync("/api/v1/work-items", new CreateWorkItemRequest("不可新增", null, null)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task WF09_Logout應撤銷Family清除Cookie並使Me失效()
    {
        using var session = await LoginAsync("Worker", "Worker");
        (await session.PostAsync("/api/v1/auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await session.Client.GetAsync("/api/v1/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var connection = new SqlConnection(connectionString);
        var active = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.RefreshTokens WHERE TokenHash = @TokenHash AND RevokedAt IS NULL",
            new { TokenHash = TokenGenerator.HashRefreshToken(session.RefreshToken) });
        active.Should().Be(0);
    }

    [Test]
    public async Task WF10_AdminManagerWorker完整旅程應成功()
    {
        using var admin = await LoginAsync("Admin", "Admin");
        var roles = await admin.GetFromJsonAsync<PermissionItemResponse[]>("/api/v1/roles");
        var workerRole = roles!.Single(role => role.Code == "Worker");
        var loginName = $"Journey{Guid.NewGuid():N}"[..18];
        var createUser = new CreateUserRequest(loginName, "Journey123!Password", "Journey Worker", null, null, [workerRole.Id]);
        var createUserResponse = await admin.PostAsJsonAsync("/api/v1/users", createUser);
        createUserResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = (await createUserResponse.Content.ReadFromJsonAsync<UserResponse>())!;

        using var manager = await LoginAsync("Manager", "manager");
        var item = await CreateWorkItemAsync(manager, "WF10 完整旅程", user.UserId);
        using var worker = await LoginAsync(loginName, "Journey123!Password");
        (await worker.PutAsync($"/api/v1/work-items/{item.WorkItemId}/confirmation", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await worker.GetFromJsonAsync<WorkItemResponse>($"/api/v1/work-items/{item.WorkItemId}"))!
            .IsConfirmed.Should().BeTrue();
    }

    [Test]
    public async Task WF11_Swagger等價流程可自動CookieCsrf登入RefreshLogout()
    {
        using var session = await LoginAsync("Admin", "Admin");
        (await session.Client.GetAsync("/api/v1/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await CreateWorkItemAsync(session, "WF11 Swagger Journey", null)).Should().NotBeNull();
        (await session.PostAsync("/api/v1/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await session.PostAsync("/api/v1/auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await session.Client.GetAsync("/api/v1/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<ApiSession> LoginAsync(string loginName, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        var csrf = csrfResponse.Headers.GetValues("Set-Cookie")
            .Select(value => Regex.Match(value, "XSRF-TOKEN=([^;]+)"))
            .First(match => match.Success).Groups[1].Value;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", Uri.UnescapeDataString(csrf));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(loginName, password));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshToken = response.Headers.GetValues("Set-Cookie")
            .Select(value => Regex.Match(value, "mwi_refresh=([^;]+)"))
            .First(match => match.Success).Groups[1].Value;
        csrf = await RefreshCsrfAsync(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        return new ApiSession(client, refreshToken);
    }

    private static async Task<string> RefreshCsrfAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/csrf");
        return Uri.UnescapeDataString(response.Headers.GetValues("Set-Cookie")
            .Select(value => Regex.Match(value, "XSRF-TOKEN=([^;]+)"))
            .First(match => match.Success).Groups[1].Value);
    }

    private static async Task<CsrfCookies> GetCsrfCookiesAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/csrf");
        var values = response.Headers.GetValues("Set-Cookie").ToArray();
        static string Read(IEnumerable<string> values, string name) => Uri.UnescapeDataString(values
            .Select(value => Regex.Match(value, $"{name}=([^;]+)"))
            .First(match => match.Success).Groups[1].Value);
        return new CsrfCookies(Read(values, "XSRF-TOKEN"), Read(values, "mwi_antiforgery"));
    }

    private static async Task<WorkItemResponse> CreateWorkItemAsync(ApiSession session, string title, Guid? assignedUserId)
    {
        var response = await session.PostAsJsonAsync("/api/v1/work-items", new CreateWorkItemRequest(title, "Workflow Test", assignedUserId));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<WorkItemResponse>())!;
    }

    private sealed class ApiSession(HttpClient client, string refreshToken) : IDisposable
    {
        public HttpClient Client { get; } = client;
        public string RefreshToken { get; } = refreshToken;
        public Task<HttpResponseMessage> PutAsync(string path, HttpContent? content) => Client.PutAsync(path, content);
        public Task<HttpResponseMessage> DeleteAsync(string path) => Client.DeleteAsync(path);
        public Task<HttpResponseMessage> PostAsync(string path, HttpContent? content) => Client.PostAsync(path, content);
        public Task<HttpResponseMessage> PostAsJsonAsync<T>(string path, T value) => Client.PostAsJsonAsync(path, value);
        public Task<HttpResponseMessage> PutAsJsonAsync<T>(string path, T value) => Client.PutAsJsonAsync(path, value);
        public Task<T?> GetFromJsonAsync<T>(string path) => Client.GetFromJsonAsync<T>(path);
        public void Dispose() => Client.Dispose();
    }

    private sealed record CsrfCookies(string RequestToken, string AntiforgeryCookie);
}
