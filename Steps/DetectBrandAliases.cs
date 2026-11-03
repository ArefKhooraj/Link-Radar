using LinkRadar.Api.AIRequestModels;
using LinkRadar.Api.AiServices;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.Mapper;
using LinkRadar.Api.ResultPattern;
using LinkRadar.Api.Services;
using LinkRadar.Api.Statics;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

// املاهای برند در انکرها، مثل «وبمسترسلام» برای webmastersalam؛ فقط ComputeMetrics از آن استفاده می‌کند
public class DetectBrandAliases(CommonServices<DetectBrandAliases> commonServices, AiService aiService) : IProcessStep
{
    // انکرهای پرتکرار برای پیدا کردن برند کافی‌اند و توکن را محدود نگه می‌دارند
    private const int MaxAnchors = 300;

    private const int MinAliasLength = 3;

    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.DetectBrandAliases;

    public async Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken)
    {
        var context = commonServices.Context;
        var logger = commonServices.Logger;

        var analysis = await context.Analyses.FirstOrDefaultAsync(s => s.Id == processState.AnalysisId,
            cancellationToken);

        if (analysis is null)
        {
            logger.LogError("Can Not Find Analysis For This ProcessStateId = {ProcessStateId}, AnalysisId = {AnalysisId}",
                processState.Id, processState.AnalysisId);

            return OperationResult.FailureResult(Error.NotFound(ErrorCodes.Analysis.InvalidTarget, "Analysis Not Found"));
        }

        var anchors = await context.BacklinkAnchors.AsNoTracking()
            .Where(s => s.AnalysisId == processState.AnalysisId && s.Anchor != null)
            .OrderByDescending(s => s.ReferringDomains)
            .Select(s => s.Anchor!)
            .Take(MaxAnchors)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Start Detect Brand Aliases For This ProcessStateId = {ProcessStateId}, Target = {Target}, AnchorCount = {AnchorCount}",
            processState.Id, analysis.Target, anchors.Count);

        // بدون انکر متنی چیزی برای پیدا کردن نیست و پولی خرج نمی‌شود
        if (anchors.Count > 0)
            analysis.BrandAliases = await DetectAliases(analysis, anchors, processState, cancellationToken);

        processState.CurrentStep = CurrentStep.GetReferringDomains;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Detect Brand Aliases For This ProcessStateId = {ProcessStateId}, Aliases = {Aliases}, Next Step = {NextStep}",
            processState.Id, analysis.BrandAliases, processState.CurrentStep);

        return OperationResult.SuccessResult();
    }

    private async Task<List<string>> DetectAliases(Analysis analysis, List<string> anchors, ProcessState processState,
        CancellationToken cancellationToken)
    {
        var logger = commonServices.Logger;

        var aiResult = await aiService.GetCompletionV2Async<BrandAliasesAiResponse>(
            DetectBrandAliasesRequestBuilder.Build(analysis.Target, analysis.BrandName, anchors), cancellationToken);

        // این داده فقط دقت دسته‌بندی انکر را بالا می‌برد؛ بدونش ComputeMetrics با BrandName و دامنه کار می‌کند
        if (!aiResult.IsSuccess)
        {
            logger.LogWarning(
                "Detect Brand Aliases Failed For This ProcessStateId = {ProcessStateId}, Error = {Error}, Continue Without Aliases",
                processState.Id, aiResult.GetErrorMessage());

            return [];
        }

        var aiResponse = aiResult.Result!;

        if (ModelPricing.TryCalculateCost(aiResponse.MapToCalculateCost(), out var aiCost))
            processState.TotalCost += aiCost;
        else
            logger.LogWarning("No Price For Model {Model}, ProcessStateId = {ProcessStateId}",
                aiResponse.Model, processState.Id);

        // خروجی AI معتبر فرض نمی‌شود: املایی که در هیچ انکری نیست، ساخته‌ی مدل است
        var compactAnchors = anchors.Select(ComputeMetrics.Compact).ToList();
        var aliases = new List<string>();

        foreach (var alias in aiResponse.Data.Aliases)
        {
            var compact = ComputeMetrics.Compact(alias);

            if (compact.Length < MinAliasLength || !compactAnchors.Any(s => s.Contains(compact)))
            {
                logger.LogWarning("Reject Brand Alias For This ProcessStateId = {ProcessStateId}, Alias = {Alias}",
                    processState.Id, alias);
                continue;
            }

            if (!aliases.Any(s => ComputeMetrics.Compact(s) == compact))
                aliases.Add(alias.Trim());
        }

        logger.LogInformation(
            "Ai Detect Brand Aliases Completed For This ProcessStateId = {ProcessStateId}, AcceptedCount = {AcceptedCount}, Cost = {Cost}",
            processState.Id, aliases.Count, aiCost);

        return aliases;
    }
}

#region Response

public class BrandAliasesAiResponse
{
    public List<string> Aliases { get; set; } = [];
}

#endregion
