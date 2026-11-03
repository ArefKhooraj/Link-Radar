using System.Text.Json.Serialization;

namespace LinkRadar.Api.AiServices.Dtos;

public class UsageDto
{
    [JsonPropertyName("input_tokens")] public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")] public int OutputTokens { get; set; }
}