namespace MyWorkItem.Domain.Entities;

public sealed record Account(Guid AccountId, Guid UserId, string LoginName, bool IsEnabled);
