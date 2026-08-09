using Microsoft.Extensions.DependencyInjection;
using IsSuas.Application.Abstractions;
using IsSuas.Application.Policies;

namespace IsSuas.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IDelegatedPermissionChecker, NullDelegatedPermissionChecker>();
        services.AddScoped<IAuthorizationPolicyService, AuthorizationPolicyService>();
        return services;
    }
}
