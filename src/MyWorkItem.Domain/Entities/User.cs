namespace MyWorkItem.Domain.Entities;

public sealed record User(Guid UserId, string Name, string? Email, string? Remark);
