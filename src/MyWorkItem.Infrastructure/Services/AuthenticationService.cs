using System.Data.Common;
using Dapper;
using Microsoft.AspNetCore.Identity;
using MyWorkItem.Application.Abstractions;
using MyWorkItem.Application.Contracts;
using MyWorkItem.Infrastructure.Security;

namespace MyWorkItem.Infrastructure.Services;

public sealed class AuthenticationService(
    IDbConnectionFactory connectionFactory,
    IClock clock,
    JwtOptions jwtOptions) : IAuthenticationService
{
    private readonly PasswordHasher<AccountPasswordSubject> passwordHasher = new();
    private readonly TokenGenerator tokenGenerator = new(jwtOptions);

    public async Task<AuthenticationResult?> LoginAsync(
        string loginName,
        string password,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var account = await connection.QuerySingleOrDefaultAsync<AccountLoginRow>(new CommandDefinition(
            """
            SELECT a.AccountId, a.UserId, a.LoginName, a.PasswordHash, a.IsEnabled, u.Name
            FROM dbo.Accounts a
            JOIN dbo.Users u ON u.UserId = a.UserId
            WHERE a.NormalizedLoginName = @NormalizedLoginName
            """,
            new { NormalizedLoginName = Normalize(loginName) },
            cancellationToken: cancellationToken));

        if (account is null || !account.IsEnabled)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(
            new AccountPasswordSubject(account.AccountId),
            account.PasswordHash,
            password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var user = await BuildCurrentUserAsync(connection, null, account, cancellationToken);
        return await IssueTokensAsync(connection, null, account.AccountId, user, Guid.NewGuid(), cancellationToken);
    }

    public async Task<AuthenticationResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = TokenGenerator.HashRefreshToken(refreshToken);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var token = await connection.QuerySingleOrDefaultAsync<RefreshTokenRow>(new CommandDefinition(
            """
            SELECT rt.RefreshTokenId, rt.AccountId, rt.FamilyId, rt.ExpiresAt, rt.RevokedAt,
                   a.UserId, a.LoginName, a.PasswordHash, a.IsEnabled, u.Name
            FROM dbo.RefreshTokens rt WITH (UPDLOCK, HOLDLOCK)
            JOIN dbo.Accounts a ON a.AccountId = rt.AccountId
            JOIN dbo.Users u ON u.UserId = a.UserId
            WHERE rt.TokenHash = @Hash
            """,
            new { Hash = hash }, transaction, cancellationToken: cancellationToken));

        if (token is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var now = clock.UtcNow;
        if (!token.IsEnabled || token.RevokedAt is not null || token.ExpiresAt <= now)
        {
            await RevokeFamilyAsync(connection, transaction, token.FamilyId, "Refresh token replay or invalid token", now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var account = new AccountLoginRow(
            token.AccountId,
            token.UserId,
            token.LoginName,
            token.PasswordHash,
            token.IsEnabled,
            token.Name);
        var user = await BuildCurrentUserAsync(connection, transaction, account, cancellationToken);
        var replacement = await IssueTokensAsync(connection, transaction, token.AccountId, user, token.FamilyId, cancellationToken);
        var replacementId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "SELECT RefreshTokenId FROM dbo.RefreshTokens WHERE TokenHash = @Hash",
            new { Hash = TokenGenerator.HashRefreshToken(replacement.RefreshToken) }, transaction,
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.RefreshTokens
            SET RevokedAt = @Now, ReplacedByTokenId = @ReplacementId, RevocationReason = N'Rotated'
            WHERE RefreshTokenId = @RefreshTokenId
            """,
            new { Now = now, ReplacementId = replacementId, token.RefreshTokenId }, transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return replacement;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var familyId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT FamilyId FROM dbo.RefreshTokens WHERE TokenHash = @Hash",
            new { Hash = TokenGenerator.HashRefreshToken(refreshToken) }, cancellationToken: cancellationToken));
        if (familyId is null)
        {
            return;
        }

        await RevokeFamilyAsync(connection, null, familyId.Value, "Logout", clock.UtcNow, cancellationToken);
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(
        Guid accountId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var account = await connection.QuerySingleOrDefaultAsync<AccountLoginRow>(new CommandDefinition(
            """
            SELECT a.AccountId, a.UserId, a.LoginName, a.PasswordHash, a.IsEnabled, u.Name
            FROM dbo.Accounts a JOIN dbo.Users u ON u.UserId = a.UserId
            WHERE a.AccountId = @AccountId AND a.UserId = @UserId
            """,
            new { AccountId = accountId, UserId = userId }, cancellationToken: cancellationToken));
        return account is { IsEnabled: true }
            ? await BuildCurrentUserAsync(connection, null, account, cancellationToken)
            : null;
    }

    public async Task<bool> HasFunctionAsync(
        Guid accountId,
        Guid userId,
        string functionCode,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM dbo.Accounts a
                JOIN dbo.UserRoles ur ON ur.UserId = a.UserId AND ur.IsEnabled = 1
                JOIN dbo.Roles r ON r.RoleId = ur.RoleId AND r.IsEnabled = 1
                JOIN dbo.RoleFunctions rf ON rf.RoleId = r.RoleId AND rf.IsEnabled = 1
                JOIN dbo.Functions f ON f.FunctionId = rf.FunctionId AND f.IsEnabled = 1
                WHERE a.AccountId = @AccountId AND a.UserId = @UserId AND a.IsEnabled = 1 AND f.Code = @FunctionCode
            ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
            """,
            new { AccountId = accountId, UserId = userId, FunctionCode = functionCode },
            cancellationToken: cancellationToken));
    }

    private async Task<AuthenticationResult> IssueTokensAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid accountId,
        CurrentUserResponse user,
        Guid familyId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var (accessToken, accessExpiresAt) = tokenGenerator.CreateAccessToken(user, now);
        var refreshToken = TokenGenerator.CreateRefreshToken();
        var refreshExpiresAt = now.AddDays(jwtOptions.RefreshTokenDays);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.RefreshTokens
                (RefreshTokenId, AccountId, TokenHash, FamilyId, ExpiresAt, CreatedAt)
            VALUES
                (@RefreshTokenId, @AccountId, @TokenHash, @FamilyId, @ExpiresAt, @CreatedAt)
            """,
            new
            {
                RefreshTokenId = Guid.NewGuid(),
                AccountId = accountId,
                TokenHash = TokenGenerator.HashRefreshToken(refreshToken),
                FamilyId = familyId,
                ExpiresAt = refreshExpiresAt,
                CreatedAt = now
            }, transaction, cancellationToken: cancellationToken));
        return new AuthenticationResult(accessToken, refreshToken, accessExpiresAt, refreshExpiresAt, user);
    }

    private static async Task<CurrentUserResponse> BuildCurrentUserAsync(
        DbConnection connection,
        DbTransaction? transaction,
        AccountLoginRow account,
        CancellationToken cancellationToken)
    {
        var roles = (await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT DISTINCT r.Code FROM dbo.UserRoles ur
            JOIN dbo.Roles r ON r.RoleId = ur.RoleId
            WHERE ur.UserId = @UserId AND ur.IsEnabled = 1 AND r.IsEnabled = 1
            """,
            new { account.UserId }, transaction, cancellationToken: cancellationToken))).ToArray();
        var functions = (await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT DISTINCT f.Code FROM dbo.UserRoles ur
            JOIN dbo.Roles r ON r.RoleId = ur.RoleId AND r.IsEnabled = 1
            JOIN dbo.RoleFunctions rf ON rf.RoleId = r.RoleId AND rf.IsEnabled = 1
            JOIN dbo.Functions f ON f.FunctionId = rf.FunctionId AND f.IsEnabled = 1
            WHERE ur.UserId = @UserId AND ur.IsEnabled = 1
            """,
            new { account.UserId }, transaction, cancellationToken: cancellationToken))).ToArray();
        return new CurrentUserResponse(account.UserId, account.AccountId, account.LoginName, account.Name, roles, functions);
    }

    private static Task RevokeFamilyAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid familyId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken) => connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.RefreshTokens
            SET RevokedAt = COALESCE(RevokedAt, @Now), RevocationReason = COALESCE(RevocationReason, @Reason)
            WHERE FamilyId = @FamilyId
            """,
            new { FamilyId = familyId, Now = now, Reason = reason }, transaction,
            cancellationToken: cancellationToken));

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private sealed record AccountPasswordSubject(Guid AccountId);
    private sealed record AccountLoginRow(
        Guid AccountId,
        Guid UserId,
        string LoginName,
        string PasswordHash,
        bool IsEnabled,
        string Name);

    private sealed record RefreshTokenRow(
        Guid RefreshTokenId,
        Guid AccountId,
        Guid FamilyId,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? RevokedAt,
        Guid UserId,
        string LoginName,
        string PasswordHash,
        bool IsEnabled,
        string Name);
}
