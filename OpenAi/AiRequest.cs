using LinkRadar.Api.Statics;
using AiProvider = LinkRadar.Api.AiServices.AiProvider;

namespace LinkRadar.Api.OpenAi;

/// <summary>
/// Represents a request to the AI service.
/// When Tools or Schema is provided, the Responses API is used instead of Chat Completions.
/// </summary>
public record AiRequest(
    string? SystemPrompt,
    string UserPrompt,
    string Model = AiModels.Default,
    IReadOnlyList<AiTool>? Tools = null,
    string? ToolChoice = null,
    double? Temperature = null,
    AiResponseSchema? Schema = null,
    AiProvider Provider = AiProvider.OpenAi
);

/// <summary>e.g. new AiTool("web_search")</summary>
public record AiTool(string Type);

/// <summary>
/// Structured output schema for the Responses API.
/// Pass the JSON schema as an anonymous object or a typed C# object — it will be serialized as-is.
/// </summary>
public record AiResponseSchema(string Name, object Definition);