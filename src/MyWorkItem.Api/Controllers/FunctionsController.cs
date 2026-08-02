using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWorkItem.Application.Abstractions;
using MyWorkItem.Application.Contracts;
using MyWorkItem.Domain.Constants;

namespace MyWorkItem.Api.Controllers;

[ApiController]
[Route("api/v1/functions")]
[Tags("Functions")]
[Authorize(Policy = FunctionCodes.FunctionsManage)]
public sealed class FunctionsController(IPermissionAdminService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PermissionItemResponse>>> Get(CancellationToken cancellationToken) =>
        Ok(await service.GetFunctionsAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PermissionItemResponse>> Create(CreatePermissionItemRequest request, CancellationToken cancellationToken) =>
        Created("/api/v1/functions", await service.CreateFunctionAsync(request, cancellationToken));

    [HttpPut("{functionId:guid}")]
    public async Task<ActionResult<PermissionItemResponse>> Update(
        Guid functionId, UpdatePermissionItemRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateFunctionAsync(functionId, request, cancellationToken));
}
