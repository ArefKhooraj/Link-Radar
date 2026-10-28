using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LinkRadar.Api.DataForSeoServices;
using LinkRadar.Api.Statics;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace LinkRadar.Api.Configuration;

public static class HttpClientConfiguration
{
    public static IServiceCollection AddDataForSeoConfiguration(this IServiceCollection services)
    {
        services.AddHttpClient<DataForSeoService>(client =>
        {
            client.BaseAddress = new Uri(DataForSeo.BaseAddress);

            var credentials =
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{DataForSeo.Login}:{DataForSeo.Password}"));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            // لیست‌های ۱۰۰۰ ردیفی ممکن است کند باشند؛ این زمان شامل retryها هم می‌شود
            client.Timeout = TimeSpan.FromMinutes(3);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 20,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            AutomaticDecompression = DecompressionMethods.GZip,
            UseCookies = false
        }).AddResilienceHandler("data-for-seo-retry-policy", pipelineBuilder =>
        {
            // پیش‌فرض فقط خطاهای گذرا را retry می‌کند (5xx، 408، 429، قطعی شبکه)
            pipelineBuilder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2)
            });
        });

        return services;
    }
}
