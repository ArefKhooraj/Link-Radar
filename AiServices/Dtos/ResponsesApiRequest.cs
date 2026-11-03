using System.Text.Json.Serialization;
using LinkRadar.Api.OpenAi;

namespace LinkRadar.Api.AiServices.Dtos;

internal class ResponsesApiRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = null!;
    [JsonPropertyName("tools")] public List<ToolDto>? Tools { get; set; }
    [JsonPropertyName("tool_choice")] public string? ToolChoice { get; set; }
    [JsonPropertyName("input")] public List<ChatMessage> Input { get; set; } = null!;
    [JsonPropertyName("text")] public TextConfig? Text { get; set; }
}

internal class ToolDto
{
    [JsonPropertyName("type")] public string Type { get; set; } = null!;
}

internal class TextConfig
{
    [JsonPropertyName("format")] public TextFormatDto Format { get; set; } = null!;
}

internal class TextFormatDto
{
    [JsonPropertyName("type")] public string Type { get; set; } = null!; // "json_schema"
    [JsonPropertyName("name")] public string Name { get; set; } = null!;
    [JsonPropertyName("schema")] public object Schema { get; set; } = null!; // serialized as-is
}