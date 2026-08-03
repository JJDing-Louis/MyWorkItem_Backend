using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWorkItem.Api.Infrastructure;
using MyWorkItem.Application.Abstractions;
using MyWorkItem.Application.Contracts;
using MyWorkItem.Domain.Constants;

namespace MyWorkItem.Api.Controllers;

[ApiController]
[Route("api/v1/work-items")]
[Tags("WorkItems")]
public sealed class WorkItemsController(IWorkItemService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = FunctionCodes.WorkItemsRead)]
    [ProducesResponseType<PagedResponse<WorkItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<WorkItemResponse>>> Query(
        [FromQuery] WorkItemQuery query, CancellationToken cancellationToken) =>
        Ok(await service.QueryAsync(User.GetUserId(), query, cancellationToken));

    [HttpGet("user-options")]
    [Authorize(Policy = FunctionCodes.WorkItemsRead)]
    [ProducesResponseType<IReadOnlyCollection<WorkItemUserOptionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<WorkItemUserOptionResponse>>> GetUserOptions(
        CancellationToken cancellationToken) =>
        Ok(await service.GetUserOptionsAsync(cancellationToken));

    [HttpGet("{workItemId:guid}")]
    [Authorize(Policy = FunctionCodes.WorkItemsRead)]
    public async Task<ActionResult<WorkItemResponse>> Get(Guid workItemId, CancellationToken cancellationToken)
    {
        var item = await service.GetAsync(User.GetUserId(), workItemId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = FunctionCodes.WorkItemsManage)]
    [ProducesResponseType<WorkItemResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkItemResponse>> Create(CreateWorkItemRequest request, CancellationToken cancellationToken)
    {
        var item = await service.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { workItemId = item.WorkItemId }, item);
    }

    [HttpPut("{workItemId:guid}")]
    [Authorize(Policy = FunctionCodes.WorkItemsManage)]
    [ProducesResponseType<WorkItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkItemResponse>> Update(
        Guid workItemId, UpdateWorkItemRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(User.GetUserId(), workItemId, request, cancellationToken));

    [HttpDelete("{workItemId:guid}")]
    [Authorize(Policy = FunctionCodes.WorkItemsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid workItemId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(User.GetUserId(), workItemId, cancellationToken);
        return NoContent();
    }

    [HttpPut("{workItemId:guid}/confirmation")]
    [Authorize(Policy = FunctionCodes.WorkItemsConfirm)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Confirm(Guid workItemId, CancellationToken cancellationToken)
    {
        await service.ConfirmAsync(User.GetUserId(), workItemId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{workItemId:guid}/confirmation")]
    [Authorize(Policy = FunctionCodes.WorkItemsConfirm)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeConfirmation(Guid workItemId, CancellationToken cancellationToken)
    {
        await service.RevokeConfirmationAsync(User.GetUserId(), workItemId, cancellationToken);
        return NoContent();
    }

    [HttpPost("confirmations/batch")]
    [Authorize(Policy = FunctionCodes.WorkItemsConfirm)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ConfirmBatch(BatchConfirmationRequest request, CancellationToken cancellationToken)
    {
        await service.ConfirmBatchAsync(User.GetUserId(), request.WorkItemIds, cancellationToken);
        return NoContent();
    }
}
