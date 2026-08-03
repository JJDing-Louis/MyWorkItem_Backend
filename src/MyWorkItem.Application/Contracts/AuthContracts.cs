using System.ComponentModel.DataAnnotations;

namespace MyWorkItem.Application.Contracts;

public sealed record LoginRequest(
    [param: Required, MaxLength(100)] string LoginName,
    [param: Required] string Password);

public sealed record CurrentUserResponse(
    Guid UserId,
    Guid AccountId,
    string LoginName,
    string Name,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Functions);

public sealed record AuthenticationResult(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    CurrentUserResponse User);
