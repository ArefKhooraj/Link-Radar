using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinkRadar.Api.Dtos.DataForSeo;
using LinkRadar.Api.ResultPattern;

namespace LinkRadar.Api.DataForSeoServices;

public class DataForSeoService(HttpClient httpClient, ILogger<DataForSeoService> logger)
{
    private const int SuccessStatusCode = 20000;

    // کدهای DataForSEO که با تلاش دوباره درست می‌شوند (محدودیت نرخ)؛ 5xxxx هم همیشه گذراست
    private static readonly HashSet<int> TransientDataForSeoCodes = [40202];

    // فیلدهای DataForSEO snake_case هستند؛ DTOها بدون JsonPropertyName مپ می‌شوند و فیلدهای اضافه نادیده گرفته می‌شوند
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new DataForSeoDateTimeOffsetConverter() }
    };

    public async Task<OperationResult<DataForSeoCallResult<TResult>>> PostAsync<TResult>(
        string endpoint,
        object task,
        CancellationToken cancellationToken)
    {
        // بدنه همیشه آرایه است و هر Live call فقط یک task می‌گیرد
        var requestJson = JsonSerializer.Serialize(new[] { task }, JsonOptions);

        var callResult = new DataForSeoCallResult<TResult>
        {
            Endpoint = endpoint,
            RequestJson = requestJson
        };

        try
        {
            logger.LogInformation("Send Request To DataForSeo Endpoint = {Endpoint}, Body = {Body}",
                endpoint, requestJson);

            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            using var httpResponse = await httpClient.PostAsync(endpoint, content, cancellationToken);

            callResult.HttpStatusCode = (int)httpResponse.StatusCode;
            callResult.ResponseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                logger.LogError(
                    "DataForSeo Http Error For Endpoint = {Endpoint}, StatusCode = {StatusCode}, Body = {Body}",
                    endpoint, callResult.HttpStatusCode, callResult.ResponseJson);

                return Failure(callResult, ErrorCodes.DataForSeo.HttpError, $"HTTP {callResult.HttpStatusCode}",
                    IsTransientHttpStatus(httpResponse.StatusCode));
            }

            var response =
                JsonSerializer.Deserialize<DataForSeoResponse<TResult>>(callResult.ResponseJson, JsonOptions);

            if (response is null || response.StatusCode != SuccessStatusCode)
            {
                callResult.StatusCode = response?.StatusCode;
                callResult.StatusMessage = response?.StatusMessage;

                logger.LogError(
                    "DataForSeo Top Level Error For Endpoint = {Endpoint}, StatusCode = {StatusCode}, Message = {Message}",
                    endpoint, response?.StatusCode, response?.StatusMessage);

                return Failure(callResult, ErrorCodes.DataForSeo.StatusError,
                    $"Top level status {response?.StatusCode}: {response?.StatusMessage}",
                    IsTransientDataForSeoCode(response?.StatusCode));
            }

            var dfsTask = response.Tasks?.FirstOrDefault();

            callResult.StatusCode = dfsTask?.StatusCode;
            callResult.StatusMessage = dfsTask?.StatusMessage;
            callResult.Cost = dfsTask?.Cost ?? response.Cost;

            if (dfsTask is null || dfsTask.StatusCode != SuccessStatusCode)
            {
                logger.LogError(
                    "DataForSeo Task Error For Endpoint = {Endpoint}, StatusCode = {StatusCode}, Message = {Message}",
                    endpoint, dfsTask?.StatusCode, dfsTask?.StatusMessage);

                return Failure(callResult, ErrorCodes.DataForSeo.TaskError,
                    $"Task status {dfsTask?.StatusCode}: {dfsTask?.StatusMessage}",
                    IsTransientDataForSeoCode(dfsTask?.StatusCode));
            }

            callResult.Results = dfsTask.Result ?? [];

            logger.LogInformation(
                "DataForSeo Request Completed For Endpoint = {Endpoint}, Cost = {Cost}, Time = {Time}, ResultCount = {ResultCount}",
                endpoint, callResult.Cost, dfsTask.Time, callResult.Results.Count);

            return OperationResult<DataForSeoCallResult<TResult>>.SuccessResult(callResult);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException e)
        {
            // شکل پاسخ با DTO جور نیست؛ تکرار درخواست درستش نمی‌کند
            logger.LogError(e, "DataForSeo Response Could Not Be Parsed For Endpoint = {Endpoint}", endpoint);

            return Failure(callResult, ErrorCodes.DataForSeo.InvalidResponse, e.Message, isTransient: false);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // قطعی شبکه یا timeout
            logger.LogError(e, "DataForSeo Request Failed For Endpoint = {Endpoint} With This Message {Message}",
                endpoint, e.Message);

            return Failure(callResult, ErrorCodes.DataForSeo.RequestFailed, e.Message, isTransient: true);
        }
    }

    private static OperationResult<DataForSeoCallResult<TResult>> Failure<TResult>(
        DataForSeoCallResult<TResult> callResult, string code, string message, bool isTransient)
        => OperationResult<DataForSeoCallResult<TResult>>.FailureResult(
            Error.ExternalService(code, message, isTransient), callResult);

    private static bool IsTransientHttpStatus(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static bool IsTransientDataForSeoCode(int? statusCode)
        => statusCode is >= 50000 || (statusCode.HasValue && TransientDataForSeoCodes.Contains(statusCode.Value));
}
