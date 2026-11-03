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

        // Scoped تا Context داخلش همان DbContext موتور باشد و تغییرات processState ذخیره شود
        services.AddScoped(typeof(CommonServices<>));

        #region Steps

        //Step 1
        services.AddScoped<IProcessStep,CheckDomainSummary>();

        //Step 2
        services.AddScoped<IProcessStep,DetectCompetitor>();

        //Step 3
        services.AddScoped<IProcessStep,GetBacklinks>();

        //Step 3.1
        services.AddScoped<IProcessStep,GetLostBacklinks>();

        //Step 4
        services.AddScoped<IProcessStep,GetAnchors>();

        //Step 4.1
        services.AddScoped<IProcessStep,DetectBrandAliases>();

        //Step 5
        services.AddScoped<IProcessStep,GetReferringDomains>();
        
        //Step 6
        services.AddScoped<IProcessStep,GetTimeSeries>();

        //Step 7
        services.AddScoped<IProcessStep,GetLinkGap>();

        //Step 8
        services.AddScoped<IProcessStep,GetCompetitorSummaries>();

        //Step 9
        services.AddScoped<IProcessStep,ComputeMetrics>();

        //Step 10
        services.AddScoped<IProcessStep,GetOpportunityPages>();

        //Step 11
        services.AddScoped<IProcessStep,ClassifyOpportunities>();

        //Step 12
        services.AddScoped<IProcessStep,GenerateAiAnalysis>();

        //Step 13
        services.AddScoped<IProcessStep,SendToUserWebhook>();

        #endregion

        return services;
    }
}