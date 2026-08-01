using System.Data;
using Dapper;
using MyWorkItem.Application;
using MyWorkItem.Domain;

namespace MyWorkItem.Infrastructure;

public sealed class AuthRepository(IDbConnectionFactory connections) : IAuthRepository
{
    public Task<AccessProfile?> GetAccessProfileByUserNameAsync(string userName, CancellationToken cancellationToken) =>
        GetProfileAsync("a.UserName = @Value", userName, cancellationToken);

    public Task<AccessProfile?> GetAccessProfileByAccountIdAsync(Guid accountId, CancellationToken cancellationToken) =>
        GetProfileAsync("a.AccountId = @Value", accountId, cancellationToken);

    public async Task<RefreshTokenRecord?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT RefreshTokenId, AccountId, TokenHash, TokenFamily, ExpiresAt, CreatedAt, RevokedAt, ReplacedByTokenId
            FROM RefreshTokens
            WHERE TokenHash = @TokenHash;
            """;
        await using var connection = connections.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RefreshTokenRecord>(new CommandDefinition(sql, new { TokenHash = tokenHash }, cancellationToken: cancellationToken));
    }

    public async Task StoreRefreshTokenAsync(RefreshTokenRecord token, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO RefreshTokens
                (RefreshTokenId, AccountId, TokenHash, TokenFamily, ExpiresAt, CreatedAt, RevokedAt, ReplacedByTokenId)
            VALUES
                (@RefreshTokenId, @AccountId, @TokenHash, @TokenFamily, @ExpiresAt, @CreatedAt, @RevokedAt, @ReplacedByTokenId);
            """;
        await using var connection = connections.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, token, cancellationToken: cancellationToken));
    }

    public async Task RotateRefreshTokenAsync(Guid oldTokenId, RefreshTokenRecord replacement, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        await using var connection = connections.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        const string revokeSql = """
            UPDATE RefreshTokens
            SET RevokedAt = @RevokedAt, ReplacedByTokenId = @ReplacementId
            WHERE RefreshTokenId = @OldTokenId AND RevokedAt IS NULL;
            """;
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            revokeSql,
            new { RevokedAt = revokedAt, ReplacementId = replacement.RefreshTokenId, OldTokenId = oldTokenId },
            transaction,
            cancellationToken: cancellationToken));
        if (affected != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new UnauthorizedException("Refresh Token 已被使用或撤銷。");
        }

        const string insertSql = """
            INSERT INTO RefreshTokens
                (RefreshTokenId, AccountId, TokenHash, TokenFamily, ExpiresAt, CreatedAt, RevokedAt, ReplacedByTokenId)
            VALUES
                (@RefreshTokenId, @AccountId, @TokenHash, @TokenFamily, @ExpiresAt, @CreatedAt, NULL, NULL);
            """;
        await connection.ExecuteAsync(new CommandDefinition(insertSql, replacement, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeTokenFamilyAsync(Guid tokenFamily, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE RefreshTokens SET RevokedAt = COALESCE(RevokedAt, @RevokedAt) WHERE TokenFamily = @TokenFamily;";
        await using var connection = connections.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TokenFamily = tokenFamily, RevokedAt = revokedAt }, cancellationToken: cancellationToken));
    }

    private async Task<AccessProfile?> GetProfileAsync(string predicate, object value, CancellationToken cancellationToken)
    {
        var sql = $$"""
            SELECT a.AccountId, u.UserId, a.UserName, u.Name, a.PasswordHash, a.IsEnabled,
                   r.Code AS RoleCode, f.Code AS PermissionCode
            FROM Accounts a
            INNER JOIN Users u ON u.AccountId = a.AccountId
            LEFT JOIN AccountRoles ar ON ar.AccountId = a.AccountId
            LEFT JOIN Roles r ON r.RoleId = ar.RoleId AND r.IsEnabled = 1
            LEFT JOIN RoleFunctions rf ON rf.RoleId = r.RoleId
            LEFT JOIN [Functions] f ON f.FunctionId = rf.FunctionId AND f.IsEnabled = 1
            WHERE {{predicate}};
            """;
        await using var connection = connections.CreateConnection();
        var rows = (await connection.QueryAsync<AccessRow>(new CommandDefinition(sql, new { Value = value }, cancellationToken: cancellationToken))).ToArray();
        if (rows.Length == 0)
        {
            return null;
        }

        var first = rows[0];
        return new AccessProfile(
            first.AccountId,
            first.UserId,
            first.UserName,
            first.Name,
            first.PasswordHash,
            first.IsEnabled,
            rows.Where(x => x.RoleCode is not null).Select(x => x.RoleCode!).Distinct(StringComparer.Ordinal).ToArray(),
            rows.Where(x => x.PermissionCode is not null).Select(x => x.PermissionCode!).Distinct(StringComparer.Ordinal).ToArray());
    }

    private sealed class AccessRow
    {
        public Guid AccountId { get; init; }
        public Guid UserId { get; init; }
        public required string UserName { get; init; }
        public required string Name { get; init; }
        public required string PasswordHash { get; init; }
        public bool IsEnabled { get; init; }
        public string? RoleCode { get; init; }
        public string? PermissionCode { get; init; }
    }
}
