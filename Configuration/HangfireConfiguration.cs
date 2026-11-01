using global::Hangfire;
using LinkRadar.Api.Hangfire;

namespace LinkRadar.Api.Configuration;

public static class HangfireConfiguration
{
    public static IServiceCollection AddHangfireConfiguration(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ProcessEngine>();
        services.AddScoped<ProcessWorker>();
        services.AddScoped<JobScheduler>();

        services.AddHangfire(s => { s.UseSqlServerStorage(configuration.GetConnectionString("LinkRadarConnectionString")); });

        services.AddHangfireServer(options => { options.WorkerCount = Statics.Hangfire.WorkersCount; });

        return services;
    }
}
