using LinkRadar.Api.Context;
using LinkRadar.Api.DataForSeoServices;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.ResultPattern;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

public class GetReferringDomains(
    ILogger<GetReferringDomains> logger,
    LinkRadarDbContext context,
    DataForSeoService dataForSeoService) : IProcessStep
{
    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.GetReferringDomains;

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

        logger.LogInformation("Start Get Referring Domains For This ProcessStateId = {ProcessStateId}, Target = {Target}",
            processState.Id, target);

        var dataForSeoResult = await dataForSeoService.PostAsync<ReferringDomainsResult>(
            "backlinks/referring_domains/live", new ReferringDomainsRequest(target), cancellationToken);

        // تماس خطادار هم ممکن است هزینه داشته باشد
        processState.TotalCost += dataForSeoResult.Result?.Cost ?? 0;

        if (!dataForSeoResult.IsSuccess)
        {
            logger.LogWarning(
                "Get Referring Domains Failed For This ProcessStateId = {ProcessStateId}, Target = {Target}, Error = {Error}",
                processState.Id, target, dataForSeoResult.GetErrorMessage());

            return OperationResult.FailureResult(dataForSeoResult.Errors);
        }

        var callResult = dataForSeoResult.Result!;
        var items = callResult.Result?.Items ?? [];

        // Domain اجباری است؛ یک ردیف ناقص نباید ذخیره‌ی بقیه را خراب کند
        var referringDomains = items
            .Where(s => !string.IsNullOrWhiteSpace(s.Domain))
            .Select(s => new ReferringDomain
            {
                AnalysisId = processState.AnalysisId,
                Domain = s.Domain!,
                Rank = s.Rank ?? 0,
                Backlinks = s.Backlinks ?? 0,
                BacklinksSpamScore = s.BacklinksSpamScore ?? 0,
                BrokenBacklinks = s.BrokenBacklinks ?? 0,
                ReferringPages = s.ReferringPages ?? 0,
                ReferringPagesNofollow = s.ReferringPagesNofollow ?? 0,
                FirstSeen = s.FirstSeen
            })
            .ToList();

        if (referringDomains.Count < items.Count)
            logger.LogWarning(
                "Skip Incomplete Referring Domains For This ProcessStateId = {ProcessStateId}, SkippedCount = {SkippedCount}",
                processState.Id, items.Count - referringDomains.Count);

        // Summary بک‌لینک داشته ولی لیست خالی برگشته؛ پردازش ادامه پیدا می‌کند
        if (referringDomains.Count == 0)
            logger.LogWarning("Referring Domains List Is Empty For This ProcessStateId = {ProcessStateId}, Target = {Target}",
                processState.Id, target);

        context.ReferringDomains.AddRange(referringDomains);

        processState.CurrentStep = CurrentStep.GetTimeSeries;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Get Referring Domains For This ProcessStateId = {ProcessStateId}, SavedCount = {SavedCount}, TotalCount = {TotalCount}, Cost = {Cost}, Next Step = {NextStep}",
            processState.Id, referringDomains.Count, callResult.Result?.TotalCount, callResult.Cost, processState.CurrentStep);

        return OperationResult.SuccessResult();
    }
}

#region Request

public record ReferringDomainsRequest(
    string Target,
    string BacklinksStatusType = "live",
    string RankScale = "one_hundred",
    bool IncludeSubdomains = true,
    bool ExcludeInternalBacklinks = true,
    bool IncludeIndirectLinks = true,
    int Limit = 1000)
{
    public string[] OrderBy { get; init; } = ["rank,desc"];
}

#endregion

#region Response

public class ReferringDomainsResult
{
    public long? TotalCount { get; set; }
    public List<ReferringDomainItem>? Items { get; set; }
}

public class ReferringDomainItem
{
    public string? Domain { get; set; }
    public int? Rank { get; set; }
    public long? Backlinks { get; set; }
    public int? BacklinksSpamScore { get; set; }
    public long? BrokenBacklinks { get; set; }
    public long? ReferringPages { get; set; }
    public long? ReferringPagesNofollow { get; set; }
    public DateTimeOffset? FirstSeen { get; set; }
}

#endregion