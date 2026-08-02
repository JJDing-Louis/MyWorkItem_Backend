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
                    "IF EXISTS (SELECT 1 FROM RoleFunctions WHERE RoleId = @RoleId AND FunctionId = @FunctionId) UPDATE RoleFunctions SET IsEnabled = 1 WHERE RoleId = @RoleId AND FunctionId = @FunctionId ELSE INSERT INTO RoleFunctions (RoleId, FunctionId, IsEnabled) VALUES (@RoleId, @FunctionId, 1);",
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
        var identity = await connection.QuerySingleOrDefaultAsync<SeedIdentity>(new CommandDefinition(
            "SELECT AccountId, UserId FROM Accounts WHERE UserName = @UserName;",
            new { UserName = userName },
            transaction,
            cancellationToken: cancellationToken));
        if (identity is null)
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
                "INSERT INTO Users (UserId, Name) VALUES (@UserId, @Name); INSERT INTO Accounts (AccountId, UserId, UserName, PasswordHash, IsEnabled, CreatedAt, UpdatedAt) VALUES (@AccountId, @UserId, @UserName, @PasswordHash, 1, @CreatedAt, @UpdatedAt);",
                new { account.AccountId, account.UserName, account.PasswordHash, account.CreatedAt, account.UpdatedAt, UserId = userId, Name = name },
                transaction,
                cancellationToken: cancellationToken));
            identity = new SeedIdentity { AccountId = account.AccountId, UserId = userId };
        }

        foreach (var roleId in roleIds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "IF EXISTS (SELECT 1 FROM UserRoles WHERE UserId = @UserId AND RoleId = @RoleId) UPDATE UserRoles SET IsEnabled = 1 WHERE UserId = @UserId AND RoleId = @RoleId ELSE INSERT INTO UserRoles (UserId, RoleId, IsEnabled) VALUES (@UserId, @RoleId, 1);",
                new { identity.UserId, RoleId = roleId },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private sealed class SeedIdentity
    {
        public Guid AccountId { get; init; }
        public Guid UserId { get; init; }
    }
}
