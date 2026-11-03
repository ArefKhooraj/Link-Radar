using System.Threading.Channels;
using LinkRadar.Api.DataForSeoServices;
using LinkRadar.Api.Dtos.DataForSeo;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.ResultPattern;
using LinkRadar.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

// برای فرصت‌های برتر، صفحه‌ای که به رقیب لینک داده؛ کاربر می‌بیند دقیقاً کجا و با چه متنی
public class GetOpportunityPages(CommonServices<GetOpportunityPages> commonServices) : IProcessStep
{
    private const int MaxParallelCalls = 5;

    // فقط برای فرصت‌های برتر پول می‌دهیم
    private const int MaxOpportunities = 50;

    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.GetOpportunityPages;

    public async Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken)
    {
        var context = commonServices.Context;
        var logger = commonServices.Logger;

        var opportunities = await context.LinkOpportunities
            .Where(s => s.AnalysisId == processState.AnalysisId)
            .OrderByDescending(s => s.Score)
            .Take(MaxOpportunities)
            .ToListAsync(cancellationToken);

        // رقیبی که قبلاً گرفته شده دوباره پول نمی‌گیرد
        var competitors = await context.Set<Competitor>()
            .Where(s => s.AnalysisId == processState.AnalysisId && s.OpportunityPagesFetchedAt == null)
            .ToListAsync(cancellationToken);

        var opportunityDomains = opportunities.Select(s => s.Domain).ToList();
        var competitorIds = competitors.Select(s => s.Id).ToList();

        // هر رقیب فقط برای دامنه‌هایی صدا زده می‌شود که واقعاً به او لینک داده‌اند
        var domainsByCompetitor = (await context.LinkGapDomains.AsNoTracking()
                .Where(s => competitorIds.Contains(s.CompetitorId) && opportunityDomains.Contains(s.Domain))
                .Select(s => new { s.CompetitorId, s.Domain })
                .ToListAsync(cancellationToken))
            .GroupBy(s => s.CompetitorId)
            .ToDictionary(s => s.Key, s => s.Select(g => g.Domain).Distinct().ToList());

        logger.LogInformation(
            "Start Get Opportunity Pages For This ProcessStateId = {ProcessStateId}, OpportunityCount = {OpportunityCount}, CompetitorCount = {CompetitorCount}",
            processState.Id, opportunities.Count, competitors.Count);

        var channel = Channel.CreateBounded<OpportunityPagesCall>(new BoundedChannelOptions(Math.Max(competitors.Count, 1))
        {
            SingleReader = true
        });

        // فقط همین consumer به DbContext و processState دست می‌زند، چون DbContext امن برای همزمانی نیست
        var consumer = SaveResults(channel.Reader, processState, opportunities, cancellationToken);

        try
        {
            await Parallel.ForEachAsync(competitors,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelCalls, CancellationToken = cancellationToken },
                async (competitor, ct) =>
                {
                    var domains = domainsByCompetitor.GetValueOrDefault(competitor.Id) ?? [];

                    // رقیب بدون فرصت برتر تماسی ندارد و پولی خرج نمی‌شود
                    var result = domains.Count == 0
                        ? null
                        : await CallDataForSeo(competitor, domains, processState.Id, ct);

                    await channel.Writer.WriteAsync(new OpportunityPagesCall(competitor, result), ct);
                });
        }
        finally
        {
            channel.Writer.Complete();
        }

        var transientErrors = await consumer;

        // فقط رقبای ناموفق با retry دوباره زده می‌شوند
        if (transientErrors.Count > 0)
            return OperationResult.FailureResult(transientErrors);

        processState.CurrentStep = CurrentStep.ClassifyOpportunities;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Get Opportunity Pages For This ProcessStateId = {ProcessStateId}, WithPageCount = {WithPageCount}, Next Step = {NextStep}",
            processState.Id, opportunities.Count(s => s.PageUrl is not null), processState.CurrentStep);

        return OperationResult.SuccessResult();
    }

    // خطا exception نمی‌شود تا بقیه‌ی تماس‌های موازی لغو نشوند
    private async Task<OperationResult<DataForSeoCallResult<BacklinksResult>>> CallDataForSeo(Competitor competitor,
        List<string> domains, int processStateId, CancellationToken cancellationToken)
    {
        try
        {
            return await commonServices.DataForSeoService.PostAsync<BacklinksResult>(
                "backlinks/backlinks/live", new OpportunityPagesRequest(competitor.Domain, domains), cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            commonServices.Logger.LogError(e,
                "Get Exception In Opportunity Pages Call For This ProcessStateId = {ProcessStateId}, Competitor = {Competitor}, With This Message {Message}",
                processStateId, competitor.Domain, e.Message);

            return OperationResult<DataForSeoCallResult<BacklinksResult>>.FailureResult(
                Error.ExternalService(ErrorCodes.DataForSeo.RequestFailed, e.Message, isTransient: true));
        }
    }

    private async Task<List<Error>> SaveResults(ChannelReader<OpportunityPagesCall> reader, ProcessState processState,
        List<LinkOpportunity> opportunities, CancellationToken cancellationToken)
    {
        var context = commonServices.Context;
        var logger = commonServices.Logger;
        var transientErrors = new List<Error>();

        await foreach (var (competitor, result) in reader.ReadAllAsync(cancellationToken))
        {
            // تماس خطادار هم ممکن است هزینه داشته باشد
            var cost = result?.Result?.Cost ?? 0;
            processState.TotalCost += cost;

            if (result is { IsSuccess: false, IsTransient: true })
            {
                logger.LogWarning(
                    "Opportunity Pages Failed For This ProcessStateId = {ProcessStateId}, Competitor = {Competitor}, Error = {Error}",
                    processState.Id, competitor.Domain, result.GetErrorMessage());

                transientErrors.AddRange(result.Errors);

                // هزینه ثبت می‌شود ولی رقیب برای retry باز می‌ماند
                await context.SaveChangesAsync(cancellationToken);
                continue;
            }

            // خطای دائمی با تکرار درست نمی‌شود؛ فرصت‌ها بدون صفحه می‌مانند
            if (result is { IsSuccess: false })
                logger.LogWarning(
                    "Skip Opportunity Pages For This ProcessStateId = {ProcessStateId}, Competitor = {Competitor}, Error = {Error}",
                    processState.Id, competitor.Domain, result.GetErrorMessage());

            var matchedCount = result is { IsSuccess: true }
                ? MapPages(result.Result!.Result?.Items ?? [], competitor.Domain, opportunities)
                : 0;

            competitor.OpportunityPagesFetchedAt = DateTimeOffset.UtcNow;

            // پولی که داده شده با هر رقیب ذخیره می‌شود و با خطای بقیه از بین نمی‌رود
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Opportunity Pages Saved For This ProcessStateId = {ProcessStateId}, Competitor = {Competitor}, MatchedCount = {MatchedCount}, Cost = {Cost}",
                processState.Id, competitor.Domain, matchedCount, cost);
        }

        return transientErrors;
    }

    private static int MapPages(List<BacklinkItem> items, string competitorDomain, List<LinkOpportunity> opportunities)
    {
        var matched = 0;

        // مرتب بر اساس rank است، پس اولین صفحه‌ی هر دامنه باارزش‌ترین است
        foreach (var item in items.Where(s => !string.IsNullOrWhiteSpace(s.DomainFrom) && !string.IsNullOrWhiteSpace(s.UrlFrom)))
        {
            var opportunity = opportunities.FirstOrDefault(s => s.PageUrl is null
                                                                && (item.DomainFrom == s.Domain
                                                                    || item.DomainFrom!.EndsWith("." + s.Domain)));

            if (opportunity is null)
                continue;

            opportunity.PageUrl = item.UrlFrom;
            opportunity.PageAnchor = item.Anchor;
            opportunity.PageCompetitorDomain = competitorDomain;
            matched++;
        }

        return matched;
    }

    private record OpportunityPagesCall(
        Competitor Competitor,
        OperationResult<DataForSeoCallResult<BacklinksResult>>? Result);
}

#region Request

public record OpportunityPagesRequest
{
    public OpportunityPagesRequest(string target, List<string> domains)
    {
        Target = target;
        Filters = [new object[] { "domain_from", "in", domains }];
        Limit = domains.Count;
    }

    public string Target { get; }

    public object[] Filters { get; }

    public int Limit { get; }

    public string Mode { get; init; } = "one_per_domain";

    public string BacklinksStatusType { get; init; } = "live";

    public string RankScale { get; init; } = "one_hundred";

    public bool IncludeSubdomains { get; init; } = true;

    public string[] OrderBy { get; init; } = ["rank,desc"];
}

#endregion
