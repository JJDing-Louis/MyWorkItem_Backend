namespace MyWorkItem.Domain.Entities;

public sealed record WorkItem(
    Guid WorkItemId,
    string Title,
    string? Description,
    Guid CreatedByUserId,
    Guid? AssignedUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt,
    byte[] RowVersion);
