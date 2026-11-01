using LinkRadar.Api.EndpointFilters;
using LinkRadar.Api.EndpointHandlers;

namespace LinkRadar.Api.Endpoints;

public static class Endpoints
{
    public static WebApplication RegisterAllEndpoints(this WebApplication application)
    {
        application.RegisterAnalysisEndpoints();
        application.RegisterUserEndpoints();
        return application;
    }

    public static WebApplication RegisterAnalysisEndpoints(this WebApplication application)
    {
        var group = application.MapGroup("/analysis").AddEndpointFilter<ApiKeyFilter>();
        group.MapPost("/request", AnalysisEndpointHandlers.RequestAnalysis);
        return application;
    }

    public static WebApplication RegisterUserEndpoints(this WebApplication application)
    {
        var group = application.MapGroup("/user");
        group.MapPost("/", UserEndpointHandler.CreateUser);
        group.MapGet("/", UserEndpointHandler.GetUser).AddEndpointFilter<ApiKeyFilter>();
        group.MapPut("/", UserEndpointHandler.UpdateUser).AddEndpointFilter<ApiKeyFilter>();
        group.MapDelete("/", UserEndpointHandler.DeleteUser).AddEndpointFilter<ApiKeyFilter>();
        return application;
    }
}