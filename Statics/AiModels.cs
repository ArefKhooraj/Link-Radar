using System.Text.Json.Serialization;

namespace LinkRadar.Api.Statics;

public class AiModels
{
    public const string O4Mini = "o4-mini";
    public const string Gpt52 = "gpt-5.2";
    public const string Gpt5nano = "gpt-5-nano";
    public const string Default = O4Mini;
    public const string Gpt41Mini = "gpt-4.1-mini";
    public const string Gpt5Mini = "gpt-5-mini";
    public const string Gpt54Nano = "gpt-5.4-nano";
    public const string Gpt54Mini = "gpt-5.4-mini";
    public const string Gpt6Luna = "gpt-6-luna";

    public const string Grok3MiniFastBeta = "grok-3-mini-fast-beta";

    public const string GptImage2 = "gpt-image-2";


    public const string Gemini25Flash = "gemini-2.5-flash";
    public const string Gemini25Pro = "gemini-2.5-pro";
    public const string GeminiDefault = Gemini25Flash;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool
}

public enum AiProvider
{
    OpenAi,
    Gemini
}