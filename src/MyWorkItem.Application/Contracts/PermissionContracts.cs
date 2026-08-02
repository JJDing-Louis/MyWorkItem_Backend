using System.ComponentModel.DataAnnotations;

namespace MyWorkItem.Application.Contracts;

public sealed record CreatePermissionItemRequest(
    [param: Required, MaxLength(100)] string Code,
    [param: Required, MaxLength(200)] string Name,
    [param: MaxLength(1000)] string? Description);

public sealed record UpdatePermissionItemRequest(
    [param: Required, MaxLength(200)] string Name,
    [param: MaxLength(1000)] string? Description,
    bool IsEnabled);

public sealed record ReplaceRoleFunctionsRequest([param: Required] IReadOnlyCollection<Guid> FunctionIds);

public sealed record PermissionItemResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsEnabled,
    IReadOnlyCollection<LookupResponse>? Functions = null);
