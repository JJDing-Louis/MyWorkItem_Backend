using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWorkItem.Application;

namespace MyWorkItem.Api.Controllers;

[ApiController]
[Route("api/v1/roles")]
[Authorize(Policy = PermissionCodes.RolesManage)]
public sealed class RolesController(IAdministrationRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<RoleResponse>>> List(CancellationToken cancellationToken) => Ok(await repository.ListRolesAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<RoleResponse>> Create(CreateNamedResourceRequest request, CancellationToken cancellationToken) => StatusCode(StatusCodes.Status201Created, await repository.CreateRoleAsync(request, cancellationToken));

    [HttpPut("{roleId:guid}")]
    public async Task<ActionResult<RoleResponse>> Update(Guid roleId, UpdateNamedResourceRequest request, CancellationToken cancellationToken) => Ok(await repository.UpdateRoleAsync(roleId, request, cancellationToken));

    [HttpPut("{roleId:guid}/functions")]
    public async Task<IActionResult> ReplaceFunctions(Guid roleId, ReplaceIdsRequest request, CancellationToken cancellationToken)
    {
        await repository.ReplaceRoleFunctionsAsync(roleId, request.Ids, cancellationToken);
        return NoContent();
    }
}
