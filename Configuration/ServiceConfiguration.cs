using LinkRadar.Api.Interface;
using LinkRadar.Api.Services;

namespace LinkRadar.Api.Configuration;

public static class ServiceConfiguration
{
    public static IServiceCollection AddServiceConfiguration(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<UserService>();

        return services;
    }
}