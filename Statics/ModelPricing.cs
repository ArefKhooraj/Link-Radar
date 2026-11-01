namespace LinkRadar.Api.Statics;

/// <summary>
/// USD pricing for the AI models the app calls, quoted per 1,000,000 tokens.
/// This is the single place to update when a provider changes its rates.
///
/// Sources (verified 2026-06-21):
///   OpenAI  — https://platform.openai.com/docs/pricing (per-model pages)
///   Gemini  — https://ai.google.dev/gemini-api/docs/pricing
///
/// Note: AiService only captures total input/output tokens, so cached-input
/// discounts are intentionally not modeled here — every input token is billed
/// at the full input rate.
/// </summary>
public static class ModelPricing
{
    private const decimal TokensPerUnit = 1_000_000m;

    // OpenAI "Web search (all models)": $10.00 / 1k calls
    private const decimal WebSearchPerThousandCalls = 10.00m;

    /// <summary>
    /// Per-1M-token USD rates for a model. Some models (e.g. Gemini 2.5 Pro) bill
    /// every token at a higher rate once the input crosses a context threshold;
    /// the optional long-context fields capture that.
    /// </summary>
    public sealed record ModelPrice(
        decimal InputPerMillion,
        decimal OutputPerMillion,
        int? LongContextThreshold = null,
        decimal? LongContextInputPerMillion = null,
        decimal? LongContextOutputPerMillion = null);

    // Keyed by the bare model id from AiModels. Dated snapshot ids returned by the
    // API (e.g. "gpt-5.2-2025-12-10") resolve via longest-prefix match in Resolve().
    private static readonly IReadOnlyDictionary<string, ModelPrice> Prices =
        new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase)
        {
            // --- OpenAI ---
            [AiModels.Gpt52] = new(InputPerMillion: 1.75m, OutputPerMillion: 14.00m),
            [AiModels.Gpt5Mini] = new(InputPerMillion: 0.25m, OutputPerMillion: 2.00m),
            [AiModels.Gpt5nano] = new(InputPerMillion: 0.05m, OutputPerMillion: 0.40m),
            [AiModels.Gpt41Mini] = new(InputPerMillion: 0.40m, OutputPerMillion: 1.60m),
            [AiModels.O4Mini] = new(InputPerMillion: 1.10m, OutputPerMillion: 4.40m),
            [AiModels.Gpt54Mini] = new(InputPerMillion: 0.75m, OutputPerMillion: 4.50m),
            [AiModels.Gpt6Luna] = new(
                InputPerMillion: 0.10m,
                OutputPerMillion: 0.50m,
                LongContextThreshold: 272_000,
                LongContextInputPerMillion: 0.20m,
                LongContextOutputPerMillion: 0.75m),

            // --- xAI (called through AvalAI) ---
            // Bare id without "-beta" so both "grok-3-mini-fast" and "grok-3-mini-fast-beta"
            // resolve via prefix match. xAI list price; AvalAI may bill slightly differently.
            ["grok-3-mini-fast"] = new(InputPerMillion: 0.60m, OutputPerMillion: 4.00m),

            // --- Google Gemini ---
            // 2.5 Flash is flat. 2.5 Pro: inputs above 200k tokens push the whole
            // request (input AND output) onto the long-context rate.
            [AiModels.Gemini25Flash] = new(InputPerMillion: 0.30m, OutputPerMillion: 2.50m),
            [AiModels.Gemini25Pro] = new(
                InputPerMillion: 1.25m,
                OutputPerMillion: 10.00m,
                LongContextThreshold: 200_000,
                LongContextInputPerMillion: 2.50m,
                LongContextOutputPerMillion: 15.00m),
        };

    /// <summary>
    /// The cost in USD that a single request incurred, from its token usage and model.
    /// Returns a full-precision <see cref="decimal"/> — no rounding, so sub-cent
    /// requests are never truncated to zero. Throws <see cref="KeyNotFoundException"/>
    /// if the model has no configured price.
    /// </summary>
    /// <param name="inputTokens">Prompt/input tokens billed for the request.</param>
    /// <param name="outputTokens">Completion/output tokens billed for the request.</param>
    /// <param name="model">Model id (bare or dated snapshot, e.g. AiResponse.Model).</param>
    public static decimal CalculateCost(CalculateCost model)
    {
        var price = Resolve(model.Model);

        var inputRate = price.InputPerMillion;
        var outputRate = price.OutputPerMillion;

        if (price.LongContextThreshold is { } threshold && model.InputToken > threshold)
        {
            inputRate = price.LongContextInputPerMillion ?? inputRate;
            outputRate = price.LongContextOutputPerMillion ?? outputRate;
        }

        var tokenCost = (model.InputToken * inputRate + model.OutputToken * outputRate) / TokensPerUnit;
        var webSearchCost = model.WebSearchCalls * WebSearchPerThousandCalls / 1000m;

        return tokenCost + webSearchCost;
    }

    /// <summary>
    /// Like <see cref="CalculateCost"/>, but returns false instead of throwing when the
    /// model has no configured price — so a missing price never fails a generation step.
    /// </summary>
    public static bool TryCalculateCost(CalculateCost model, out decimal cost)
    {
        cost = 0;

        if (string.IsNullOrWhiteSpace(model.Model) || !TryResolve(model.Model, out _))
            return false;

        cost = CalculateCost(model);
        return true;
    }

    private static bool TryResolve(string model, out ModelPrice? price)
    {
        try
        {
            price = Resolve(model);
            return true;
        }
        catch (KeyNotFoundException)
        {
            price = null;
            return false;
        }
    }

    private static ModelPrice Resolve(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model must be provided.", nameof(model));

        if (Prices.TryGetValue(model, out var exact))
            return exact;

        // Longest-prefix match so dated snapshots resolve to their base model.
        var match = Prices.Keys
            .Where(key => model.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(key => key.Length)
            .FirstOrDefault();

        if (match is not null)
            return Prices[match];

        throw new KeyNotFoundException($"No pricing configured for model '{model}'.");
    }
}

public class CalculateCost
{
    public int InputToken { get; set; }
    public int OutputToken { get; set; }
    public int WebSearchCalls { get; set; }
    public string Model { get; set; }
}

public class CalculateCostResponse
{
    public int BrandId { get; set; }
    public decimal Cost { get; set; }
    public int InputToken { get; set; }
    public int OutputToken { get; set; }
    public string Model { get; set; }
}