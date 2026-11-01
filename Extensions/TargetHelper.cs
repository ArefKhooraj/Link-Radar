using LinkRadar.Api.Entities;

namespace LinkRadar.Api.Extensions;

public static class TargetHelper
{
    public static bool TryParse(string input, out string target, out TargetType targetType)
    {
        target = string.Empty;
        targetType = TargetType.Domain;

        var value = input.Trim();

        if (!value.Contains("://"))
            value = $"https://{value}";

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !uri.Host.Contains('.'))
            return false;

        if (uri.AbsolutePath == "/")
        {
            var host = uri.Host.ToLowerInvariant();
            target = host.StartsWith("www.") ? host[4..] : host;
            return true;
        }

        targetType = TargetType.Page;
        target = uri.GetLeftPart(UriPartial.Query);
        return true;
    }
}