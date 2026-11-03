using LinkRadar.Api.Entities;
using LinkRadar.Api.Steps;

namespace LinkRadar.Api.Dtos.Webhook;

// گزارش کامل و آماده‌ی نمایش؛ ajebhom فقط نشانش می‌دهد و چیزی حساب نمی‌کند
// پیام ممکن است با retry دوباره برسد؛ ajebhom بر اساس EntityId جایگزین کند نه اضافه
// در پایش بدون بک‌لینک بخش‌های شیء null و لیست‌ها خالی‌اند
public record AnalysisReportWebhook(
    int SchemaVersion,
    int AnalysisId,
    int EntityId,
    string Target,
    string DisplayTarget,
    string TargetType,
    string CountryCode,
    string? Keyword,
    string? BrandName,
    DateTimeOffset CompletedAt,
    bool HasBacklinks,
    decimal TotalCost,
    WebhookOverview Overview,
    WebhookReport? Report,
    List<WebhookInsight> Insights,
    WebhookCompetition? Competition,
    List<WebhookOpportunity> Opportunities,
    WebhookRisks? Risks,
    List<WebhookLostDomain> LostDomains,
    List<WebhookBrokenPage> BrokenPages,
    WebhookAnchors? Anchors,
    WebhookProfile? Profile,
    WebhookTrend? Trend,
    List<WebhookBacklink> Backlinks);

#region Overview

public record WebhookOverview(
    decimal? HealthScore,
    HealthStatus? HealthStatus,
    WebhookScores? Scores,
    int Rank,
    long Backlinks,
    long ReferringDomains,
    long ReferringMainDomains,
    long ReferringPages,
    decimal DofollowDomainsPct,
    long BrokenBacklinks,
    int BacklinksSpamScore,
    DateTimeOffset? FirstSeen);

// هر کدام ۰ تا ۱۰۰؛ Competition بدون رقیب null است
public record WebhookScores(
    decimal Authority,
    decimal Quality,
    decimal Diversity,
    decimal? Competition,
    decimal Trend);

#endregion

#region Report

// هر متن { ar, en } است؛ ajebhom بر اساس زبان سایت یکی را نشان می‌دهد
public record WebhookReport(
    WebhookText Summary,
    List<WebhookFinding> Findings,
    List<WebhookAction> Actions);

public record WebhookText(string Ar, string En);

public record WebhookFinding(WebhookText Title, WebhookText Description, InsightType Type);

public record WebhookAction(WebhookText Title, WebhookText Description, ActionPriority Priority);

public record WebhookInsight(InsightCode Code, InsightType Type, InsightSeverity Severity, decimal? Value);

#endregion

#region Competition

public record WebhookCompetition(
    int? MarketPosition,
    int CompetitorsCount,
    long? MedianReferringDomains,
    long? ReferringDomainsGapToMedian,
    string? ReachableCompetitorDomain,
    string? ReachableCompetitorDisplayDomain,
    List<WebhookCompetitorRow> Rows);

// ردیف کاربر هم در جدول است با IsUser؛ Source برای کاربر null است
// رقیبی که آمارش گرفته نشد ستون‌های عددی null دارد
public record WebhookCompetitorRow(
    string Domain,
    string DisplayDomain,
    bool IsUser,
    CompetitorSource? Source,
    int? Rank,
    long? Backlinks,
    long? ReferringDomains,
    long? DofollowReferringDomains,
    long? BrokenBacklinks,
    DateTimeOffset? FirstSeen,
    decimal? ContentLinksPct);

#endregion

#region Opportunity

public record WebhookOpportunity(
    string Domain,
    string DisplayDomain,
    OpportunityCategory? Category,
    decimal Score,
    int Rank,
    int SpamScore,
    int CompetitorCount,
    string? PageUrl,
    string? PageAnchor,
    string? PageCompetitorDomain);

#endregion

#region Risk

public record WebhookRisks(
    int RiskyDomainsCount,
    decimal RiskyDomainsPct,
    decimal RiskyAnchorDomainsPct,
    DateTimeOffset? SpikeMonth,
    long? SpikeNewBacklinks,
    List<WebhookRiskyDomain> Domains,
    List<WebhookRiskyAnchor> Anchors);

public record WebhookRiskyDomain(string Domain, string DisplayDomain, int SpamScore, int Rank, long Backlinks);

public record WebhookRiskyAnchor(string? Anchor, int SpamScore, long ReferringDomains, long Backlinks);

#endregion

#region Lost And Broken

public record WebhookLostDomain(
    string Domain,
    string DisplayDomain,
    int Rank,
    string UrlFrom,
    string? Anchor,
    DateTimeOffset? LastSeen);

// ReferringDomains یعنی چند دامنه هنوز به این صفحه‌ی خراب لینک می‌دهند
public record WebhookBrokenPage(string Url, int? StatusCode, int ReferringDomains);

#endregion

#region Anchor

public record WebhookAnchors(
    decimal BrandPct,
    decimal UrlPct,
    decimal GenericPct,
    decimal NoTextPct,
    decimal OtherPct,
    List<WebhookAnchor> Top);

public record WebhookAnchor(
    string? Anchor,
    ComputeMetrics.AnchorCategory Category,
    long ReferringDomains,
    long Backlinks,
    decimal DofollowDomainsPct,
    int SpamScore);

#endregion

#region Profile

public record WebhookProfile(
    int StrongDomainsCount,
    int MediumDomainsCount,
    int WeakDomainsCount,
    decimal DofollowDomainsPct,
    decimal? ContentLinksPct,
    int KnownLocationDomainsCount,
    decimal Top5DomainsBacklinkPct,
    List<WebhookLocation> Locations,
    List<WebhookTopDomain> TopDomains);

// تعداد دامنه در هر محل لینک، مثل article یا footer
public record WebhookLocation(string Location, int Domains);

public record WebhookTopDomain(
    string Domain,
    string DisplayDomain,
    int Rank,
    long Backlinks,
    decimal BacklinksSharePct,
    int SpamScore);

#endregion

#region Trend

public record WebhookTrend(
    long? ReferringDomains12MonthsAgo,
    decimal? NetReferringDomains12MonthsPct,
    List<WebhookTrendMonth> Months);

// IsPartial یعنی ماه جاری که هنوز تمام نشده
public record WebhookTrendMonth(
    DateTimeOffset Date,
    bool IsPartial,
    int Rank,
    long Backlinks,
    long ReferringDomains,
    long NewBacklinks,
    long LostBacklinks,
    long NewReferringDomains,
    long LostReferringDomains);

#endregion

#region Backlink

public record WebhookBacklink(
    string DomainFrom,
    string DisplayDomainFrom,
    string UrlFrom,
    string UrlTo,
    string? Anchor,
    bool DoFollow,
    int Rank,
    int DomainFromRank,
    int SpamScore,
    string? SemanticLocation,
    string? ItemType,
    bool IsBroken,
    DateTimeOffset? FirstSeen,
    DateTimeOffset? LastSeen);

#endregion
