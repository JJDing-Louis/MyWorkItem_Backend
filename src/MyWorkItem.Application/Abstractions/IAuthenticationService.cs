using MyWorkItem.Application.Contracts;

namespace MyWorkItem.Application.Abstractions;

public interface IAuthenticationService
{
    Task<AuthenticationResult?> LoginAsync(string loginName, string password, CancellationToken cancellationToken);
    Task<AuthenticationResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
    Task<CurrentUserResponse?> GetCurrentUserAsync(Guid accountId, Guid userId, CancellationToken cancellationToken);
    Task<bool> HasFunctionAsync(Guid accountId, Guid userId, string functionCode, CancellationToken cancellationToken);
}
