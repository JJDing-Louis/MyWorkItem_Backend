using Microsoft.AspNetCore.Authorization;
using MyWorkItem.Api.Infrastructure;
using MyWorkItem.Application.Abstractions;

namespace MyWorkItem.Api.Security;

public sealed class FunctionAuthorizationHandler(IAuthenticationService authenticationService)
    : AuthorizationHandler<FunctionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FunctionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (await authenticationService.HasFunctionAsync(
                context.User.GetAccountId(), context.User.GetUserId(), requirement.FunctionCode, CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}
