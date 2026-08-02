using MyWorkItem.Domain;

namespace MyWorkItem.Application;

public interface IAuthRepository
{
    Task<AccessProfile?> GetAccessProfileByUserNameAsync(string userName, CancellationToken cancellationToken);
    Task<AccessProfile?> GetAccessProfileByAccountIdAsync(Guid accountId, CancellationToken cancellationToken);
    Task<RefreshTokenRecord?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task StoreRefreshTokenAsync(RefreshTokenRecord token, CancellationToken cancellationToken);
    Task RotateRefreshTokenAsync(Guid oldTokenId, RefreshTokenRecord replacement, DateTimeOffset revokedAt, CancellationToken cancellationToken);
    Task RevokeTokenFamilyAsync(Guid tokenFamily, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}

public interface IWorkItemRepository
{
    Task<PagedResult<WorkItemRecord>> ListAsync(Guid userId, int page, int pageSize, string? keyword, bool descending, CancellationToken cancellationToken);
    Task<WorkItemRecord?> GetAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken);
    Task<WorkItemRecord> CreateAsync(string title, string? description, Guid userId, CancellationToken cancellationToken);
    Task<WorkItemRecord> UpdateAsync(Guid workItemId, string title, string? description, byte[] rowVersion, Guid userId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid workItemId, Guid userId, byte[]? rowVersion, CancellationToken cancellationToken);
    Task ConfirmAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken);
    Task RevokeConfirmationAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken);
    Task ConfirmBatchAsync(IReadOnlyCollection<Guid> workItemIds, Guid userId, CancellationToken cancellationToken);
}

public interface IAdministrationRepository
{
    Task<PagedResult<UserResponse>> ListUsersAsync(int page, int pageSize, string? keyword, CancellationToken cancellationToken);
    Task<UserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserResponse> CreateUserAsync(CreateUserRequest request, string passwordHash, CancellationToken cancellationToken);
    Task<UserResponse> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken);
    Task SetAccountStatusAsync(Guid userId, bool isEnabled, CancellationToken cancellationToken);
    Task ResetPasswordAsync(Guid userId, string passwordHash, CancellationToken cancellationToken);
    Task ReplaceUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RoleResponse>> ListRolesAsync(CancellationToken cancellationToken);
    Task<RoleResponse> CreateRoleAsync(CreateNamedResourceRequest request, CancellationToken cancellationToken);
    Task<RoleResponse> UpdateRoleAsync(Guid roleId, UpdateNamedResourceRequest request, CancellationToken cancellationToken);
    Task ReplaceRoleFunctionsAsync(Guid roleId, IReadOnlyCollection<Guid> functionIds, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<FunctionResponse>> ListFunctionsAsync(CancellationToken cancellationToken);
    Task<FunctionResponse> CreateFunctionAsync(CreateNamedResourceRequest request, CancellationToken cancellationToken);
    Task<FunctionResponse> UpdateFunctionAsync(Guid functionId, UpdateNamedResourceRequest request, CancellationToken cancellationToken);
}

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}

public interface ITokenService
{
    TokenPair Create(AccessProfile profile, Guid? tokenFamily = null);
    string HashRefreshToken(string refreshToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IAuthService
{
    Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
}

public interface IWorkItemService
{
    Task<PagedResult<WorkItemResponse>> ListAsync(Guid userId, int page, int pageSize, string? keyword, bool descending, CancellationToken cancellationToken);
    Task<WorkItemResponse> GetAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken);
    Task<WorkItemResponse> CreateAsync(CreateWorkItemRequest request, Guid userId, CancellationToken cancellationToken);
    Task<WorkItemResponse> UpdateAsync(Guid workItemId, UpdateWorkItemRequest request, Guid userId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken);
    Task ConfirmAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken);
    Task RevokeConfirmationAsync(Guid workItemId, Guid userId, CancellationToken cancellationToken);
    Task ConfirmBatchAsync(IReadOnlyCollection<Guid> workItemIds, Guid userId, CancellationToken cancellationToken);
}
