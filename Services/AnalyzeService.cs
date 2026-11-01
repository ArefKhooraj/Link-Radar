using LinkRadar.Api.Context;
using LinkRadar.Api.Dtos.Analyze;
using LinkRadar.Api.Dtos.ProcessJob;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Extensions;
using LinkRadar.Api.Hangfire;
using LinkRadar.Api.Interface;
using LinkRadar.Api.ResultPattern;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Services;

public class AnalyzeService(
    LinkRadarDbContext context,
    ILogger<AnalyzeService> logger,
    JobScheduler jobScheduler,
    ICurrentUser currentUser)
{
    public async Task<OperationResult> CreateAnalyzeAsync(AnalyzeRequestDto requestDto,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Start Create Analyze For This EntityId = {EntityId}, Domain = {Domain}",
            requestDto.EntityId, requestDto.Domain);

        try
        {
            if (!TargetHelper.TryParse(requestDto.Domain, out var target, out var targetType))
            {
                logger.LogWarning("Invalid Domain For This EntityId = {EntityId}, Domain = {Domain}",
                    requestDto.EntityId, requestDto.Domain);

                return OperationResult.FailureResult(
                    Error.Validation(ErrorCodes.Analysis.InvalidTarget, "Domain is not valid"));
            }

            if (targetType == TargetType.Page && string.IsNullOrWhiteSpace(requestDto.Keywords))
            {
                logger.LogWarning("Keyword Is Required For This EntityId = {EntityId}, Target = {Target}",
                    requestDto.EntityId, target);

                return OperationResult.FailureResult(
                    Error.Validation(ErrorCodes.Analysis.KeywordRequired, "Keyword is required for a page"));
            }

            var exists = await context.Analyses.AnyAsync(
                s => s.UserId == currentUser.UserId && s.EntityId == requestDto.EntityId, cancellationToken);

            if (exists)
            {
                logger.LogInformation(
                    "Analyze Already Exists For This UserId = {UserId}, EntityId = {EntityId}, Skip Create",
                    currentUser.UserId, requestDto.EntityId);

                return OperationResult.SuccessResult();
            }

            var now = DateTimeOffset.UtcNow;

            var competitors = new List<Competitor>();

            foreach (var input in requestDto.Competitors ?? [])
            {
                if (!TargetHelper.TryParse(input, out var competitor, out var competitorType)
                    || competitorType != TargetType.Domain)
                {
                    logger.LogWarning("Invalid Competitor For This EntityId = {EntityId}, Competitor = {Competitor}",
                        requestDto.EntityId, input);

                    return OperationResult.FailureResult(
                        Error.Validation(ErrorCodes.Analysis.InvalidCompetitor, $"Competitor {input} is not valid"));
                }

                if (competitor == target || competitors.Any(c => c.Domain == competitor))
                    continue;

                competitors.Add(new Competitor
                {
                    Domain = competitor,
                    Source = CompetitorSource.User,
                    CreateDate = now
                });
            }

           
            var newAnalyze = new Analysis
            {
                UserId = currentUser.UserId,
                EntityId = requestDto.EntityId,
                Target = target,
                TargetType = targetType,
                Keyword = targetType == TargetType.Page ? requestDto.Keywords : null,
                CountryCode = requestDto.CountryCode.ToUpperInvariant(),
                BrandName = requestDto.BrandName,
                CreateDate = now,
                Competitors = competitors,
                ProcessState = new ProcessState
                {
                    Status = ProcessStatus.Running,
                    CurrentStep = CurrentStep.CheckDomainSummary,
                    StepStartedAt = now,
                    CreateDate = now
                }
            };

            context.Analyses.Add(newAnalyze);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Analyze Saved With This AnalysisId = {AnalysisId}, ProcessStateId = {ProcessStateId}, EntityId = {EntityId}, Target = {Target}, TargetType = {TargetType}, CompetitorsCount = {CompetitorsCount}",
                newAnalyze.Id, newAnalyze.ProcessState.Id, requestDto.EntityId, target, targetType, competitors.Count);

            jobScheduler.Enqueue(new ProcessJobDto(newAnalyze.ProcessState.Id));

            logger.LogInformation("Complete Create Analyze For This AnalysisId = {AnalysisId}, EntityId = {EntityId}",
                newAnalyze.Id, requestDto.EntityId);

            return OperationResult.SuccessResult();
        }
        catch (Exception e)
        {
            logger.LogError(e,
                "Get Exception When Create Analyze For This EntityId = {EntityId}, Domain = {Domain}, With This Message {Message}",
                requestDto.EntityId, requestDto.Domain, e.Message);
            throw;
        }
    }
}