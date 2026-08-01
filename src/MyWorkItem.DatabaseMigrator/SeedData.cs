using System.Data;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using MyWorkItem.Domain;

namespace MyWorkItem.DatabaseMigrator;

public static class SeedData
{
    private static readonly IReadOnlyDictionary<string, string[]> RolePermissions = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["Admin"] = ["WorkItems.Read", "WorkItems.Confirm", "WorkItems.Manage", "Users.Manage", "Roles.Manage", "Functions.Manage"],
        ["User"] = ["WorkItems.Read", "WorkItems.Confirm"],
        ["BackOffice"] = ["WorkItems.Read", "WorkItems.Manage"]
    };

    public static async Task ApplyAsync(string connectionString, string environmentName, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var functionIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var code in RolePermissions.Values.SelectMany(x => x).Distinct(StringComparer.Ordinal))
        {
            functionIds[code] = await UpsertNamedAsync(connection, transaction, "[Functions]", "FunctionId", code, code, cancellationToken);
        }

        var roleIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var (role, permissions) in RolePermissions)
        {
            var roleId = await UpsertNamedAsync(connection, transaction, "Roles", "RoleId", role, role, cancellationToken);
            roleIds[role] = roleId;
            foreach (var permission in permissions)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "IF NOT EXISTS (SELECT 1 FROM RoleFunctions WHERE RoleId = @RoleId AND FunctionId = @FunctionId) INSERT INTO RoleFunctions (RoleId, FunctionId) VALUES (@RoleId, @FunctionId);",
                    new { RoleId = roleId, FunctionId = functionIds[permission] },
                    transaction,
                    cancellationToken: cancellationToken));
            }
        }

        if (environmentName is "Development" or "Test")
        {
            await EnsureAccountAsync(connection, transaction, "Admin", "Admin", "系統管理員", [roleIds["Admin"]], cancellationToken);
            await EnsureAccountAsync(connection, transaction, "User", "User", "一般使用者", [roleIds["User"]], cancellationToken);
            await EnsureAccountAsync(connection, transaction, "BackOffice", "BackOffice", "後台使用者", [roleIds["BackOffice"]], cancellationToken);
            await EnsureAccountAsync(connection, transaction, "PowerUser", "PowerUser", "複合角色使用者", [roleIds["User"], roleIds["BackOffice"]], cancellationToken);
        }
        else
        {
            var adminPassword = Environment.GetEnvironmentVariable("Bootstrap__AdminPassword");
            if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < 12)
            {
                throw new InvalidOperationException("Production 必須透過 Bootstrap__AdminPassword 提供至少 12 字元的初始管理員密碼。");
            }

            var adminUserName = Environment.GetEnvironmentVariable("Bootstrap__AdminUserName") ?? "Admin";
            await EnsureAccountAsync(connection, transaction, adminUserName, adminPassword, "系統管理員", [roleIds["Admin"]], cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<Guid> UpsertNamedAsync(SqlConnection connection, IDbTransaction transaction, string table, string idColumn, string code, string name, CancellationToken cancellationToken)
    {
        var existing = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition($"SELECT {idColumn} FROM {table} WHERE Code = @Code;", new { Code = code }, transaction, cancellationToken: cancellationToken));
        if (existing is not null)
        {
            return existing.Value;
        }

        var id = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition($"INSERT INTO {table} ({idColumn}, Code, Name, IsEnabled) VALUES (@Id, @Code, @Name, 1);", new { Id = id, Code = code, Name = name }, transaction, cancellationToken: cancellationToken));
        return id;
    }

    private static async Task EnsureAccountAsync(SqlConnection connection, IDbTransaction transaction, string userName, string password, string name, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var accountId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT AccountId FROM Accounts WHERE UserName = @UserName;", new { UserName = userName }, transaction, cancellationToken: cancellationToken));
        if (accountId is null)
        {
            var now = DateTimeOffset.UtcNow;
            var account = new Account
            {
                AccountId = Guid.NewGuid(),
                UserName = userName,
                PasswordHash = string.Empty,
                IsEnabled = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            account.PasswordHash = new PasswordHasher<Account>().HashPassword(account, password);
            var userId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO Accounts (AccountId, UserName, PasswordHash, IsEnabled, CreatedAt, UpdatedAt) VALUES (@AccountId, @UserName, @PasswordHash, 1, @CreatedAt, @UpdatedAt); INSERT INTO Users (UserId, AccountId, Name) VALUES (@UserId, @AccountId, @Name);",
                new { account.AccountId, account.UserName, account.PasswordHash, account.CreatedAt, account.UpdatedAt, UserId = userId, Name = name },
                transaction,
                cancellationToken: cancellationToken));
            accountId = account.AccountId;
        }

        foreach (var roleId in roleIds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "IF NOT EXISTS (SELECT 1 FROM AccountRoles WHERE AccountId = @AccountId AND RoleId = @RoleId) INSERT INTO AccountRoles (AccountId, RoleId) VALUES (@AccountId, @RoleId);",
                new { AccountId = accountId.Value, RoleId = roleId },
                transaction,
                cancellationToken: cancellationToken));
        }
    }
}
