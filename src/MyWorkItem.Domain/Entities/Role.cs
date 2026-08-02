namespace MyWorkItem.Domain.Entities;

public sealed record Role(Guid RoleId, string Code, string Name, bool IsEnabled);
