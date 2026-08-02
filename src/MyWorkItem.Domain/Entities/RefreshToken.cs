namespace MyWorkItem.Domain.Entities;

public sealed record RefreshToken(
    Guid RefreshTokenId,
    Guid AccountId,
    Guid FamilyId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt);
