using Microsoft.AspNetCore.Authorization;

namespace MyWorkItem.Api.Security;

public sealed record FunctionRequirement(string FunctionCode) : IAuthorizationRequirement;
