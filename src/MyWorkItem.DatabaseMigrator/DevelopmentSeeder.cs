using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using MyWorkItem.Domain.Constants;

namespace MyWorkItem.DatabaseMigrator;

public static class DevelopmentSeeder
{
    public static async Task SeedAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        const string testingPassword = "Test";
        var users = new[]
        {
            new SeedUser("Admin", "Admin User", GetPassword("SEED_ADMIN_PASSWORD", "Admin"), RoleCodes.Admin),
            new SeedUser("Manager", "Manager User", GetPassword("SEED_MANAGER_PASSWORD", "manager"), RoleCodes.Manager),
            new SeedUser("Worker", "Worker User", GetPassword("SEED_WORKER_PASSWORD", "Worker"), RoleCodes.Worker),
            new SeedUser("Lisa1150803", "Lisa Test Worker", testingPassword, RoleCodes.Worker),
            new SeedUser("James1150803", "James Test Worker", testingPassword, RoleCodes.Worker),
            new SeedUser("Emily1150803", "Emily Test Worker", testingPassword, RoleCodes.Worker),
            new SeedUser("Daniel1150803", "Daniel Test Worker", testingPassword, RoleCodes.Worker),
            new SeedUser("Sophia1150803", "Sophia Test Worker", testingPassword, RoleCodes.Worker),
            new SeedUser("Michael1150803", "Michael Test Manager", testingPassword, RoleCodes.Manager),
            new SeedUser("Olivia1150803", "Olivia Test Manager", testingPassword, RoleCodes.Manager),
            new SeedUser("Ethan1150803", "Ethan Test Manager", testingPassword, RoleCodes.Manager),
            new SeedUser("Ava1150803", "Ava Test Manager", testingPassword, RoleCodes.Manager),
            new SeedUser("Noah1150803", "Noah Test Manager", testingPassword, RoleCodes.Manager)
        };

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var seed in users)
        {
            if (await connection.ExecuteScalarAsync<int>(
                    new CommandDefinition("SELECT COUNT(1) FROM dbo.Accounts WHERE LoginName = @LoginName", seed, cancellationToken: cancellationToken)) > 0)
            {
                continue;
            }

            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var hash = new PasswordHasher<object>().HashPassword(new object(), seed.Password);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT dbo.Users (UserId, Name, Email, Remark, CreatedAt, UpdatedAt)
                VALUES (@UserId, @Name, NULL, N'Development seed account', @Now, @Now);
                INSERT dbo.Accounts (AccountId, UserId, LoginName, NormalizedLoginName, PasswordHash, IsEnabled, CreatedAt, UpdatedAt)
                VALUES (@AccountId, @UserId, @LoginName, @NormalizedLoginName, @PasswordHash, 1, @Now, @Now);
                INSERT dbo.UserRoles (UserId, RoleId, IsEnabled, AssignedAt, AssignedByUserId)
                SELECT @UserId, RoleId, 1, @Now, NULL FROM dbo.Roles WHERE Code = @RoleCode;
                """,
                new
                {
                    userId,
                    accountId,
                    seed.LoginName,
                    NormalizedLoginName = seed.LoginName.ToUpperInvariant(),
                    seed.Name,
                    PasswordHash = hash,
                    Now = now,
                    seed.RoleCode
                },
                transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static string GetPassword(string variableName, string developmentDefault) =>
        Environment.GetEnvironmentVariable(variableName) ?? developmentDefault;

    private sealed record SeedUser(string LoginName, string Name, string Password, string RoleCode);
}
