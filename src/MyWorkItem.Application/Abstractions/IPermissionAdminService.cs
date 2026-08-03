using MyWorkItem.Application.Contracts;

namespace MyWorkItem.Application.Abstractions;

public interface IPermissionAdminService
{
    Task<IReadOnlyCollection<PermissionItemResponse>> GetRolesAsync(CancellationToken cancellationToken);
    Task<PermissionItemResponse> CreateRoleAsync(CreatePermissionItemRequest request, CancellationToken cancellationToken);
    Task<PermissionItemResponse> UpdateRoleAsync(Guid roleId, UpdatePermissionItemRequest request, CancellationToken cancellationToken);
    Task ReplaceRoleFunctionsAsync(Guid currentUserId, Guid roleId, IReadOnlyCollection<Guid> functionIds, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PermissionItemResponse>> GetFunctionsAsync(CancellationToken cancellationToken);
    Task<PermissionItemResponse> CreateFunctionAsync(CreatePermissionItemRequest request, CancellationToken cancellationToken);
    Task<PermissionItemResponse> UpdateFunctionAsync(Guid functionId, UpdatePermissionItemRequest request, CancellationToken cancellationToken);
}
