using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LinkRadar.Api.AiServices;
using LinkRadar.Api.DataForSeoServices;
using LinkRadar.Api.Statics;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace LinkRadar.Api.Configuration;

public static class HttpClientConfiguration
{
    public static IServiceCollection AddOpenAiConfiguration(this IServiceCollection services)
    {
        services.AddHttpClient<AiService>(client =>
        {
            client.BaseAddress = new Uri(Statics.OpenAi.BaseAddress);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", Statics.OpenAi.Apikey);
            client.Timeout = TimeSpan.FromMinutes(5);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 20,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            AutomaticDecompression = DecompressionMethods.GZip,
            UseCookies = false
        }).AddResilienceHandler("ai-retry-policy", pipelineBuilder =>
        {
            pipelineBuilder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(response =>
                        response.StatusCode == HttpStatusCode.RequestTimeout ||
                        response.StatusCode == HttpStatusCode.TooManyRequests ||
                        response.StatusCode == HttpStatusCode.InternalServerError ||
                        response.StatusCode == HttpStatusCode.BadGateway ||
                        response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                        response.StatusCode == HttpStatusCode.GatewayTimeout)
            });
        });
        return services;
    }

    public static IServiceCollection AddAvalAiConfiguration(this IServiceCollection services)
    {
        services.AddHttpClient("Grok", client =>
        {
            client.BaseAddress = new Uri(AvalAi.BaseAddress);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", AvalAi.Apikey);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 20,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            AutomaticDecompression = DecompressionMethods.GZip,
            UseCookies = false
        });

        return services;
    }

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
