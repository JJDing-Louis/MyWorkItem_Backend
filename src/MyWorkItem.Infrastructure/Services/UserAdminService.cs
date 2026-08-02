using System.Data.Common;
using Dapper;
using Microsoft.AspNetCore.Identity;
using MyWorkItem.Application.Abstractions;
using MyWorkItem.Application.Contracts;
using MyWorkItem.Application.Exceptions;
using MyWorkItem.Infrastructure.Security;

namespace MyWorkItem.Infrastructure.Services;

public sealed class UserAdminService(IDbConnectionFactory connectionFactory, IClock clock) : IUserAdminService
{
    private readonly PasswordHasher<AccountPasswordSubject> passwordHasher = new();

    public async Task<IReadOnlyCollection<UserResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<UserRow>(new CommandDefinition(
            """
            SELECT u.UserId, a.AccountId, a.LoginName, u.Name, u.Email, u.Remark, a.IsEnabled
            FROM dbo.Users u JOIN dbo.Accounts a ON a.UserId = u.UserId
            ORDER BY u.Name, a.LoginName
            """,
            cancellationToken: cancellationToken));
        var result = new List<UserResponse>();
        foreach (var row in rows)
        {
            result.Add(await MapAsync(connection, null, row, cancellationToken));
        }

        return result;
    }

    public async Task<UserResponse?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var row = await GetRowAsync(connection, null, userId, cancellationToken);
        return row is null ? null : await MapAsync(connection, null, row, cancellationToken);
    }

    public async Task<UserResponse> CreateAsync(
        Guid currentUserId,
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        ValidatePassword(request.Password);
        var normalizedLogin = Normalize(request.LoginName);
        var normalizedEmail = NormalizeOptional(request.Email);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await EnsureUniqueAsync(connection, transaction, normalizedLogin, normalizedEmail, null, cancellationToken);
        await ValidateRolesAsync(connection, transaction, request.RoleIds, cancellationToken);

        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var now = clock.UtcNow;
        var passwordHash = passwordHasher.HashPassword(new AccountPasswordSubject(accountId), request.Password);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.Users (UserId, Name, Email, NormalizedEmail, Remark, CreatedAt, UpdatedAt)
            VALUES (@UserId, @Name, @Email, @NormalizedEmail, @Remark, @Now, @Now);
            INSERT dbo.Accounts (AccountId, UserId, LoginName, NormalizedLoginName, PasswordHash, IsEnabled, CreatedAt, UpdatedAt)
            VALUES (@AccountId, @UserId, @LoginName, @NormalizedLoginName, @PasswordHash, 1, @Now, @Now);
            """,
            new
            {
                UserId = userId,
                AccountId = accountId,
                LoginName = request.LoginName.Trim(),
                NormalizedLoginName = normalizedLogin,
                PasswordHash = passwordHash,
                Name = request.Name.Trim(),
                request.Email,
                NormalizedEmail = normalizedEmail,
                request.Remark,
                Now = now
            }, transaction, cancellationToken: cancellationToken));
        await ReplaceRolesCoreAsync(connection, transaction, currentUserId, userId, request.RoleIds, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(userId, cancellationToken) ?? throw new NotFoundException("找不到新建立的使用者。");
    }

    public async Task<UserResponse> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeOptional(request.Email);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await EnsureUniqueAsync(connection, null, null, normalizedEmail, userId, cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.Users SET Name = @Name, Email = @Email, NormalizedEmail = @NormalizedEmail,
                Remark = @Remark, UpdatedAt = @Now WHERE UserId = @UserId
            """,
            new
            {
                UserId = userId,
                Name = request.Name.Trim(),
                request.Email,
                NormalizedEmail = normalizedEmail,
                request.Remark,
                Now = clock.UtcNow
            }, cancellationToken: cancellationToken));
        if (affected == 0)
        {
            throw new NotFoundException("找不到使用者。");
        }

        return await GetAsync(userId, cancellationToken) ?? throw new NotFoundException("找不到使用者。");
    }

    public async Task SetStatusAsync(Guid userId, bool isEnabled, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.Accounts SET IsEnabled = @IsEnabled, UpdatedAt = @Now WHERE UserId = @UserId",
            new { UserId = userId, IsEnabled = isEnabled, Now = clock.UtcNow }, cancellationToken: cancellationToken));
        if (affected == 0)
        {
            throw new NotFoundException("找不到使用者。");
        }
    }

    public async Task ResetPasswordAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        ValidatePassword(password);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var accountId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT AccountId FROM dbo.Accounts WHERE UserId = @UserId",
            new { UserId = userId }, cancellationToken: cancellationToken));
        if (accountId is null)
        {
            throw new NotFoundException("找不到使用者。");
        }

        var hash = passwordHasher.HashPassword(new AccountPasswordSubject(accountId.Value), password);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.Accounts SET PasswordHash = @PasswordHash, UpdatedAt = @Now WHERE AccountId = @AccountId;
            UPDATE dbo.RefreshTokens SET RevokedAt = COALESCE(RevokedAt, @Now), RevocationReason = COALESCE(RevocationReason, N'Password reset')
            WHERE AccountId = @AccountId;
            """,
            new { AccountId = accountId.Value, PasswordHash = hash, Now = clock.UtcNow },
            cancellationToken: cancellationToken));
    }

    public async Task ReplaceRolesAsync(
        Guid currentUserId,
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (await GetRowAsync(connection, transaction, userId, cancellationToken) is null)
        {
            throw new NotFoundException("找不到使用者。");
        }

        await ValidateRolesAsync(connection, transaction, roleIds, cancellationToken);
        await ReplaceRolesCoreAsync(connection, transaction, currentUserId, userId, roleIds, clock.UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ReplaceRolesCoreAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid currentUserId,
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE dbo.UserRoles WHERE UserId = @UserId",
            new { UserId = userId }, transaction, cancellationToken: cancellationToken));
        foreach (var roleId in roleIds.Distinct())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT dbo.UserRoles (UserId, RoleId, IsEnabled, AssignedAt, AssignedByUserId) VALUES (@UserId, @RoleId, 1, @Now, @CurrentUserId)",
                new { UserId = userId, RoleId = roleId, Now = now, CurrentUserId = currentUserId }, transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static async Task ValidateRolesAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        var distinct = roleIds.Distinct().ToArray();
        if (distinct.Length == 0)
        {
            throw new RequestValidationException("使用者至少需要一個角色。");
        }

        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.Roles WHERE RoleId IN @RoleIds AND IsEnabled = 1",
            new { RoleIds = distinct }, transaction, cancellationToken: cancellationToken));
        if (count != distinct.Length)
        {
            throw new RequestValidationException("角色不存在或已停用。");
        }
    }

    private static async Task EnsureUniqueAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string? normalizedLogin,
        string? normalizedEmail,
        Guid? excludedUserId,
        CancellationToken cancellationToken)
    {
        if (normalizedLogin is not null && await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.Accounts WHERE NormalizedLoginName = @Value) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END",
                new { Value = normalizedLogin }, transaction, cancellationToken: cancellationToken)))
        {
            throw new ConflictException("登入名稱已存在。");
        }

        if (normalizedEmail is not null && await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.Users WHERE NormalizedEmail = @Value AND (@Excluded IS NULL OR UserId <> @Excluded)) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END",
                new { Value = normalizedEmail, Excluded = excludedUserId }, transaction, cancellationToken: cancellationToken)))
        {
            throw new ConflictException("Email 已存在。");
        }
    }

    private static async Task<UserRow?> GetRowAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid userId,
        CancellationToken cancellationToken) => await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
            """
            SELECT u.UserId, a.AccountId, a.LoginName, u.Name, u.Email, u.Remark, a.IsEnabled
            FROM dbo.Users u JOIN dbo.Accounts a ON a.UserId = u.UserId WHERE u.UserId = @UserId
            """,
            new { UserId = userId }, transaction, cancellationToken: cancellationToken));

    private static async Task<UserResponse> MapAsync(
        DbConnection connection,
        DbTransaction? transaction,
        UserRow row,
        CancellationToken cancellationToken)
    {
        var roles = (await connection.QueryAsync<LookupResponse>(new CommandDefinition(
            """
            SELECT r.RoleId AS Id, r.Code, r.Name, r.IsEnabled
            FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.RoleId = ur.RoleId
            WHERE ur.UserId = @UserId AND ur.IsEnabled = 1 ORDER BY r.Code
            """,
            new { row.UserId }, transaction, cancellationToken: cancellationToken))).ToArray();
        return new UserResponse(
            row.UserId, row.AccountId, row.LoginName, row.Name, row.Email, row.Remark, row.IsEnabled, roles);
    }

    private static void ValidatePassword(string password)
    {
        if (!PasswordPolicy.IsValid(password))
        {
            throw new RequestValidationException("密碼至少 12 字元，並須符合大小寫、數字、符號四類中的三類。");
        }
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : Normalize(value);

    private sealed record AccountPasswordSubject(Guid AccountId);
    private sealed record UserRow(
        Guid UserId,
        Guid AccountId,
        string LoginName,
        string Name,
        string? Email,
        string? Remark,
        bool IsEnabled);
}
