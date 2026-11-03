using System.Text.Json.Serialization;

namespace LinkRadar.Api.AiServices.Dtos;

internal class ResponsesApiResponse
{
    [JsonPropertyName("id")] public string Id { get; set; } = null!;

    [JsonPropertyName("model")] public string Model { get; set; } = null!;

    [JsonPropertyName("output")] public List<OutputItem> Output { get; set; } = [];

    [JsonPropertyName("usage")] public UsageDto Usage { get; set; } = null!;
}

internal class OutputItem
{
    [JsonPropertyName("type")] public string Type { get; set; } = null!; // "message" | "web_search_call"
    [JsonPropertyName("content")] public List<ContentPart> Content { get; set; } = [];
    [JsonPropertyName("action")] public WebSearchAction? Action { get; set; }
}

internal class WebSearchAction
{
    [JsonPropertyName("type")] public string Type { get; set; } = null!; // "search" | "open_page" | "find_in_page"
}

internal class ContentPart
{
    [JsonPropertyName("type")] public string Type { get; set; } = null!; // "output_text"
    [JsonPropertyName("text")] public string Text { get; set; } = null!;
}