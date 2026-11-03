using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinkRadar.Api.Configuration;
using LinkRadar.Api.Dtos.Webhook;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.ResultPattern;
using LinkRadar.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

// گزارش کامل و آماده‌ی نمایش را برای ajebhom می‌فرستد؛ داده در LinkRadar هم می‌ماند
public class SendToUserWebhook(CommonServices<SendToUserWebhook> commonServices, IHttpClientFactory clientFactory)
    : IProcessStep
{
    // با هر تغییر شکل payload یکی بالا برود تا ajebhom بفهمد با کدام نسخه کار دارد
    private const int SchemaVersion = 1;

    private const int MaxOpportunities = 50;
    private const int MaxBacklinks = 100;
    private const int MaxAnchors = 20;
    private const int MaxRiskyDomains = 10;
    private const int MaxRiskyAnchors = 10;
    private const int MaxBrokenPages = 50;
    private const int MaxTopDomains = 10;

    // enum به‌صورت متن و حروف عربی بدون escape تا ajebhom مستقیم نمایش دهد
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.SendToUserWebhook;

    public async Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken)
    {
        var context = commonServices.Context;
        var logger = commonServices.Logger;

        var analysis = await context.Analyses.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == processState.AnalysisId, cancellationToken);
        var summary = await context.BacklinkSummaries.AsNoTracking()
            .FirstOrDefaultAsync(s => s.AnalysisId == processState.AnalysisId, cancellationToken);

        if (analysis is null || summary is null)
        {
            logger.LogError(
                "Can Not Find Analysis Or Summary For This ProcessStateId = {ProcessStateId}, AnalysisId = {AnalysisId}",
                processState.Id, processState.AnalysisId);

            return OperationResult.FailureResult(Error.NotFound(ErrorCodes.Analysis.InvalidTarget,
                "Analysis Or Summary Not Found"));
        }

        logger.LogInformation("Start Send To User Webhook For This ProcessStateId = {ProcessStateId}, EntityId = {EntityId}",
            processState.Id, analysis.EntityId);

        var completedAt = DateTimeOffset.UtcNow;

        var payload = await BuildPayload(analysis, summary, processState.TotalCost, completedAt, cancellationToken);

        var webhook = await context.Webhooks.AsNoTracking()
            .Where(s => s.UserId == analysis.UserId && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        // بدون webhook گزارش نمی‌رود، ولی پایش تمام شده و داده در LinkRadar هست
        if (webhook is null)
        {
            logger.LogWarning(
                "No Webhook For This ProcessStateId = {ProcessStateId}, UserId = {UserId}, EntityId = {EntityId}, Complete Without Notify",
                processState.Id, analysis.UserId, analysis.EntityId);
        }
        else
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(payload, PayloadJsonOptions);

            logger.LogInformation(
                "Send Webhook For This ProcessStateId = {ProcessStateId}, EntityId = {EntityId}, WebhookUrl = {WebhookUrl}, PayloadBytes = {PayloadBytes}",
                processState.Id, analysis.EntityId, webhook.WebhookUrl, body.Length);

            var client = clientFactory.CreateClient(HttpClientConfiguration.WebhookClientName);

            var content = new ByteArrayContent(body);
            content.Headers.ContentType = new("application/json") { CharSet = "utf-8" };

            using var request = new HttpRequestMessage(HttpMethod.Post, webhook.WebhookUrl) { Content = content };
            request.Headers.TryAddWithoutValidation(webhook.HeaderName, webhook.HeaderValue);

            HttpResponseMessage response;

            try
            {
                response = await client.SendAsync(request, cancellationToken);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException
                                      && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(e,
                    "Webhook Request Failed For This ProcessStateId = {ProcessStateId}, EntityId = {EntityId}, WebhookUrl = {WebhookUrl}",
                    processState.Id, analysis.EntityId, webhook.WebhookUrl);

                return OperationResult.FailureResult(
                    Error.ExternalService(ErrorCodes.Webhook.RequestFailed, e.Message, isTransient: true));
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                    logger.LogWarning(
                        "Webhook Rejected For This ProcessStateId = {ProcessStateId}, EntityId = {EntityId}, StatusCode = {StatusCode}, Body = {Body}",
                        processState.Id, analysis.EntityId, (int)response.StatusCode, responseBody);

                    // ajebhom در دسترس نیست یا رد کرده؛ Hangfire دوباره می‌فرستد
                    return OperationResult.FailureResult(Error.ExternalService(ErrorCodes.Webhook.Rejected,
                        $"HTTP {(int)response.StatusCode}", isTransient: true));
                }
            }
        }

        processState.Status = ProcessStatus.Completed;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Send To User Webhook For This ProcessStateId = {ProcessStateId}, EntityId = {EntityId}, Notified = {Notified}, HasBacklinks = {HasBacklinks}, TotalCost = {TotalCost}",
            processState.Id, analysis.EntityId, webhook is not null, payload.HasBacklinks, processState.TotalCost);

        return OperationResult.SuccessResult();
    }

    private async Task<AnalysisReportWebhook> BuildPayload(Analysis analysis, BacklinkSummary summary,
        decimal totalCost, DateTimeOffset completedAt, CancellationToken cancellationToken)
    {
        var context = commonServices.Context;
        var analysisId = analysis.Id;

        // مسیر بدون بک‌لینک مستقیم از CheckDomainSummary به اینجا می‌آید و معیار ندارد
        var metrics = await context.AnalysisMetrics.AsNoTracking()
            .FirstOrDefaultAsync(s => s.AnalysisId == analysisId, cancellationToken);
        var report = await context.AnalysisReports.AsNoTracking()
            .FirstOrDefaultAsync(s => s.AnalysisId == analysisId, cancellationToken);

        var dofollowDomainsPct = summary.ReferringDomains == 0
            ? 0
            : Math.Round((decimal)(summary.ReferringDomains - summary.ReferringDomainsNofollow) * 100 / summary.ReferringDomains, 2);

        var overview = new WebhookOverview(
            metrics?.HealthScore,
            metrics?.HealthStatus,
            metrics is null
                ? null
                : new WebhookScores(metrics.Detail.AuthorityScore, metrics.Detail.QualityScore,
                    metrics.Detail.DiversityScore, metrics.Detail.CompetitionScore, metrics.Detail.TrendScore),
            summary.Rank,
            summary.Backlinks,
            summary.ReferringDomains,
            summary.ReferringMainDomains,
            summary.ReferringPages,
            dofollowDomainsPct,
            summary.BrokenBacklinks,
            summary.BacklinksSpamScore,
            summary.FirstSeen);

        var payload = new AnalysisReportWebhook(
            SchemaVersion,
            analysisId,
            analysis.EntityId,
            analysis.Target,
            ToDisplay(analysis.Target),
            analysis.TargetType.ToString(),
            analysis.CountryCode,
            analysis.Keyword,
            analysis.BrandName,
            completedAt,
            HasBacklinks: metrics is not null,
            totalCost,
            overview,
            Report: null,
            Insights: [],
            Competition: null,
            Opportunities: [],
            Risks: null,
            LostDomains: [],
            BrokenPages: [],
            Anchors: null,
            Profile: null,
            Trend: null,
            Backlinks: []);

        if (metrics is null)
            return payload;

        var detail = metrics.Detail;

        var referringDomains = await context.ReferringDomains.AsNoTracking()
            .Where(s => s.AnalysisId == analysisId).ToListAsync(cancellationToken);
        var backlinks = await context.Backlinks.AsNoTracking()
            .Where(s => s.AnalysisId == analysisId).ToListAsync(cancellationToken);
        var anchors = await context.BacklinkAnchors.AsNoTracking()
            .Where(s => s.AnalysisId == analysisId).ToListAsync(cancellationToken);
        var timeSeries = await context.BacklinkTimeSeries.AsNoTracking()
            .Where(s => s.AnalysisId == analysisId).ToListAsync(cancellationToken);
        var competitors = await context.Set<Competitor>().AsNoTracking()
            .Where(s => s.AnalysisId == analysisId).ToListAsync(cancellationToken);
        var opportunities = await context.LinkOpportunities.AsNoTracking()
            .Where(s => s.AnalysisId == analysisId)
            .OrderByDescending(s => s.Score)
            .Take(MaxOpportunities)
            .ToListAsync(cancellationToken);

        var liveBacklinks = backlinks.Where(s => !s.IsLost).ToList();

        return payload with
        {
            Report = report is null ? null : BuildReport(report),
            Insights = metrics.Insights.Select(s => new WebhookInsight(s.Code, s.Type, s.Severity, s.Value)).ToList(),
            Competition = BuildCompetition(analysis, summary, detail, competitors),
            Opportunities = opportunities.Select(s => new WebhookOpportunity(s.Domain, ToDisplay(s.Domain), s.Category,
                s.Score, s.Rank, s.BacklinksSpamScore, s.CompetitorCount, s.PageUrl, s.PageAnchor,
                s.PageCompetitorDomain)).ToList(),
            Risks = BuildRisks(detail, referringDomains, anchors),
            LostDomains = BuildLostDomains(backlinks, referringDomains),
            BrokenPages = BuildBrokenPages(liveBacklinks),
            Anchors = BuildAnchors(analysis, detail, anchors),
            Profile = BuildProfile(detail, referringDomains, liveBacklinks),
            Trend = BuildTrend(detail, timeSeries),
            Backlinks = liveBacklinks
                .OrderByDescending(s => s.Rank)
                .ThenByDescending(s => s.DomainFromRank)
                .Take(MaxBacklinks)
                .Select(s => new WebhookBacklink(s.DomainFrom, ToDisplay(s.DomainFrom), s.UrlFrom, s.UrlTo, s.Anchor,
                    s.DoFollow, s.Rank, s.DomainFromRank, s.BacklinkSpamScore, s.SemanticLocation, s.ItemType,
                    s.IsBroken, s.FirstSeen, s.LastSeen))
                .ToList()
        };
    }

    #region Sections

    private static WebhookReport BuildReport(AnalysisReport report) =>
        new(ToText(report.Summary),
            report.Findings.Select(s => new WebhookFinding(ToText(s.Title), ToText(s.Description), s.Type)).ToList(),
            report.Actions.Select(s => new WebhookAction(ToText(s.Title), ToText(s.Description), s.Priority)).ToList());

    private static WebhookText ToText(LocalizedText text) => new(text.Ar, text.En);

    private static WebhookCompetition BuildCompetition(Analysis analysis, BacklinkSummary summary,
        MetricsDetail detail, List<Competitor> competitors)
    {
        var userRow = new WebhookCompetitorRow(
            ComputeMetrics.GetDomain(analysis.Target),
            ToDisplay(ComputeMetrics.GetDomain(analysis.Target)),
            IsUser: true,
            Source: null,
            summary.Rank,
            summary.Backlinks,
            summary.ReferringDomains,
            summary.ReferringDomains - summary.ReferringDomainsNofollow,
            summary.BrokenBacklinks,
            summary.FirstSeen,
            detail.ContentLinksPct);

        var competitorRows = competitors.Select(s => new WebhookCompetitorRow(
            s.Domain,
            ToDisplay(s.Domain),
            IsUser: false,
            s.Source,
            s.Rank,
            s.Backlinks,
            s.ReferringDomains,
            s.ReferringDomains - s.ReferringDomainsNofollow,
            s.BrokenBacklinks,
            s.FirstSeen,
            s.ContentLinksPct));

        // رقیب بدون آمار ته جدول می‌رود
        var rows = competitorRows.Append(userRow)
            .OrderByDescending(s => s.ReferringDomains.HasValue)
            .ThenByDescending(s => s.ReferringDomains)
            .ToList();

        return new WebhookCompetition(
            detail.MarketPosition,
            detail.CompetitorsCount,
            detail.CompetitorsMedianReferringDomains,
            detail.ReferringDomainsGapToMedian,
            detail.ReachableCompetitorDomain,
            detail.ReachableCompetitorDomain is null ? null : ToDisplay(detail.ReachableCompetitorDomain),
            rows);
    }

    private static WebhookRisks BuildRisks(MetricsDetail detail, List<ReferringDomain> referringDomains,
        List<BacklinkAnchor> anchors)
    {
        var domains = referringDomains
            .Where(s => s.BacklinksSpamScore > ComputeMetrics.RiskySpamScore)
            .OrderByDescending(s => s.Backlinks)
            .Take(MaxRiskyDomains)
            .Select(s => new WebhookRiskyDomain(s.Domain, ToDisplay(s.Domain), s.BacklinksSpamScore, s.Rank, s.Backlinks))
            .ToList();

        var riskyAnchors = anchors
            .Where(s => s.BacklinksSpamScore > ComputeMetrics.RiskySpamScore)
            .OrderByDescending(s => s.ReferringDomains)
            .Take(MaxRiskyAnchors)
            .Select(s => new WebhookRiskyAnchor(s.Anchor, s.BacklinksSpamScore, s.ReferringDomains, s.Backlinks))
            .ToList();

        return new WebhookRisks(detail.RiskyDomainsCount, detail.RiskyDomainsPct, detail.RiskyAnchorDomainsPct,
            detail.SpikeMonth, detail.SpikeNewBacklinks, domains, riskyAnchors);
    }

    private static List<WebhookLostDomain> BuildLostDomains(List<Backlink> backlinks,
        List<ReferringDomain> referringDomains)
    {
        // مثل ComputeMetrics: دامنه‌ای که هنوز لینک زنده دارد از دست رفته نیست
        var liveDomains = backlinks.Where(s => !s.IsLost).Select(s => s.DomainFrom)
            .Concat(referringDomains.Select(s => s.Domain)).ToList();

        return backlinks
            .Where(s => s.IsLost && !liveDomains.Any(live => ComputeMetrics.IsSameOrSubdomain(s.DomainFrom, live)))
            .GroupBy(s => s.DomainFrom)
            .Select(s => s.OrderByDescending(b => b.Rank).First())
            .OrderByDescending(s => s.Rank)
            .Select(s => new WebhookLostDomain(s.DomainFrom, ToDisplay(s.DomainFrom), s.Rank, s.UrlFrom, s.Anchor,
                s.LastSeen))
            .ToList();
    }

    private static List<WebhookBrokenPage> BuildBrokenPages(List<Backlink> liveBacklinks) =>
        // همان شرط ComputeMetrics: ریدایرکت خراب نیست
        liveBacklinks
            .Where(s => s.IsBroken || s.UrlToStatusCode >= 400)
            .GroupBy(s => s.UrlTo)
            .Select(s => new WebhookBrokenPage(s.Key, s.Max(b => b.UrlToStatusCode),
                s.Select(b => b.DomainFrom).Distinct().Count()))
            .OrderByDescending(s => s.ReferringDomains)
            .Take(MaxBrokenPages)
            .ToList();

    private static WebhookAnchors BuildAnchors(Analysis analysis, MetricsDetail detail, List<BacklinkAnchor> anchors)
    {
        // همان توکن‌ها و دسته‌بندی ComputeMetrics تا درصدها و دسته‌ی هر انکر یکی باشند
        var brandTokens = ComputeMetrics.GetBrandTokens(ComputeMetrics.GetDomain(analysis.Target), analysis.BrandName,
            analysis.BrandAliases);

        var top = anchors
            .OrderByDescending(s => s.ReferringDomains)
            .ThenByDescending(s => s.Backlinks)
            .Take(MaxAnchors)
            .Select(s => new WebhookAnchor(
                s.Anchor,
                ComputeMetrics.ClassifyAnchor(s.Anchor, brandTokens),
                s.ReferringDomains,
                s.Backlinks,
                Pct(s.ReferringDomains - s.ReferringDomainsNofollow, s.ReferringDomains),
                s.BacklinksSpamScore))
            .ToList();

        return new WebhookAnchors(detail.AnchorBrandPct, detail.AnchorUrlPct, detail.AnchorGenericPct,
            detail.AnchorNoTextPct, detail.AnchorOtherPct, top);
    }

    private static WebhookProfile BuildProfile(MetricsDetail detail, List<ReferringDomain> referringDomains,
        List<Backlink> liveBacklinks)
    {
        // Backlinks یک لینک از هر دامنه دارد، پس شمارش بر اساس دامنه است
        var locations = liveBacklinks
            .Where(s => s.SemanticLocation is not null)
            .GroupBy(s => s.SemanticLocation!)
            .Select(s => new WebhookLocation(s.Key, s.Count()))
            .OrderByDescending(s => s.Domains)
            .ToList();

        var totalBacklinks = referringDomains.Sum(s => s.Backlinks);

        var topDomains = referringDomains
            .OrderByDescending(s => s.Backlinks)
            .Take(MaxTopDomains)
            .Select(s => new WebhookTopDomain(s.Domain, ToDisplay(s.Domain), s.Rank, s.Backlinks,
                Pct(s.Backlinks, totalBacklinks), s.BacklinksSpamScore))
            .ToList();

        return new WebhookProfile(detail.StrongDomainsCount, detail.MediumDomainsCount, detail.WeakDomainsCount,
            detail.DofollowDomainsPct, detail.ContentLinksPct, detail.KnownLocationDomainsCount,
            detail.Top5DomainsBacklinkPct, locations, topDomains);
    }

    private static WebhookTrend BuildTrend(MetricsDetail detail, List<BacklinkTimeSeries> timeSeries)
    {
        var now = DateTimeOffset.UtcNow;
        var currentMonthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var months = timeSeries.OrderBy(s => s.Date).Select(s => new WebhookTrendMonth(
                s.Date,
                IsPartial: s.Date >= currentMonthStart,
                s.Rank,
                s.Backlinks,
                s.ReferringDomains,
                s.NewBacklinks,
                s.LostBacklinks,
                s.NewReferringDomains,
                s.LostReferringDomains))
            .ToList();

        return new WebhookTrend(detail.ReferringDomains12MonthsAgo, detail.NetReferringDomains12MonthsPct, months);
    }

    #endregion

    private static decimal Pct(decimal part, decimal total) => total == 0 ? 0 : Math.Round(part * 100 / total, 2);

    // دامنه‌های punycode مثل xn--... با حروف اصلی نمایش داده شوند
    private static string ToDisplay(string domain)
    {
        try
        {
            return new IdnMapping().GetUnicode(domain);
        }
        catch (ArgumentException)
        {
            return domain;
        }
    }
}
