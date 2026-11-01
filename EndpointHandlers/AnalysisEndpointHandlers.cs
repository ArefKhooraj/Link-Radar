using FluentValidation;
using LinkRadar.Api.Dtos.Analyze;
using LinkRadar.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LinkRadar.Api.EndpointHandlers;

public class AnalysisEndpointHandlers
{
    public static async Task<Results<Ok, BadRequest<ProblemDetails>, ValidationProblem>> RequestAnalysis(
        AnalyzeRequestDto requestDto,
        ILogger<AnalysisEndpointHandlers> logger, AnalyzeService analyzeService,
        IValidator<AnalyzeRequestDto> validator, CancellationToken cancellationToken)
    {
        logger.LogInformation("Start Request Analysis For This EntityId = {EntityId}", requestDto.EntityId);

        var validation = await validator.ValidateAsync(requestDto, cancellationToken);

        if (!validation.IsValid)
        {
            logger.LogWarning("Invalid Request Analysis For This EntityId = {EntityId}, Errors = {Errors}",
                requestDto.EntityId, validation.ToString());

            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var result = await analyzeService.CreateAnalyzeAsync(requestDto, cancellationToken);

        if (!result.IsSuccess)
        {
            var error = result.Errors[0];

            logger.LogWarning(
                "Request Analysis Failed For This EntityId = {EntityId}, ErrorCode = {ErrorCode}, Message = {Message}",
                requestDto.EntityId, error.Code, error.Message);

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = error.Code,
                Detail = error.Message
            });
        }

        logger.LogInformation("Complete Request Analysis For This EntityId = {EntityId}", requestDto.EntityId);

        return TypedResults.Ok();
    }
}