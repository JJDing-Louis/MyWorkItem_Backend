using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWorkItem.Application;

namespace MyWorkItem.Api.Controllers;

[ApiController]
[Route("api/v1/work-items")]
[Authorize(Policy = PermissionCodes.WorkItemsRead)]
public sealed class WorkItemsController(IWorkItemService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<WorkItemResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string direction = "desc",
        CancellationToken cancellationToken = default) =>
        Ok(await service.ListAsync(User.GetUserId(), page, pageSize, keyword, !string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase), cancellationToken));

    [HttpGet("{workItemId:guid}")]
    public async Task<ActionResult<WorkItemResponse>> Get(Guid workItemId, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(workItemId, User.GetUserId(), cancellationToken));

    [HttpPost]
    [Authorize(Policy = PermissionCodes.WorkItemsManage)]
    public async Task<ActionResult<WorkItemResponse>> Create(CreateWorkItemRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, User.GetAccountId(), User.GetUserId(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { workItemId = result.WorkItemId }, result);
    }

    [HttpPut("{workItemId:guid}")]
    [Authorize(Policy = PermissionCodes.WorkItemsManage)]
    public async Task<ActionResult<WorkItemResponse>> Update(Guid workItemId, UpdateWorkItemRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(workItemId, request, User.GetUserId(), cancellationToken));

    [HttpDelete("{workItemId:guid}")]
    [Authorize(Policy = PermissionCodes.WorkItemsManage)]
    public async Task<IActionResult> Delete(Guid workItemId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(workItemId, User.GetAccountId(), cancellationToken);
        return NoContent();
    }

    [HttpPut("{workItemId:guid}/confirmation")]
    [Authorize(Policy = PermissionCodes.WorkItemsConfirm)]
    public async Task<IActionResult> Confirm(Guid workItemId, CancellationToken cancellationToken)
    {
        await service.ConfirmAsync(workItemId, User.GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{workItemId:guid}/confirmation")]
    [Authorize(Policy = PermissionCodes.WorkItemsConfirm)]
    public async Task<IActionResult> RevokeConfirmation(Guid workItemId, CancellationToken cancellationToken)
    {
        await service.RevokeConfirmationAsync(workItemId, User.GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpPost("confirmations/batch")]
    [Authorize(Policy = PermissionCodes.WorkItemsConfirm)]
    public async Task<IActionResult> ConfirmBatch(BatchConfirmationRequest request, CancellationToken cancellationToken)
    {
        await service.ConfirmBatchAsync(request.WorkItemIds, User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
