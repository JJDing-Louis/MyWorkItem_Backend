namespace MyWorkItem.Domain.Entities;

public sealed record UserWorkItemState(
    Guid UserId,
    Guid WorkItemId,
    string StatusCode,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset UpdatedAt);
