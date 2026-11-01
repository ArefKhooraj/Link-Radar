using LinkRadar.Api.Services;

namespace LinkRadar.Api.EndpointFilters;

public sealed class ApiKeyFilter(UserService userService) : IEndpointFilter
{
    private const string HeaderName = "ApiKey";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var values))
            return TypedResults.Unauthorized();

        var apiKey = values.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(apiKey))
            return TypedResults.Unauthorized();

        var userValidate = await userService.ValidateApiKeyAsync(apiKey, context.HttpContext.RequestAborted);

        if (!userValidate.IsSuccess)
            return TypedResults.Unauthorized();

        // CurrentUser شناسه را از همین‌جا می‌خواند
        context.HttpContext.Items["UserId"] = userValidate.Result;

        return await next(context);
    }
}
