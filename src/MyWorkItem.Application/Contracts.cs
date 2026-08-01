using System.ComponentModel.DataAnnotations;

namespace MyWorkItem.Application;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalCount);

public sealed record LoginRequest(
    [Required, StringLength(100, MinimumLength = 3)] string UserName,
    [Required, StringLength(128)] string Password);

public sealed record AuthSessionResponse(
    Guid AccountId,
    Guid UserId,
    string UserName,
    string Name,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    DateTimeOffset AccessTokenExpiresAt);

public sealed record CurrentUserResponse(
    Guid AccountId,
    Guid UserId,
    string UserName,
    string Name,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

public sealed record WorkItemResponse(
    Guid WorkItemId,
    string Title,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsConfirmed,
    DateTimeOffset? ConfirmedAt,
    string Version);

public sealed record CreateWorkItemRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [StringLength(4000)] string? Description);

public sealed record UpdateWorkItemRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [StringLength(4000)] string? Description,
    [Required] string Version);

public sealed record BatchConfirmationRequest(
    [Required, MinLength(1), MaxLength(100)] IReadOnlyCollection<Guid> WorkItemIds);

public sealed record CreateUserRequest(
    [Required, StringLength(100, MinimumLength = 3)] string UserName,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    [Required, StringLength(200)] string Name,
    [EmailAddress, StringLength(320)] string? Email,
    [StringLength(1000)] string? Remark,
    [Required, MinLength(1)] IReadOnlyCollection<Guid> RoleIds);

public sealed record UpdateUserRequest(
    [Required, StringLength(200)] string Name,
    [EmailAddress, StringLength(320)] string? Email,
    [StringLength(1000)] string? Remark);

public sealed record SetAccountStatusRequest(bool IsEnabled);

public sealed record ResetPasswordRequest(
    [Required, StringLength(128, MinimumLength = 12)] string NewPassword);

public sealed record ReplaceIdsRequest(
    [Required] IReadOnlyCollection<Guid> Ids);

public sealed record UserResponse(
    Guid UserId,
    Guid AccountId,
    string UserName,
    string Name,
    string? Email,
    string? Remark,
    bool IsEnabled,
    IReadOnlyCollection<NamedReference> Roles);

public sealed record NamedReference(Guid Id, string Code, string Name, bool IsEnabled);

public sealed record CreateNamedResourceRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Code,
    [Required, StringLength(200, MinimumLength = 1)] string Name);

public sealed record UpdateNamedResourceRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    bool IsEnabled);

public sealed record RoleResponse(
    Guid RoleId,
    string Code,
    string Name,
    bool IsEnabled,
    IReadOnlyCollection<NamedReference> Functions);

public sealed record FunctionResponse(Guid FunctionId, string Code, string Name, bool IsEnabled);

public sealed record TokenPair(string AccessToken, string RefreshToken, Guid RefreshTokenId, Guid TokenFamily, DateTimeOffset AccessTokenExpiresAt, DateTimeOffset RefreshTokenExpiresAt);

public sealed record AuthSession(AuthSessionResponse Response, TokenPair Tokens);
