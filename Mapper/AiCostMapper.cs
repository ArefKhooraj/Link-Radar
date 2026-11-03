using LinkRadar.Api.OpenAi;
using LinkRadar.Api.Statics;

namespace LinkRadar.Api.Mapper;

public static class AiCostMapper
{
    public static CalculateCost MapToCalculateCost<T>(this AiResponse<T> model) =>
        new()
        {
            InputToken = model.InputTokens,
            OutputToken = model.OutputTokens,
            WebSearchCalls = model.WebSearchCalls,
            Model = model.Model,
        };
}
