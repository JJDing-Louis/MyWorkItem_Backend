using MyWorkItem.Application.Contracts;

namespace MyWorkItem.Application.Abstractions;

public interface IUserAdminService
{
    Task<IReadOnlyCollection<UserResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<UserResponse?> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<UserResponse> CreateAsync(Guid currentUserId, CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserResponse> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken);
    Task SetStatusAsync(Guid userId, bool isEnabled, CancellationToken cancellationToken);
    Task ResetPasswordAsync(Guid userId, string password, CancellationToken cancellationToken);
    Task ReplaceRolesAsync(Guid currentUserId, Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);
}
