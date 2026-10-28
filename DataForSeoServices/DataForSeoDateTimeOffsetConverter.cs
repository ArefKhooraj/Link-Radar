using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkRadar.Api.DataForSeoServices;

// تاریخ‌های DataForSEO به شکل "2025-03-09 05:22:12 +00:00" هستند که System.Text.Json خودش نمی‌خواند
public class DataForSeoDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd HH:mm:ss zzz";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (DateTimeOffset.TryParseExact(value, Format, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var exact))
            return exact;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                out var parsed))
            return parsed;

        throw new JsonException($"Invalid DataForSeo date value: {value}");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}
