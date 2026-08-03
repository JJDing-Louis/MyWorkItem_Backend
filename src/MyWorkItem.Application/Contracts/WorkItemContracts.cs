using System.ComponentModel.DataAnnotations;

namespace MyWorkItem.Application.Contracts;

public sealed record WorkItemQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    string SortDirection = "desc",
    Guid? AssignedUserId = null);

public sealed record CreateWorkItemRequest(
    [param: Required, MaxLength(200)] string Title,
    string? Description,
    Guid? AssignedUserId);

public sealed record UpdateWorkItemRequest(
    [param: Required, MaxLength(200)] string Title,
    string? Description,
    Guid? AssignedUserId,
    [param: Required] string RowVersion);

public sealed record BatchConfirmationRequest(
    [param: Required, MinLength(1), MaxLength(100)] IReadOnlyCollection<Guid> WorkItemIds);

public sealed record WorkItemResponse(
    Guid WorkItemId,
    string Title,
    string? Description,
    Guid CreatedByUserId,
    Guid? AssignedUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string StatusCode,
    bool IsConfirmed,
    DateTimeOffset? ConfirmedAt,
    string RowVersion);

public sealed record WorkItemUserOptionResponse(
    Guid UserId,
    string LoginName,
    string Name,
    bool IsEnabled);

public sealed record PagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
