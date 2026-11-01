using System.Threading.Channels;
using LinkRadar.Api.Context;
using LinkRadar.Api.DataForSeoServices;
using LinkRadar.Api.Dtos.DataForSeo;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.ResultPattern;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

public class GetLinkGap(DataForSeoService dataForSeoService, LinkRadarDbContext context, ILogger<GetLinkGap> logger)
    : IProcessStep
{
    private const int MaxParallelCalls = 5;

    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.GetLinkGap;

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

            return OperationResult.FailureResult(Error.NotFound(ErrorCodes.Analysis.InvalidTarget, "Null Data"));
        }

        // رقیبی که قبلاً گرفته شده دوباره پول نمی‌گیرد
        var competitors = await context.Set<Competitor>()
            .Where(s => s.AnalysisId == processState.AnalysisId && s.LinkGapFetchedAt == null)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Start Get Link Gap For This ProcessStateId = {ProcessStateId}, Target = {Target}, CompetitorCount = {CompetitorCount}",
            processState.Id, target, competitors.Count);

        var channel = Channel.CreateBounded<LinkGapCall>(new BoundedChannelOptions(Math.Max(competitors.Count, 1))
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
                    var result = await CallDataForSeo(competitor, target, processState.Id, ct);

                    await channel.Writer.WriteAsync(new LinkGapCall(competitor, result), ct);
                });
        }
        finally
        {
            channel.Writer.Complete();
        }

        var transientErrors = await consumer;

        
        if (transientErrors.Count > 0)
            return OperationResult.FailureResult(transientErrors);

        processState.CurrentStep = CurrentStep.ComputeMetrics;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Get Link Gap For This ProcessStateId = {ProcessStateId}, Next Step = {NextStep}",
            processState.Id, processState.CurrentStep);

        return OperationResult.SuccessResult();
    }

    // خطا exception نمی‌شود تا بقیه‌ی تماس‌های موازی لغو نشوند
    private async Task<OperationResult<DataForSeoCallResult<LinkGapResult>>> CallDataForSeo(Competitor competitor,
        string target, int processStateId, CancellationToken cancellationToken)
    {
        try
        {
            return await dataForSeoService.PostAsync<LinkGapResult>(
                "backlinks/domain_intersection/live", new LinkGapRequest(competitor.Domain, target), cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(e,
                "Get Exception In Link Gap Call For This ProcessStateId = {ProcessStateId}, Competitor = {Competitor}, With This Message {Message}",
                processStateId, competitor.Domain, e.Message);

            return OperationResult<DataForSeoCallResult<LinkGapResult>>.FailureResult(
                Error.ExternalService(ErrorCodes.DataForSeo.RequestFailed, e.Message, isTransient: true));
        }
    }

    private async Task<List<Error>> SaveResults(ChannelReader<LinkGapCall> reader, ProcessState processState,
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
                    "Link Gap Failed For This ProcessStateId = {ProcessStateId}, Competitor = {Competitor}, Error = {Error}",
                    processState.Id, competitor.Domain, result.GetErrorMessage());

                transientErrors.AddRange(result.Errors);

                // هزینه ثبت می‌شود ولی رقیب برای retry باز می‌ماند
                await context.SaveChangesAsync(cancellationToken);
                continue;
            }

            // خطای دائمی با تکرار درست نمی‌شود و نباید کل پایش را نگه دارد
            if (!result.IsSuccess)
                logger.LogWarning(
                    "Skip Competitor In Link Gap For This ProcessStateId = {ProcessStateId}, Competitor = {Competitor}, Error = {Error}",
                    processState.Id, competitor.Domain, result.GetErrorMessage());

            var domains = result.IsSuccess ? BuildDomains(result.Result!.Result?.Items ?? [], competitor.Id) : [];

            context.LinkGapDomains.AddRange(domains);
            competitor.LinkGapFetchedAt = DateTimeOffset.UtcNow;

            // پولی که داده شده با هر رقیب ذخیره می‌شود و با خطای بقیه از بین نمی‌رود
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Link Gap Saved For This ProcessStateId = {ProcessStateId}, Competitor = {Competitor}, SavedCount = {SavedCount}, TotalCount = {TotalCount}, Cost = {Cost}",
                processState.Id, competitor.Domain, domains.Count, result.Result?.Result?.TotalCount, cost);
        }

        return transientErrors;
    }

    private static List<LinkGapDomain> BuildDomains(List<LinkGapResultItem> items, int competitorId)
    {
        var domains = new List<LinkGapDomain>();

        foreach (var item in items)
        {
            var domain = item.DomainIntersection?.GetValueOrDefault("1");

            // Domain اجباری است؛ یک ردیف ناقص نباید ذخیره‌ی بقیه را خراب کند
            if (string.IsNullOrWhiteSpace(domain?.Target))
                continue;

            domains.Add(new LinkGapDomain
            {
                CompetitorId = competitorId,
                Domain = domain.Target,
                Rank = domain.Rank ?? 0,
                Backlinks = domain.Backlinks ?? 0,
                BacklinksSpamScore = domain.BacklinksSpamScore ?? 0,
                FirstSeen = domain.FirstSeen
            });
        }

        return domains;
    }

    private record LinkGapCall(Competitor Competitor, OperationResult<DataForSeoCallResult<LinkGapResult>> Result);
}

#region Request

public record LinkGapRequest
{
    public LinkGapRequest(string competitor, string target)
    {
        Targets = new() { ["1"] = competitor };
        ExcludeTargets = [target];
    }

    public Dictionary<string, string> Targets { get; }
    public string[] ExcludeTargets { get; }
    public string BacklinksStatusType { get; init; } = "live";
    public string RankScale { get; init; } = "one_hundred";
    public bool IncludeSubdomains { get; init; } = true;
    public bool ExcludeInternalBacklinks { get; init; } = true;
    public bool IncludeIndirectLinks { get; init; } = true;
    public int Limit { get; init; } = 300;
    public string[] OrderBy { get; init; } = ["1.rank,desc"];
}

#endregion

#region Response

public class LinkGapResult
{
    public long? TotalCount { get; set; }
    public List<LinkGapResultItem>? Items { get; set; }
}

public class LinkGapResultItem
{
    public Dictionary<string, LinkGapDomainItem>? DomainIntersection { get; set; }
}

public class LinkGapDomainItem
{
    // دامنه‌ی لینک‌دهنده، نه رقیب
    public string? Target { get; set; }
    public int? Rank { get; set; }
    public long? Backlinks { get; set; }
    public int? BacklinksSpamScore { get; set; }
    public DateTimeOffset? FirstSeen { get; set; }
}

#endregion