using System.Text.Json.Serialization;

namespace LinkRadar.Api.OpenAi;

public record RequestDto
{
    [JsonPropertyName("model")] public string Model { get; set; }

    [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; }
}

public class ChatMessage
{
    [JsonPropertyName("role")] public string Role { get; set; }

    [JsonPropertyName("content")] public string Content { get; set; }
}