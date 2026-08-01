using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWorkItem.Application;

namespace MyWorkItem.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = PermissionCodes.UsersManage)]
public sealed class UsersController(IAdministrationRepository repository, IPasswordService passwords) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponse>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null, CancellationToken cancellationToken = default) =>
        Ok(await repository.ListUsersAsync(page, pageSize, keyword, cancellationToken));

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UserResponse>> Get(Guid userId, CancellationToken cancellationToken) =>
        Ok(await repository.GetUserAsync(userId, cancellationToken) ?? throw new NotFoundException("找不到指定的使用者。"));

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await repository.CreateUserAsync(request, passwords.Hash(request.Password), cancellationToken);
        return CreatedAtAction(nameof(Get), new { userId = result.UserId }, result);
    }

    [HttpPut("{userId:guid}")]
    public async Task<ActionResult<UserResponse>> Update(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken) =>
        Ok(await repository.UpdateUserAsync(userId, request, cancellationToken));

    [HttpPatch("{userId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid userId, SetAccountStatusRequest request, CancellationToken cancellationToken)
    {
        await repository.SetAccountStatusAsync(userId, request.IsEnabled, cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid userId, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await repository.ResetPasswordAsync(userId, passwords.Hash(request.NewPassword), cancellationToken);
        return NoContent();
    }

    [HttpPut("{userId:guid}/roles")]
    public async Task<IActionResult> ReplaceRoles(Guid userId, ReplaceIdsRequest request, CancellationToken cancellationToken)
    {
        await repository.ReplaceUserRolesAsync(userId, request.Ids, cancellationToken);
        return NoContent();
    }
}
