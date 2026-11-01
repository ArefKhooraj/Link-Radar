namespace LinkRadar.Api.Statics;

// location_code کشور در DataForSEO؛ SA و AE با درخواست واقعی تست شده‌اند و بقیه هنوز نه
public static class DataForSeoLocations
{
    public const string LanguageCode = "ar";

    private static readonly IReadOnlyDictionary<string, int> LocationCodes =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["SA"] = 2682,
            ["AE"] = 2784,
            ["EG"] = 2818,
            ["KW"] = 2414,
            ["QA"] = 2634,
            ["BH"] = 2048,
            ["OM"] = 2512,
            ["JO"] = 2400,
            ["IQ"] = 2368,
            ["LB"] = 2422,
            ["MA"] = 2504,
            ["DZ"] = 2012,
            ["TN"] = 2788  
        };

    public static bool TryGetLocationCode(string countryCode, out int locationCode)
        => LocationCodes.TryGetValue(countryCode, out locationCode);
}
