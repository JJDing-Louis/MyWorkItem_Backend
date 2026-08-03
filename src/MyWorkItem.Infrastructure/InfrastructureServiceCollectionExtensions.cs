using Microsoft.Extensions.DependencyInjection;
using MyWorkItem.Application.Abstractions;
using MyWorkItem.Infrastructure.Data;
using MyWorkItem.Infrastructure.Security;
using MyWorkItem.Infrastructure.Services;

namespace MyWorkItem.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMyWorkItemInfrastructure(
        this IServiceCollection services,
        string connectionString,
        JwtOptions jwtOptions)
    {
        services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connectionString));
        services.AddSingleton(jwtOptions);
        services.AddSingleton<TokenGenerator>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IWorkItemService, WorkItemService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IPermissionAdminService, PermissionAdminService>();
        return services;
    }
}
