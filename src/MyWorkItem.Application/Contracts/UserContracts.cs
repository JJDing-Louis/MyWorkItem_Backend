using System.ComponentModel.DataAnnotations;

namespace MyWorkItem.Application.Contracts;

public sealed record CreateUserRequest(
    [param: Required, MaxLength(100)] string LoginName,
    [param: Required, MinLength(12)] string Password,
    [param: Required, MaxLength(200)] string Name,
    [param: EmailAddress, MaxLength(320)] string? Email,
    [param: MaxLength(1000)] string? Remark,
    IReadOnlyCollection<Guid> RoleIds);

public sealed record UpdateUserRequest(
    [param: Required, MaxLength(200)] string Name,
    [param: EmailAddress, MaxLength(320)] string? Email,
    [param: MaxLength(1000)] string? Remark);

public sealed record SetUserStatusRequest(bool IsEnabled);

public sealed record ResetPasswordRequest([param: Required, MinLength(12)] string Password);

public sealed record ReplaceUserRolesRequest([param: Required] IReadOnlyCollection<Guid> RoleIds);

public sealed record UserResponse(
    Guid UserId,
    Guid AccountId,
    string LoginName,
    string Name,
    string? Email,
    string? Remark,
    bool IsEnabled,
    IReadOnlyCollection<LookupResponse> Roles);

public sealed record LookupResponse(Guid Id, string Code, string Name, bool IsEnabled);
