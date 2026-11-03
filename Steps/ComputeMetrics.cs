using System.Text;
using System.Text.RegularExpressions;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.ResultPattern;
using LinkRadar.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

// تماس بیرونی ندارد؛ از جدول‌های خودمان حساب می‌کند و هر وقت فرمول عوض شد دوباره اجرا می‌شود
public class ComputeMetrics(CommonServices<ComputeMetrics> commonServices) : IProcessStep
{
    // با هر تغییر فرمول یکی بالا برود
    private const int MetricsVersion = 1;

    // این آستانه‌ها و وزن‌ها از یک سایت درآمده‌اند و با داده‌ی بیشتر باید تنظیم شوند
    internal const int RiskySpamScore = 50;
    private const int StrongRank = 30;
    private const int MediumRank = 10;
    private const int MaxOpportunities = 100;
    private const int MinSpikeNewBacklinks = 100;
    private const decimal SpikeMultiplier = 3;

    private static readonly string[] GenericAnchors =
    [
        "click here", "here", "read more", "more", "visit", "visit website", "website", "link", "this", "source",
        "اضغط هنا", "هنا", "اقرأ المزيد", "المزيد", "الموقع", "رابط", "زيارة الموقع", "التفاصيل", "المصدر",
        "اینجا", "کلیک کنید", "اینجا را کلیک کنید", "بیشتر بخوانید", "ادامه مطلب", "مشاهده", "لینک", "سایت", "وب سایت"
    ];

    private static readonly Regex DomainLikeAnchor = new(@"^[a-z0-9-]+(\.[a-z0-9-]+)+(/\S*)?$", RegexOptions.Compiled);

    private static readonly HashSet<string> NormalizedGenericAnchors = GenericAnchors.Select(Compact).ToHashSet();

    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.ComputeMetrics;

    public async Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken)
    {
        var context = commonServices.Context;
        var logger = commonServices.Logger;
        var analysisId = processState.AnalysisId;

        var analysis = await commonServices.Context.Analyses.Where(s => s.Id == analysisId)
            .Select(s => new { s.Target, s.BrandName, s.BrandAliases })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var summary = await context.BacklinkSummaries.AsNoTracking()
            .FirstOrDefaultAsync(s => s.AnalysisId == analysisId, cancellationToken);

        if (analysis is null || summary is null)
        {
            logger.LogError(
                "Can Not Find Analysis Or Summary For This ProcessStateId = {ProcessStateId}, AnalysisId = {AnalysisId}",
                processState.Id, analysisId);

            return OperationResult.FailureResult(Error.NotFound(ErrorCodes.Analysis.InvalidTarget,
                "Analysis Or Summary Not Found"));
        }

        logger.LogInformation("Start Compute Metrics For This ProcessStateId = {ProcessStateId}, Target = {Target}",
            processState.Id, analysis.Target);

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
        var competitorIds = competitors.Select(s => s.Id).ToList();
        var linkGapDomains = await context.LinkGapDomains.AsNoTracking()
            .Where(s => competitorIds.Contains(s.CompetitorId)).ToListAsync(cancellationToken);

        var targetDomain = GetDomain(analysis.Target);
        var liveBacklinks = backlinks.Where(s => !s.IsLost).ToList();
        var lostBacklinks = backlinks.Where(s => s.IsLost).ToList();

        var detail = new MetricsDetail();

        FillDiversity(detail, referringDomains, anchors, targetDomain, analysis.BrandName, analysis.BrandAliases);
        FillQuality(detail, summary, referringDomains, liveBacklinks);
        FillTrend(detail, timeSeries);
        FillLostAndBroken(detail, liveBacklinks, lostBacklinks, referringDomains);
        FillCompetition(detail, summary, competitors);

        var opportunities = BuildOpportunities(linkGapDomains, competitors, targetDomain, analysisId);
        detail.OpportunityCount = opportunities.Count;
        detail.MultiCompetitorOpportunityCount = opportunities.Count(s => s.CompetitorCount >= 2);

        var healthScore = FillScores(detail, summary);

        var metrics = new AnalysisMetrics
        {
            AnalysisId = analysisId,
            HealthScore = healthScore,
            HealthStatus = healthScore >= 70 ? HealthStatus.Good : healthScore >= 40 ? HealthStatus.Medium : HealthStatus.Weak,
            MetricsVersion = MetricsVersion,
            Detail = detail,
            Insights = BuildInsights(detail, summary)
        };

        // دوباره‌اجراشدنی: نتیجه‌ی قبلی همین پایش جایگزین می‌شود
        // بدون خواندن ردیف حذف می‌شود، چون JSON ردیف‌های نسخه‌ی قدیمی ممکن است با کلاس فعلی خوانده نشود
        await context.AnalysisMetrics.Where(s => s.AnalysisId == analysisId).ExecuteDeleteAsync(cancellationToken);
        await context.LinkOpportunities.Where(s => s.AnalysisId == analysisId).ExecuteDeleteAsync(cancellationToken);

        context.AnalysisMetrics.Add(metrics);
        context.LinkOpportunities.AddRange(opportunities.OrderByDescending(s => s.Score).Take(MaxOpportunities));

        processState.CurrentStep = CurrentStep.GetOpportunityPages;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Compute Metrics For This ProcessStateId = {ProcessStateId}, HealthScore = {HealthScore}, HealthStatus = {HealthStatus}, OpportunityCount = {OpportunityCount}, InsightCount = {InsightCount}, Next Step = {NextStep}",
            processState.Id, metrics.HealthScore, metrics.HealthStatus, detail.OpportunityCount, metrics.Insights.Count,
            processState.CurrentStep);

        return OperationResult.SuccessResult();
    }

    #region Diversity

    private static void FillDiversity(MetricsDetail detail, List<ReferringDomain> referringDomains,
        List<BacklinkAnchor> anchors, string targetDomain, string? brandName, List<string> brandAliases)
    {
        var totalBacklinks = referringDomains.Sum(s => s.Backlinks);
        var top5Backlinks = referringDomains.OrderByDescending(s => s.Backlinks).Take(5).Sum(s => s.Backlinks);
        detail.Top5DomainsBacklinkPct = Pct(top5Backlinks, totalBacklinks);

        // سهم هر دسته بر اساس دامنه، تا لینک‌های فوتر سراسری نتیجه را خراب نکنند
        var brandTokens = GetBrandTokens(targetDomain, brandName, brandAliases);
        var totalDomains = anchors.Sum(s => s.ReferringDomains);
        var byCategory = anchors
            .GroupBy(s => ClassifyAnchor(s.Anchor, brandTokens))
            .ToDictionary(s => s.Key, s => s.Sum(a => a.ReferringDomains));

        detail.AnchorBrandPct = Pct(byCategory.GetValueOrDefault(AnchorCategory.Brand), totalDomains);
        detail.AnchorUrlPct = Pct(byCategory.GetValueOrDefault(AnchorCategory.Url), totalDomains);
        detail.AnchorGenericPct = Pct(byCategory.GetValueOrDefault(AnchorCategory.Generic), totalDomains);
        detail.AnchorNoTextPct = Pct(byCategory.GetValueOrDefault(AnchorCategory.NoText), totalDomains);
        detail.AnchorOtherPct = Pct(byCategory.GetValueOrDefault(AnchorCategory.Other), totalDomains);

        detail.RiskyAnchorDomainsPct = Pct(
            anchors.Where(s => s.BacklinksSpamScore > RiskySpamScore).Sum(s => s.ReferringDomains), totalDomains);
    }

    internal static AnchorCategory ClassifyAnchor(string? anchor, List<string> brandTokens)
    {
        if (string.IsNullOrWhiteSpace(anchor))
            return AnchorCategory.NoText;

        var lower = anchor.Trim().ToLowerInvariant();

        // فقط وقتی کل انکر آدرس است؛ متنی که آدرس داخلش آمده URL حساب نمی‌شود
        if (lower.StartsWith("http://") || lower.StartsWith("https://") || lower.StartsWith("www.")
            || DomainLikeAnchor.IsMatch(lower))
            return AnchorCategory.Url;

        var compact = Compact(anchor);

        if (brandTokens.Any(compact.Contains))
            return AnchorCategory.Brand;

        // تطبیق کامل، نه Contains، تا انکرهای واقعی به‌اشتباه عمومی حساب نشوند
        return NormalizedGenericAnchors.Contains(compact) ? AnchorCategory.Generic : AnchorCategory.Other;
    }

    internal static List<string> GetBrandTokens(string targetDomain, string? brandName, List<string> brandAliases)
    {
        var tokens = new List<string> { Compact(targetDomain.Split('.')[0]) };

        if (!string.IsNullOrWhiteSpace(brandName))
            tokens.Add(Compact(brandName));

        tokens.AddRange(brandAliases.Select(Compact));

        // توکن خیلی کوتاه انکرهای بی‌ربط را برند حساب می‌کند
        return tokens.Where(s => s.Length >= 3).Distinct().ToList();
    }

    // یکسان‌سازی حروف عربی و فارسی و حذف فاصله، تا «وبمستر سلام» و «وبمسترسلام» یکی شوند
    // DetectBrandAliases هم از همین استفاده می‌کند تا چک املا و دسته‌بندی یکی باشد
    internal static string Compact(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var c in value.ToLowerInvariant())
        {
            switch (c)
            {
                case 'أ' or 'إ' or 'آ' or 'ٱ':
                    builder.Append('ا');
                    break;
                case 'ة':
                    builder.Append('ه');
                    break;
                case 'ى' or 'ی':
                    builder.Append('ي');
                    break;
                case 'ک':
                    builder.Append('ك');
                    break;
                // تشکیل، کشیده، نیم‌فاصله و فاصله حذف می‌شوند
                case >= 'ً' and <= 'ٟ' or 'ٰ' or 'ـ' or '‌':
                    break;
                default:
                    if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c))
                        builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    public enum AnchorCategory
    {
        Brand,
        Url,
        Generic,
        NoText,
        Other
    }

    #endregion

    #region Quality

    private static void FillQuality(MetricsDetail detail, BacklinkSummary summary,
        List<ReferringDomain> referringDomains, List<Backlink> liveBacklinks)
    {
        // سطح دامنه، مثل کیوردچی: دامنه‌هایی که فقط nofollow داده‌اند کم می‌شوند
        detail.DofollowDomainsPct = Pct(summary.ReferringDomains - summary.ReferringDomainsNofollow,
            summary.ReferringDomains);

        detail.RiskyDomainsCount = referringDomains.Count(s => s.BacklinksSpamScore > RiskySpamScore);
        detail.RiskyDomainsPct = Pct(detail.RiskyDomainsCount, referringDomains.Count);

        detail.StrongDomainsCount = referringDomains.Count(s => s.Rank >= StrongRank);
        detail.MediumDomainsCount = referringDomains.Count(s => s.Rank is >= MediumRank and < StrongRank);
        detail.WeakDomainsCount = referringDomains.Count(s => s.Rank < MediumRank);

        // Backlinks یک لینک از هر دامنه دارد، پس این درصد بر اساس دامنه است
        var knownLocation = liveBacklinks.Where(s => s.SemanticLocation is not null).ToList();
        detail.KnownLocationDomainsCount = knownLocation.Count;
        detail.ContentLinksPct = knownLocation.Count == 0
            ? null
            : Pct(knownLocation.Count(s => GetCompetitorSummaries.ContentLocations.Contains(s.SemanticLocation)),
                knownLocation.Count);
    }

    #endregion

    #region Trend

    private static void FillTrend(MetricsDetail detail, List<BacklinkTimeSeries> timeSeries)
    {
        // ماه جاری هنوز کامل نشده و نباید با ماه‌های کامل مقایسه شود
        var now = DateTimeOffset.UtcNow;
        var currentMonthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var completeMonths = timeSeries.Where(s => s.Date < currentMonthStart).OrderBy(s => s.Date).ToList();

        if (completeMonths.Count == 0)
            return;

        var latest = completeMonths[^1];
        var yearAgoDate = latest.Date.AddMonths(-12);
        var yearAgo = completeMonths.FirstOrDefault(s => s.Date.Year == yearAgoDate.Year && s.Date.Month == yearAgoDate.Month);

        if (yearAgo is not null)
        {
            detail.ReferringDomains12MonthsAgo = yearAgo.ReferringDomains;
            detail.NetReferringDomains12MonthsPct = yearAgo.ReferringDomains == 0
                ? null
                : Pct(latest.ReferringDomains - yearAgo.ReferringDomains, yearAgo.ReferringDomains);
        }

        // جهش: ماهی در ۱۲ ماه اخیر که لینک جدیدش چند برابر میانگین ماه‌های قبلش باشد
        BacklinkTimeSeries? spike = null;

        for (var i = Math.Max(6, completeMonths.Count - 12); i < completeMonths.Count; i++)
        {
            var month = completeMonths[i];
            var previous = completeMonths.Skip(Math.Max(0, i - 12)).Take(Math.Min(12, i)).ToList();
            var average = (decimal)previous.Average(s => s.NewBacklinks);

            if (month.NewBacklinks >= MinSpikeNewBacklinks && month.NewBacklinks > average * SpikeMultiplier
                                                           && (spike is null || month.NewBacklinks > spike.NewBacklinks))
                spike = month;
        }

        detail.SpikeMonth = spike?.Date;
        detail.SpikeNewBacklinks = spike?.NewBacklinks;
    }

    #endregion

    #region Lost And Broken

    private static void FillLostAndBroken(MetricsDetail detail, List<Backlink> liveBacklinks,
        List<Backlink> lostBacklinks, List<ReferringDomain> referringDomains)
    {
        // دامنه‌ای که هنوز لینک زنده دارد، از دست رفته نیست
        var liveDomains = liveBacklinks.Select(s => s.DomainFrom).Concat(referringDomains.Select(s => s.Domain)).ToList();

        detail.LostDomainsCount = lostBacklinks
            .Select(s => s.DomainFrom)
            .Distinct()
            .Count(domain => !liveDomains.Any(live => IsSameOrSubdomain(domain, live)));

        // ریدایرکت خراب نیست؛ فقط is_broken یا وضعیت ۴۰۰ به بالا
        detail.BrokenPagesCount = liveBacklinks
            .Where(s => s.IsBroken || s.UrlToStatusCode >= 400)
            .Select(s => s.UrlTo)
            .Distinct()
            .Count();
    }

    #endregion

    #region Competition

    private static void FillCompetition(MetricsDetail detail, BacklinkSummary summary, List<Competitor> competitors)
    {
        // رقیبی که آمارش گرفته نشد در مقایسه حساب نمی‌شود
        var withStats = competitors.Where(s => s.ReferringDomains is not null).ToList();
        detail.CompetitorsCount = withStats.Count;

        if (withStats.Count == 0)
            return;

        var userDomains = summary.ReferringDomains;
        var sorted = withStats.Select(s => s.ReferringDomains!.Value).OrderBy(s => s).ToList();
        var median = sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;

        detail.MarketPosition = 1 + withStats.Count(s => s.ReferringDomains > userDomains);
        detail.CompetitorsMedianReferringDomains = median;
        detail.ReferringDomainsGapToMedian = Math.Max(0, median - userDomains);
        detail.ReachableCompetitorDomain = withStats
            .Where(s => s.ReferringDomains > userDomains)
            .OrderBy(s => s.ReferringDomains)
            .FirstOrDefault()?.Domain;
    }

    #endregion

    #region Opportunity

    private static List<LinkOpportunity> BuildOpportunities(List<LinkGapDomain> linkGapDomains,
        List<Competitor> competitors, string targetDomain, int analysisId)
    {
        // رقبا به هم لینک می‌دهند؛ پیشنهاد لینک گرفتن از رقیب فرصت نیست
        var excluded = competitors.Select(s => s.Domain).Append(targetDomain).ToList();

        return linkGapDomains
            .Where(s => !excluded.Any(e => IsSameOrSubdomain(s.Domain, e)))
            .GroupBy(s => s.Domain)
            .Select(s => new
            {
                Domain = s.Key,
                CompetitorCount = s.Select(g => g.CompetitorId).Distinct().Count(),
                Rank = s.Max(g => g.Rank),
                SpamScore = s.Max(g => g.BacklinksSpamScore)
            })
            .Where(s => s.SpamScore <= RiskySpamScore)
            .Select(s => new LinkOpportunity
            {
                AnalysisId = analysisId,
                Domain = s.Domain,
                CompetitorCount = s.CompetitorCount,
                Rank = s.Rank,
                BacklinksSpamScore = s.SpamScore,
                Score = Math.Round(s.Rank * 0.5m + Math.Min(s.CompetitorCount, 3) * 10 + (RiskySpamScore - s.SpamScore) * 0.4m, 2)
            })
            .ToList();
    }

    #endregion

    #region Score

    private static decimal FillScores(MetricsDetail detail, BacklinkSummary summary)
    {
        detail.AuthorityScore = Clamp(summary.Rank);
        detail.QualityScore = Clamp(0.6m * (100 - detail.RiskyDomainsPct) + 0.4m * detail.DofollowDomainsPct);
        detail.DiversityScore = Clamp(100 - detail.Top5DomainsBacklinkPct);
        detail.CompetitionScore = detail.CompetitorsMedianReferringDomains is > 0
            ? Clamp((decimal)summary.ReferringDomains / detail.CompetitorsMedianReferringDomains.Value * 100)
            : null;
        detail.TrendScore = Clamp(50 + (detail.NetReferringDomains12MonthsPct ?? 0));

        var parts = new List<(decimal Score, decimal Weight)>
        {
            (detail.AuthorityScore, 0.20m),
            (detail.QualityScore, 0.30m),
            (detail.DiversityScore, 0.20m),
            (detail.TrendScore, 0.15m)
        };

        // بدون رقیب، وزن رقابت بین بقیه پخش می‌شود
        if (detail.CompetitionScore is not null)
            parts.Add((detail.CompetitionScore.Value, 0.15m));

        return Math.Round(parts.Sum(s => s.Score * s.Weight) / parts.Sum(s => s.Weight), 2);
    }

    #endregion

    #region Insight

    private static List<InsightItem> BuildInsights(MetricsDetail detail, BacklinkSummary summary)
    {
        var insights = new List<InsightItem>();

        void Add(InsightCode code, InsightType type, InsightSeverity severity, decimal? value) =>
            insights.Add(new InsightItem { Code = code, Type = type, Severity = severity, Value = value });

        if (summary.Rank >= 50)
            Add(InsightCode.HighAuthority, InsightType.Positive, InsightSeverity.Low, summary.Rank);
        else if (summary.Rank < 20)
            Add(InsightCode.LowAuthority, InsightType.Problem, InsightSeverity.Medium, summary.Rank);

        if (detail.Top5DomainsBacklinkPct >= 50)
            Add(InsightCode.HighLinkConcentration,
                detail.Top5DomainsBacklinkPct >= 70 ? InsightType.Problem : InsightType.Warning,
                detail.Top5DomainsBacklinkPct >= 70 ? InsightSeverity.High : InsightSeverity.Medium,
                detail.Top5DomainsBacklinkPct);

        if (detail.RiskyDomainsPct >= 20)
            Add(InsightCode.HighRiskyDomains, InsightType.Problem,
                detail.RiskyDomainsPct >= 35 ? InsightSeverity.High : InsightSeverity.Medium, detail.RiskyDomainsPct);

        if (detail.RiskyAnchorDomainsPct >= 10)
            Add(InsightCode.RiskyAnchors, InsightType.Warning, InsightSeverity.Medium, detail.RiskyAnchorDomainsPct);

        if (detail.DofollowDomainsPct < 40)
            Add(InsightCode.LowDofollow, InsightType.Warning, InsightSeverity.Medium, detail.DofollowDomainsPct);

        if (detail.NetReferringDomains12MonthsPct >= 10)
            Add(InsightCode.GrowingReferringDomains, InsightType.Positive, InsightSeverity.Low,
                detail.NetReferringDomains12MonthsPct);
        else if (detail.NetReferringDomains12MonthsPct <= -10)
            Add(InsightCode.DecliningReferringDomains, InsightType.Problem,
                detail.NetReferringDomains12MonthsPct <= -25 ? InsightSeverity.High : InsightSeverity.Medium,
                detail.NetReferringDomains12MonthsPct);

        if (detail.SpikeMonth is not null)
            Add(InsightCode.UnnaturalSpike, InsightType.Warning, InsightSeverity.High, detail.SpikeNewBacklinks);

        if (detail.LostDomainsCount >= 5)
            Add(InsightCode.LostDomains, InsightType.Warning, InsightSeverity.Medium, detail.LostDomainsCount);

        if (detail.BrokenPagesCount > 0)
            Add(InsightCode.BrokenPages, InsightType.Warning, InsightSeverity.Medium, detail.BrokenPagesCount);

        if (detail.MarketPosition == 1)
            Add(InsightCode.AheadOfCompetitors, InsightType.Positive, InsightSeverity.Low, detail.CompetitorsCount);
        else if (detail.CompetitionScore < 50)
            Add(InsightCode.FarBehindCompetitors, InsightType.Problem,
                detail.CompetitionScore < 25 ? InsightSeverity.High : InsightSeverity.Medium,
                detail.ReferringDomainsGapToMedian);

        return insights;
    }

    #endregion

    private static decimal Pct(decimal part, decimal total) => total == 0 ? 0 : Math.Round(part * 100 / total, 2);

    private static decimal Clamp(decimal value) => Math.Round(Math.Clamp(value, 0, 100), 2);

    // GenerateAiAnalysis هم از همین استفاده می‌کند تا «از دست رفته» در هر دو یکی باشد
    internal static bool IsSameOrSubdomain(string domain, string parent) =>
        domain == parent || domain.EndsWith("." + parent, StringComparison.Ordinal);

    // برای حالت صفحه، دامنه از آدرس درمی‌آید
    internal static string GetDomain(string target)
    {
        var host = Uri.TryCreate(target, UriKind.Absolute, out var uri) ? uri.Host : target;

        return host.StartsWith("www.") ? host[4..] : host;
    }
}
