using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.Interface;
using LinkRadar.Api.Services;
using LinkRadar.Api.Steps;

namespace LinkRadar.Api.Configuration;

public static class ServiceConfiguration
{
    public static IServiceCollection AddServiceConfiguration(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<UserService>();
        services.AddScoped<AnalyzeService>();

        #region Steps

        //Step 1
        services.AddScoped<IProcessStep,CheckDomainSummary>();

        //Step 2
        services.AddScoped<IProcessStep,DetectCompetitor>();

        //Step 3
        services.AddScoped<IProcessStep,GetBacklinks>();

        //Step 4
        services.AddScoped<IProcessStep,GetAnchors>();

        //Step 5
        services.AddScoped<IProcessStep,GetReferringDomains>();
        
        //Step 6
        services.AddScoped<IProcessStep,GetTimeSeries>();

        //Step 7
        services.AddScoped<IProcessStep,GetLinkGap>();

        #endregion

        return services;
    }
}