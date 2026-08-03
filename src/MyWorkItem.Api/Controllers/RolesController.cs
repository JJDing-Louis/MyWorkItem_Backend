using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWorkItem.Api.Infrastructure;
using MyWorkItem.Application.Abstractions;
using MyWorkItem.Application.Contracts;
using MyWorkItem.Domain.Constants;

namespace MyWorkItem.Api.Controllers;

[ApiController]
[Route("api/v1/roles")]
[Tags("Roles")]
[Authorize(Policy = FunctionCodes.RolesManage)]
public sealed class RolesController(IPermissionAdminService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PermissionItemResponse>>> Get(CancellationToken cancellationToken) =>
        Ok(await service.GetRolesAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PermissionItemResponse>> Create(CreatePermissionItemRequest request, CancellationToken cancellationToken) =>
        Created("/api/v1/roles", await service.CreateRoleAsync(request, cancellationToken));

    [HttpPut("{roleId:guid}")]
    public async Task<ActionResult<PermissionItemResponse>> Update(
        Guid roleId, UpdatePermissionItemRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateRoleAsync(roleId, request, cancellationToken));

    [HttpPut("{roleId:guid}/functions")]
    public async Task<IActionResult> ReplaceFunctions(
        Guid roleId, ReplaceRoleFunctionsRequest request, CancellationToken cancellationToken)
    {
        await service.ReplaceRoleFunctionsAsync(User.GetUserId(), roleId, request.FunctionIds, cancellationToken);
        return NoContent();
    }
}
