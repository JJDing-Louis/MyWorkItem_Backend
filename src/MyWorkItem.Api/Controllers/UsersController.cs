using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWorkItem.Api.Infrastructure;
using MyWorkItem.Application.Abstractions;
using MyWorkItem.Application.Contracts;
using MyWorkItem.Domain.Constants;

namespace MyWorkItem.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Tags("Users")]
[Authorize(Policy = FunctionCodes.UsersManage)]
public sealed class UsersController(IUserAdminService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UserResponse>> Get(Guid userId, CancellationToken cancellationToken)
    {
        var user = await service.GetAsync(userId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await service.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { userId = user.UserId }, user);
    }

    [HttpPut("{userId:guid}")]
    public async Task<ActionResult<UserResponse>> Update(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(userId, request, cancellationToken));

    [HttpPatch("{userId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid userId, SetUserStatusRequest request, CancellationToken cancellationToken)
    {
        await service.SetStatusAsync(userId, request.IsEnabled, cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid userId, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await service.ResetPasswordAsync(userId, request.Password, cancellationToken);
        return NoContent();
    }

    [HttpPut("{userId:guid}/roles")]
    public async Task<IActionResult> ReplaceRoles(Guid userId, ReplaceUserRolesRequest request, CancellationToken cancellationToken)
    {
        await service.ReplaceRolesAsync(User.GetUserId(), userId, request.RoleIds, cancellationToken);
        return NoContent();
    }
}
