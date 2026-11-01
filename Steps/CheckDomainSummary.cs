using LinkRadar.Api.Context;
using LinkRadar.Api.DataForSeoServices;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.ResultPattern;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

public class CheckDomainSummary(
    DataForSeoService dataForSeoService,
    LinkRadarDbContext context,
    ILogger<CheckDomainSummary> logger) : IProcessStep
{
    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.CheckDomainSummary;

    public async Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken)
    {
        var analysisTarget = await context.Analyses.Where(s => s.Id == processState.AnalysisId)
            .Select(s => s.Target)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(analysisTarget))
        {
            logger.LogError("Can Not Find Target For This ProcessStateId = {ProcessStateId}, AnalysisId = {AnalysisId}",
                processState.Id, processState.AnalysisId);

            return OperationResult.FailureResult(Error.NotFound(ErrorCodes.Analysis.InvalidTarget, "Target Is Null"));
        }

        logger.LogInformation("Start Get Backlink Summary For This ProcessStateId = {ProcessStateId}, Target = {Target}",
            processState.Id, analysisTarget);

        var dataForSeoResult = await dataForSeoService.PostAsync<BacklinkSummaryResult>(
            "backlinks/summary/live", new BacklinkSummaryRequest(analysisTarget), cancellationToken);

        // تماس خطادار هم ممکن است هزینه داشته باشد
        processState.TotalCost += dataForSeoResult.Result?.Cost ?? 0;

        if (!dataForSeoResult.IsSuccess)
        {
            logger.LogWarning(
                "Get Backlink Summary Failed For This ProcessStateId = {ProcessStateId}, Target = {Target}, Error = {Error}",
                processState.Id, analysisTarget, dataForSeoResult.GetErrorMessage());

            return OperationResult.FailureResult(dataForSeoResult.Errors);
        }

        var callResult = dataForSeoResult.Result!;
        var dataForSeoData = callResult.Result;

        // پاسخ موفق بدون result مثل دامنه‌ی بدون بک‌لینک حساب می‌شود
        if (dataForSeoData is null)
            logger.LogWarning("Backlink Summary Has No Result For This ProcessStateId = {ProcessStateId}, Target = {Target}",
                processState.Id, analysisTarget);

        var newBacklinkSummary = new BacklinkSummary
        {
            AnalysisId = processState.AnalysisId,
            Rank = dataForSeoData?.Rank ?? 0,
            Backlinks = dataForSeoData?.Backlinks ?? 0,
            ReferringDomains = dataForSeoData?.ReferringDomains ?? 0,
            ReferringDomainsNofollow = dataForSeoData?.ReferringDomainsNofollow ?? 0,
            ReferringMainDomains = dataForSeoData?.ReferringMainDomains ?? 0,
            ReferringPages = dataForSeoData?.ReferringPages ?? 0,
            ReferringPagesNofollow = dataForSeoData?.ReferringPagesNofollow ?? 0,
            BacklinksSpamScore = dataForSeoData?.BacklinksSpamScore ?? 0,
            TargetSpamScore = dataForSeoData?.Info?.TargetSpamScore ?? 0,
            BrokenBacklinks = dataForSeoData?.BrokenBacklinks ?? 0,
            FirstSeen = dataForSeoData?.FirstSeen
        };

        context.BacklinkSummaries.Add(newBacklinkSummary);

        // بدون بک‌لینک، پول stepهای بعدی خرج نمی‌شود
        processState.CurrentStep = newBacklinkSummary.Backlinks == 0
            ? CurrentStep.SendToUserWebhook
            : CurrentStep.DetectCompetitors;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Get Backlink Summary For This ProcessStateId = {ProcessStateId}, Backlinks = {Backlinks}, ReferringDomains = {ReferringDomains}, Cost = {Cost}, Next Step = {NextStep}",
            processState.Id, newBacklinkSummary.Backlinks, newBacklinkSummary.ReferringDomains, callResult.Cost,
            processState.CurrentStep);

        return OperationResult.SuccessResult();
    }
}

#region Result

public class BacklinkSummaryResult
{
    public DateTimeOffset? FirstSeen { get; set; }
    public int? Rank { get; set; }
    public long? Backlinks { get; set; }
    public int? BacklinksSpamScore { get; set; }
    public long? BrokenBacklinks { get; set; }
    public long? ReferringDomains { get; set; }
    public long? ReferringDomainsNofollow { get; set; }
    public long? ReferringMainDomains { get; set; }
    public long? ReferringPages { get; set; }
    public long? ReferringPagesNofollow { get; set; }
    public BacklinkSummaryInfo? Info { get; set; }
}

public class BacklinkSummaryInfo
{
    public int? TargetSpamScore { get; set; }
}

#endregion

#region Request

public record BacklinkSummaryRequest(
    string Target,
    string RankScale = "one_hundred",
    string BacklinksStatusType = "live",
    bool IncludeSubdomains = true,
    bool ExcludeInternalBacklinks = true,
    bool IncludeIndirectLinks = true);

#endregion