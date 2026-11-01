using LinkRadar.Api.Context;
using LinkRadar.Api.DataForSeoServices;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.ResultPattern;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

public class GetAnchors(ILogger<GetAnchors> logger, LinkRadarDbContext context, DataForSeoService dataForSeoService)
    : IProcessStep
{
    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.GetAnchors;

    public async Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken)
    {
        var target = await context.Analyses.Where(s => s.Id == processState.AnalysisId).Select(s => s.Target)
            .AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(target))
        {
            logger.LogError("Can Not Find Target For This ProcessStateId = {ProcessStateId}, AnalysisId = {AnalysisId}",
                processState.Id, processState.AnalysisId);

            return OperationResult.FailureResult(Error.NotFound(ErrorCodes.Analysis.InvalidTarget,
                "Can Not Find This Target"));
        }

        logger.LogInformation("Start Get Anchors For This ProcessStateId = {ProcessStateId}, Target = {Target}",
            processState.Id, target);

        var dataForSeoResult = await dataForSeoService.PostAsync<AnchorsResult>(
            "backlinks/anchors/live", new AnchorsRequest(target), cancellationToken);

        // تماس خطادار هم ممکن است هزینه داشته باشد
        processState.TotalCost += dataForSeoResult.Result?.Cost ?? 0;

        if (!dataForSeoResult.IsSuccess)
        {
            logger.LogWarning("Get Anchors Failed For This ProcessStateId = {ProcessStateId}, Target = {Target}, Error = {Error}",
                processState.Id, target, dataForSeoResult.GetErrorMessage());

            return OperationResult.FailureResult(dataForSeoResult.Errors);
        }

        var callResult = dataForSeoResult.Result!;

        var anchors = (callResult.Result?.Items ?? [])
            .Select(s => new BacklinkAnchor
            {
                AnalysisId = processState.AnalysisId,
                // انکر خالی هم لینک بدون متن است و یک دسته حساب می‌شود
                Anchor = string.IsNullOrWhiteSpace(s.Anchor) ? null : s.Anchor,
                Backlinks = s.Backlinks ?? 0,
                ReferringDomains = s.ReferringDomains ?? 0,
                ReferringDomainsNofollow = s.ReferringDomainsNofollow ?? 0,
                Rank = s.Rank ?? 0,
                BacklinksSpamScore = s.BacklinksSpamScore ?? 0,
                FirstSeen = s.FirstSeen
            })
            .ToList();

        // Summary بک‌لینک داشته ولی لیست خالی برگشته؛ پردازش ادامه پیدا می‌کند
        if (anchors.Count == 0)
            logger.LogWarning("Anchors List Is Empty For This ProcessStateId = {ProcessStateId}, Target = {Target}",
                processState.Id, target);

        context.BacklinkAnchors.AddRange(anchors);

        processState.CurrentStep = CurrentStep.GetReferringDomains;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Get Anchors For This ProcessStateId = {ProcessStateId}, SavedCount = {SavedCount}, TotalCount = {TotalCount}, Cost = {Cost}, Next Step = {NextStep}",
            processState.Id, anchors.Count, callResult.Result?.TotalCount, callResult.Cost, processState.CurrentStep);

        return OperationResult.SuccessResult();
    }
}

#region Request

public record AnchorsRequest(
    string Target,
    string BacklinksStatusType = "live",
    string RankScale = "one_hundred",
    bool IncludeSubdomains = true,
    bool ExcludeInternalBacklinks = true,
    bool IncludeIndirectLinks = true,
    int Limit = 1000)
{
    public string[] OrderBy { get; init; } = ["backlinks,desc"];
}

#endregion

#region Response

public class AnchorsResult
{
    public long? TotalCount { get; set; }
    public List<AnchorItem>? Items { get; set; }
}

public class AnchorItem
{
    public string? Anchor { get; set; }
    public long? Backlinks { get; set; }
    public long? ReferringDomains { get; set; }
    public long? ReferringDomainsNofollow { get; set; }
    public int? Rank { get; set; }
    public int? BacklinksSpamScore { get; set; }
    public DateTimeOffset? FirstSeen { get; set; }
}

#endregion