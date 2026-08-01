using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWorkItem.Application;

namespace MyWorkItem.Api.Controllers;

[ApiController]
[Route("api/v1/functions")]
[Authorize(Policy = PermissionCodes.FunctionsManage)]
public sealed class FunctionsController(IAdministrationRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<FunctionResponse>>> List(CancellationToken cancellationToken) => Ok(await repository.ListFunctionsAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<FunctionResponse>> Create(CreateNamedResourceRequest request, CancellationToken cancellationToken) => StatusCode(StatusCodes.Status201Created, await repository.CreateFunctionAsync(request, cancellationToken));

    [HttpPut("{functionId:guid}")]
    public async Task<ActionResult<FunctionResponse>> Update(Guid functionId, UpdateNamedResourceRequest request, CancellationToken cancellationToken) => Ok(await repository.UpdateFunctionAsync(functionId, request, cancellationToken));
}
