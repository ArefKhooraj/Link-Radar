namespace LinkRadar.Api.OpenAi;

public class AiResponse<T>
{
    public T Data { get; set; } = default!;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int WebSearchCalls { get; set; }

    public string Model { get; set; } = null!;
}