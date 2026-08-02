namespace MyWorkItem.Domain;

public sealed class Account
{
    public Guid AccountId { get; init; }
    public required string UserName { get; init; }
    public required string PasswordHash { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class User
{
    public Guid UserId { get; init; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public string? Remark { get; set; }
}

public sealed record AccessProfile(
    Guid AccountId,
    Guid UserId,
    string UserName,
    string Name,
    string PasswordHash,
    bool IsEnabled,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

public sealed record RefreshTokenRecord(
    Guid RefreshTokenId,
    Guid AccountId,
    string TokenHash,
    Guid TokenFamily,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt,
    Guid? ReplacedByTokenId);

public sealed record WorkItemRecord(
    Guid WorkItemId,
    string Title,
    string? Description,
    Guid CreatedUserId,
    Guid? AssignedUserId,
    string WorkItemStatusCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    byte[] RowVersion,
    bool IsConfirmed,
    DateTimeOffset? ConfirmedAt);
