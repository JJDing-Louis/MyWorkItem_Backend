using MyWorkItem.Application.Contracts;

namespace MyWorkItem.Application.Abstractions;

public interface IWorkItemService
{
    Task<PagedResponse<WorkItemResponse>> QueryAsync(Guid userId, WorkItemQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WorkItemUserOptionResponse>> GetUserOptionsAsync(CancellationToken cancellationToken);
    Task<WorkItemResponse?> GetAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken);
    Task<WorkItemResponse> CreateAsync(Guid userId, CreateWorkItemRequest request, CancellationToken cancellationToken);
    Task<WorkItemResponse> UpdateAsync(Guid userId, Guid workItemId, UpdateWorkItemRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken);
    Task ConfirmAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken);
    Task RevokeConfirmationAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken);
    Task ConfirmBatchAsync(Guid userId, IReadOnlyCollection<Guid> workItemIds, CancellationToken cancellationToken);
}
