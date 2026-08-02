namespace MyWorkItem.Domain.Entities;

public sealed record WorkItemHistory(
    long HistoryId,
    Guid WorkItemId,
    string ActionCode,
    Guid ChangedByUserId,
    DateTimeOffset ChangedAt,
    WorkItem Snapshot);
