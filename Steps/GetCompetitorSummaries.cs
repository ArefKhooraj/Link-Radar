using System.Threading.Channels;
using LinkRadar.Api.Context;
using LinkRadar.Api.DataForSeoServices;
using LinkRadar.Api.Dtos.DataForSeo;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.ResultPattern;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

public class GetCompetitorSummaries(
    DataForSeoService dataForSeoService,
    LinkRadarDbContext context,
    ILogger<GetCompetitorSummaries> logger) : IProcessStep
{
    private const int MaxParallelCalls = 5;

    private static readonly string[] ContentLocations = ["article", "main", "section"];

    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.GetCompetitorSummaries;

    public async Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken)
    {
        // رقیبی که قبلاً گرفته شده دوباره پول نمی‌گیرد
        var competitors = await context.Set<Competitor>()
            .Where(s => s.AnalysisId == processState.AnalysisId && s.SummaryFetchedAt == null)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Start Get Competitor Summaries For This ProcessStateId = {ProcessStateId}, CompetitorCount = {CompetitorCount}",
            processState.Id, competitors.Count);

        var channel = Channel.CreateBounded<CompetitorSummaryCall>(new BoundedChannelOptions(Math.Max(competitors.Count, 1))
        {
            SingleReader = true
        });

        // فقط همین consumer به DbContext و processState دست می‌زند، چون DbContext امن برای همزمانی نیست
        var consumer = SaveResults(channel.Reader, processState, cancellationToken);

        try
        {
            await Parallel.ForEachAsync(competitors,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelCalls, CancellationToken = cancellationToken },
                async (competitor, ct) =>
                {
                    var result = await CallDataForSeo(competitor, processState.Id, ct);

                    await channel.Writer.WriteAsync(new CompetitorSummaryCall(competitor, result), ct);
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

        processState.CurrentStep = CurrentStep.ComputeMetrics;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Get Competitor Summaries For This ProcessStateId = {ProcessStateId}, Next Step = {NextStep}",
            processState.Id, processState.CurrentStep);

        return OperationResult.SuccessResult();
    }

    // خطا exception نمی‌شود تا بقیه‌ی تماس‌های موازی لغو نشوند
    private async Task<OperationResult<DataForSeoCallResult<CompetitorSummaryResult>>> CallDataForSeo(
        Competitor competitor, int processStateId, CancellationToken cancellationToken)
    {
        try
        {
            return await dataForSeoService.PostAsync<CompetitorSummaryResult>(
                "backlinks/summary/live", new CompetitorSummaryRequest(competitor.Domain), cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(e,
                "Get Exception In Competitor Summary Call For This ProcessStateId = {ProcessStateId}, Competitor = {Competitor}, With This Message {Message}",
                processStateId, competitor.Domain, e.Message);

            return OperationResult<DataForSeoCallResult<CompetitorSummaryResult>>.FailureResult(
                Error.ExternalService(ErrorCodes.DataForSeo.RequestFailed, e.Message, isTransient: true));
        }
    }

    private async Task<List<Error>> SaveResults(ChannelReader<CompetitorSummaryCall> reader, ProcessState processState,
        CancellationToken cancellationToken)
    {
        var transientErrors = new List<Error>();

        await foreach (var (competitor, result) in reader.ReadAllAsync(cancellationToken))
        {
            // تماس خطادار هم ممکن است هزینه داشته باشد
            var cost = result.Result?.Cost ?? 0;
            processState.TotalCost += cost;

            if (result is { IsSuccess: false, IsTransient: true })
            {
                logger.LogWarning(
                    "Competitor Summary Failed For This ProcessStateId = {ProcessStateId}, Competitor = {Competitor}, Error = {Error}",
                    processState.Id, competitor.Domain, result.GetErrorMessage());

                transientErrors.AddRange(result.Errors);

                // هزینه ثبت می‌شود ولی رقیب برای retry باز می‌ماند
                await context.SaveChangesAsync(cancellationToken);
                continue;
            }

            // خطای دائمی با تکرار درست نمی‌شود؛ آمار این رقیب null می‌ماند و در خروجی «نامشخص» است
            if (!result.IsSuccess)
                logger.LogWarning(
                    "Skip Competitor Summary For This ProcessStateId = {ProcessStateId}, Competitor = {Competitor}, Error = {Error}",
                    processState.Id, competitor.Domain, result.GetErrorMessage());
            else
                MapSummary(competitor, result.Result!.Result);

            competitor.SummaryFetchedAt = DateTimeOffset.UtcNow;

            // پولی که داده شده با هر رقیب ذخیره می‌شود و با خطای بقیه از بین نمی‌رود
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Competitor Summary Saved For This ProcessStateId = {ProcessStateId}, Competitor = {Competitor}, ReferringDomains = {ReferringDomains}, Cost = {Cost}",
                processState.Id, competitor.Domain, competitor.ReferringDomains, cost);
        }

        return transientErrors;
    }

    private static void MapSummary(Competitor competitor, CompetitorSummaryResult? summary)
    {
        if (summary is null)
            return;

        competitor.Rank = summary.Rank;
        competitor.Backlinks = summary.Backlinks;
        competitor.ReferringDomains = summary.ReferringDomains;
        competitor.ReferringDomainsNofollow = summary.ReferringDomainsNofollow;
        competitor.ReferringMainDomains = summary.ReferringMainDomains;
        competitor.BrokenBacklinks = summary.BrokenBacklinks;
        competitor.FirstSeen = summary.FirstSeen;

        // کلید خالی یعنی محل لینک نامشخص است و در درصد حساب نمی‌شود
        var knownLocations = (summary.ReferringLinksSemanticLocations ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s.Key))
            .ToList();

        var knownTotal = knownLocations.Sum(s => s.Value);

        if (knownTotal == 0)
            return;

        var contentLinks = knownLocations.Where(s => ContentLocations.Contains(s.Key)).Sum(s => s.Value);
        var footerLinks = knownLocations.Where(s => s.Key == "footer").Sum(s => s.Value);

        competitor.ContentLinksPct = Math.Round((decimal)contentLinks / knownTotal * 100, 2);
        competitor.FooterLinksPct = Math.Round((decimal)footerLinks / knownTotal * 100, 2);
    }

    private record CompetitorSummaryCall(
        Competitor Competitor,
        OperationResult<DataForSeoCallResult<CompetitorSummaryResult>> Result);
}

#region Request

public record CompetitorSummaryRequest(
    string Target,
    string RankScale = "one_hundred",
    string BacklinksStatusType = "live",
    bool IncludeSubdomains = true,
    bool ExcludeInternalBacklinks = true,
    bool IncludeIndirectLinks = true,
    int InternalListLimit = 100);

#endregion

#region Response

public class CompetitorSummaryResult
{
    public DateTimeOffset? FirstSeen { get; set; }
    public int? Rank { get; set; }
    public long? Backlinks { get; set; }
    public long? BrokenBacklinks { get; set; }
    public long? ReferringDomains { get; set; }
    public long? ReferringDomainsNofollow { get; set; }
    public long? ReferringMainDomains { get; set; }
    public Dictionary<string, long>? ReferringLinksSemanticLocations { get; set; }
}

#endregion
