using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinkRadar.Api.AiServices.Dtos;
using LinkRadar.Api.OpenAi;
using LinkRadar.Api.ResultPattern;
using JsonSerializer = System.Text.Json.JsonSerializer;


namespace LinkRadar.Api.AiServices;

public class AiService(HttpClient httpClient, IHttpClientFactory httpClientFactory, ILogger<AiService> logger)
{
    private HttpClient GetClient(AiProvider provider)
    {
        return provider switch
        {
            AiProvider.OpenAi => httpClient,
            AiProvider.Grok => httpClientFactory.CreateClient("Grok"),
            AiProvider.Gemini => httpClientFactory.CreateClient("Gemini"),
            _ => httpClient
        };
    }


    private static readonly JsonSerializerOptions ContentDeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<OperationResult<AiResponse<T>>> GetCompletionV2Async<T>(
        AiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = BuildRequestBody(request);
            var json = JsonSerializer.Serialize(body, SerializerOptions);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "responses")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // Polly حالا خودش Retry رو مدیریت می‌کنه - این خط ممکنه داخلش چند بار تلاش بزنه
            var httpResponse = await GetClient(request.Provider).SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // httpResponse.EnsureSuccessStatusCode();

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("AI API {Status}. Body: {Body}", (int)httpResponse.StatusCode, errorBody);
                throw new Exception($"AI API {(int)httpResponse.StatusCode}: {errorBody}");
            }

            await using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
            var response = await JsonSerializer.DeserializeAsync<ResponsesApiResponse>(
                stream, SerializerOptions, cancellationToken);

            var content = response.Output
                .FirstOrDefault(o => o.Type == "message")?
                .Content?
                .FirstOrDefault(c => c.Type == "output_text")?
                .Text;

            if (string.IsNullOrWhiteSpace(content))
                throw new Exception("AI response content is empty");

            var data = typeof(T) == typeof(string)
                ? (object)content!
                : JsonSerializer.Deserialize<T>(content!, ContentDeserializeOptions)!;

            var result = new AiResponse<T>
            {
                Data = (T)data,
                InputTokens = response.Usage.InputTokens,
                OutputTokens = response.Usage.OutputTokens,
                // طبق مستندات OpenAI فقط action از نوع search هزینه‌ی جست‌وجو دارد
                WebSearchCalls = response.Output.Count(o => o.Type == "web_search_call"
                                                            && o.Action?.Type is null or "search"),
                Model = response.Model
            };


            return OperationResult<AiResponse<T>>.SuccessResult(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI API call failed permanently. Model: {Model}", request.Model);


            return OperationResult<AiResponse<T>>.FailureResult(
                Error.ExternalService(ErrorCodes.Ai.RequestFailed, ex.Message, isTransient: true));
        }
    }


    private static ResponsesApiRequest BuildRequestBody(AiRequest request)
    {
        var input = new List<ChatMessage>();

        if (request.SystemPrompt != null)
            input.Add(new ChatMessage { Role = "system", Content = request.SystemPrompt });

        input.Add(new ChatMessage { Role = "user", Content = request.UserPrompt });

        return new ResponsesApiRequest
        {
            Model = request.Model,
            ToolChoice = request.ToolChoice,
            Tools = request.Tools?.Select(t => new ToolDto { Type = t.Type }).ToList(),
            Input = input,
            Text = request.Schema is null
                ? null
                : new TextConfig
                {
                    Format = new TextFormatDto
                    {
                        Type = "json_schema",
                        Name = request.Schema.Name,
                        Schema = request.Schema.Definition
                    }
                }
        };
    }
}

public enum AiProvider
{
    OpenAi,
    Grok,
    Gemini
}