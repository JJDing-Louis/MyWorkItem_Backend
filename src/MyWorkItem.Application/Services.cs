using MyWorkItem.Domain;

namespace MyWorkItem.Application;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class AuthService(IAuthRepository repository, IPasswordService passwords, ITokenService tokens, IClock clock) : IAuthService
{
    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetAccessProfileByUserNameAsync(request.UserName.Trim(), cancellationToken);
        if (profile is null || !profile.IsEnabled || !passwords.Verify(profile.PasswordHash, request.Password))
        {
            throw new UnauthorizedException("帳號或密碼錯誤。");
        }

        return await CreateSessionAsync(profile, null, cancellationToken);
    }

    public async Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var stored = await repository.GetRefreshTokenAsync(tokens.HashRefreshToken(refreshToken), cancellationToken)
            ?? throw new UnauthorizedException("Refresh Token 無效。");

        if (stored.RevokedAt is not null)
        {
            await repository.RevokeTokenFamilyAsync(stored.TokenFamily, clock.UtcNow, cancellationToken);
            throw new UnauthorizedException("偵測到已撤銷的 Refresh Token。");
        }

        if (stored.ExpiresAt <= clock.UtcNow)
        {
            throw new UnauthorizedException("Refresh Token 已過期。");
        }

        var profile = await repository.GetAccessProfileByAccountIdAsync(stored.AccountId, cancellationToken);
        if (profile is null || !profile.IsEnabled)
        {
            throw new UnauthorizedException("帳號不存在或已停用。");
        }

        var pair = tokens.Create(profile, stored.TokenFamily);
        var replacement = ToRefreshRecord(profile.AccountId, pair);
        await repository.RotateRefreshTokenAsync(stored.RefreshTokenId, replacement, clock.UtcNow, cancellationToken);
        return ToSession(profile, pair);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var stored = await repository.GetRefreshTokenAsync(tokens.HashRefreshToken(refreshToken), cancellationToken);
        if (stored is not null)
        {
            await repository.RevokeTokenFamilyAsync(stored.TokenFamily, clock.UtcNow, cancellationToken);
        }
    }

    private async Task<AuthSession> CreateSessionAsync(AccessProfile profile, Guid? family, CancellationToken cancellationToken)
    {
        var pair = tokens.Create(profile, family);
        await repository.StoreRefreshTokenAsync(ToRefreshRecord(profile.AccountId, pair), cancellationToken);
        return ToSession(profile, pair);
    }

    private RefreshTokenRecord ToRefreshRecord(Guid accountId, TokenPair pair) => new(
        pair.RefreshTokenId,
        accountId,
        tokens.HashRefreshToken(pair.RefreshToken),
        pair.TokenFamily,
        pair.RefreshTokenExpiresAt,
        clock.UtcNow,
        null,
        null);

    private static AuthSession ToSession(AccessProfile profile, TokenPair pair) => new(
        new AuthSessionResponse(profile.AccountId, profile.UserId, profile.UserName, profile.Name, profile.Roles, profile.Permissions, pair.AccessTokenExpiresAt),
        pair);
}

public sealed class WorkItemService(IWorkItemRepository repository) : IWorkItemService
{
    public async Task<PagedResult<WorkItemResponse>> ListAsync(Guid userId, int page, int pageSize, string? keyword, bool descending, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await repository.ListAsync(userId, page, pageSize, keyword?.Trim(), descending, cancellationToken);
        return new PagedResult<WorkItemResponse>(result.Items.Select(Map).ToArray(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<WorkItemResponse> GetAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken) =>
        Map(await repository.GetAsync(workItemId, userId, cancellationToken)
            ?? throw new NotFoundException("找不到指定的 Work Item。"));

    public async Task<WorkItemResponse> CreateAsync(CreateWorkItemRequest request, Guid accountId, Guid userId, CancellationToken cancellationToken) =>
        Map(await repository.CreateAsync(request.Title.Trim(), request.Description?.Trim(), accountId, userId, cancellationToken));

    public async Task<WorkItemResponse> UpdateAsync(Guid workItemId, UpdateWorkItemRequest request, Guid userId, CancellationToken cancellationToken)
    {
        byte[] version;
        try
        {
            version = Convert.FromBase64String(request.Version);
        }
        catch (FormatException)
        {
            throw new ValidationException("Version 格式不正確。");
        }

        return Map(await repository.UpdateAsync(workItemId, request.Title.Trim(), request.Description?.Trim(), version, userId, cancellationToken));
    }

    public Task DeleteAsync(Guid workItemId, Guid accountId, CancellationToken cancellationToken) => repository.DeleteAsync(workItemId, accountId, null, cancellationToken);
    public Task ConfirmAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken) => repository.ConfirmAsync(workItemId, userId, cancellationToken);
    public Task RevokeConfirmationAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken) => repository.RevokeConfirmationAsync(workItemId, userId, cancellationToken);
    public Task ConfirmBatchAsync(IReadOnlyCollection<Guid> workItemIds, Guid userId, CancellationToken cancellationToken) => repository.ConfirmBatchAsync(workItemIds.Distinct().ToArray(), userId, cancellationToken);

    private static WorkItemResponse Map(WorkItemRecord item) => new(
        item.WorkItemId,
        item.Title,
        item.Description,
        item.CreatedAt,
        item.UpdatedAt,
        item.IsConfirmed,
        item.ConfirmedAt,
        Convert.ToBase64String(item.RowVersion));
}
