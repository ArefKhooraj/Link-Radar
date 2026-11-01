using LinkRadar.Api.Context;
using LinkRadar.Api.DataForSeoServices;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.ResultPattern;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

public class GetBacklinks(DataForSeoService dataForSeoService, ILogger<GetBacklinks> logger, LinkRadarDbContext context)
    : IProcessStep
{
    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.GetBacklinks;

    public async Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken)
    {
        var target = await context.Analyses.Where(s => s.Id == processState.AnalysisId)
            .Select(s => s.Target)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(target))
        {
            logger.LogError("Can Not Find Target For This ProcessStateId = {ProcessStateId}, AnalysisId = {AnalysisId}",
                processState.Id, processState.AnalysisId);

            return OperationResult.FailureResult(Error.NotFound(ErrorCodes.Analysis.InvalidTarget,
                "Can Not Find Target For This Analysis Id"));
        }

        logger.LogInformation("Start Get Backlinks For This ProcessStateId = {ProcessStateId}, Target = {Target}",
            processState.Id, target);

        var dataForSeoResult = await dataForSeoService.PostAsync<BacklinksResult>(
            "backlinks/backlinks/live", new BacklinksRequest(target), cancellationToken);

        // تماس خطادار هم ممکن است هزینه داشته باشد
        processState.TotalCost += dataForSeoResult.Result?.Cost ?? 0;

        if (!dataForSeoResult.IsSuccess)
        {
            logger.LogWarning("Get Backlinks Failed For This ProcessStateId = {ProcessStateId}, Target = {Target}, Error = {Error}",
                processState.Id, target, dataForSeoResult.GetErrorMessage());

            return OperationResult.FailureResult(dataForSeoResult.Errors);
        }

        var callResult = dataForSeoResult.Result!;
        var items = callResult.Result?.Items ?? [];

        // این سه ستون اجباری‌اند؛ یک ردیف ناقص نباید ذخیره‌ی بقیه را خراب کند
        var backlinks = items
            .Where(s => !string.IsNullOrWhiteSpace(s.DomainFrom)
                        && !string.IsNullOrWhiteSpace(s.UrlFrom)
                        && !string.IsNullOrWhiteSpace(s.UrlTo))
            .Select(s => new Backlink
            {
                AnalysisId = processState.AnalysisId,
                DomainFrom = s.DomainFrom!,
                UrlFrom = s.UrlFrom!,
                UrlTo = s.UrlTo!,
                Anchor = s.Anchor,
                ItemType = s.ItemType,
                DoFollow = s.Dofollow ?? false,
                Attributes = s.Attributes is { Count: > 0 } ? string.Join(",", s.Attributes) : null,
                Rank = s.Rank ?? 0,
                PageFromRank = s.PageFromRank ?? 0,
                DomainFromRank = s.DomainFromRank ?? 0,
                BacklinkSpamScore = s.BacklinkSpamScore ?? 0,
                SemanticLocation = string.IsNullOrWhiteSpace(s.SemanticLocation) ? null : s.SemanticLocation,
                PageFromLanguage = s.PageFromLanguage,
                TldFrom = s.TldFrom,
                IsBroken = s.IsBroken ?? false,
                UrlToStatusCode = s.UrlToStatusCode,
                FirstSeen = s.FirstSeen,
                LastSeen = s.LastSeen
            })
            .ToList();

        if (backlinks.Count < items.Count)
            logger.LogWarning(
                "Skip Incomplete Backlinks For This ProcessStateId = {ProcessStateId}, SkippedCount = {SkippedCount}",
                processState.Id, items.Count - backlinks.Count);

        // Summary بک‌لینک داشته ولی لیست خالی برگشته؛ پردازش ادامه پیدا می‌کند
        if (backlinks.Count == 0)
            logger.LogWarning("Backlinks List Is Empty For This ProcessStateId = {ProcessStateId}, Target = {Target}",
                processState.Id, target);

        context.Backlinks.AddRange(backlinks);

        processState.CurrentStep = CurrentStep.GetAnchors;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Get Backlinks For This ProcessStateId = {ProcessStateId}, SavedCount = {SavedCount}, TotalCount = {TotalCount}, Cost = {Cost}, Next Step = {NextStep}",
            processState.Id, backlinks.Count, callResult.Result?.TotalCount, callResult.Cost, processState.CurrentStep);

        return OperationResult.SuccessResult();
    }
}

#region Request


public record BacklinksRequest(
    string Target,
    string Mode = "one_per_domain",
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

public class BacklinksResult
{
    public long? TotalCount { get; set; }
    public List<BacklinkItem>? Items { get; set; }
}

public class BacklinkItem
{
    public string? DomainFrom { get; set; }
    public string? UrlFrom { get; set; }
    public string? UrlTo { get; set; }
    public string? Anchor { get; set; }
    public string? ItemType { get; set; }
    public bool? Dofollow { get; set; }
    public List<string>? Attributes { get; set; }
    public int? Rank { get; set; }
    public int? PageFromRank { get; set; }
    public int? DomainFromRank { get; set; }
    public int? BacklinkSpamScore { get; set; }
    public string? SemanticLocation { get; set; }
    public string? PageFromLanguage { get; set; }
    public string? TldFrom { get; set; }
    public bool? IsBroken { get; set; }
    public int? UrlToStatusCode { get; set; }
    public DateTimeOffset? FirstSeen { get; set; }
    public DateTimeOffset? LastSeen { get; set; }
}

#endregion
