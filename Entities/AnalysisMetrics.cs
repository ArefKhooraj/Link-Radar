using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

// یک ردیف برای هر پایش؛ همه از جدول‌های خودمان حساب می‌شوند و می‌شود دوباره حساب کرد
public class AnalysisMetrics : BaseEntity
{
    public int AnalysisId { get; set; }

    public decimal HealthScore { get; set; }

    public HealthStatus HealthStatus { get; set; }

    // با هر تغییر فرمول بالا می‌رود تا معلوم باشد هر ردیف با کدام نسخه حساب شده
    public int MetricsVersion { get; set; }

    // به‌صورت JSON ذخیره می‌شود تا تغییر معیارها migration نخواهد
    public MetricsDetail Detail { get; set; } = new();

    public List<InsightItem> Insights { get; set; } = [];

    #region Relation

    public Analysis Analysis { get; set; } = null!;

    #endregion
}

public class MetricsDetail
{
    #region Diversity

    // سهم ۵ دامنه‌ی برتر از کل بک‌لینک‌ها
    public decimal Top5DomainsBacklinkPct { get; set; }

    // درصد انکرها بر اساس تعداد دامنه، نه تعداد لینک
    public decimal AnchorBrandPct { get; set; }

    public decimal AnchorUrlPct { get; set; }

    public decimal AnchorGenericPct { get; set; }

    public decimal AnchorNoTextPct { get; set; }

    public decimal AnchorOtherPct { get; set; }

    // انکرهایی که امتیاز اسپمشان بالای آستانه است
    public decimal RiskyAnchorDomainsPct { get; set; }

    #endregion

    #region Quality

    public decimal DofollowDomainsPct { get; set; }

    public int RiskyDomainsCount { get; set; }

    public decimal RiskyDomainsPct { get; set; }

    // Rank ۳۰ به بالا / ۱۰ تا ۲۹ / زیر ۱۰
    public int StrongDomainsCount { get; set; }

    public int MediumDomainsCount { get; set; }

    public int WeakDomainsCount { get; set; }

    // null یعنی هیچ دامنه‌ای محل لینک مشخص ندارد
    public decimal? ContentLinksPct { get; set; }

    // پایه‌ی ContentLinksPct؛ کنار درصد نمایش داده می‌شود
    public int KnownLocationDomainsCount { get; set; }

    #endregion

    #region Trend

    // null یعنی داده‌ی ۱۲ ماه پیش وجود ندارد
    public long? ReferringDomains12MonthsAgo { get; set; }

    public decimal? NetReferringDomains12MonthsPct { get; set; }

    // null یعنی جهش غیرعادی پیدا نشد
    public DateTimeOffset? SpikeMonth { get; set; }

    public long? SpikeNewBacklinks { get; set; }

    #endregion

    #region Lost And Broken

    // فقط دامنه‌هایی که دیگر هیچ لینک زنده‌ای ندارند
    public int LostDomainsCount { get; set; }

    // صفحاتی از سایت کاربر که خرابند و هنوز بک‌لینک می‌گیرند
    public int BrokenPagesCount { get; set; }

    #endregion

    #region Competition

    // جایگاه کاربر بین خودش و رقبا بر اساس تعداد دامنه؛ ۱ یعنی اول
    public int? MarketPosition { get; set; }

    public int CompetitorsCount { get; set; }

    // میانه، تا یک رقیب غول نتیجه را خراب نکند
    public long? CompetitorsMedianReferringDomains { get; set; }

    public long? ReferringDomainsGapToMedian { get; set; }

    // نزدیک‌ترین رقیبی که از کاربر جلوتر است
    public string? ReachableCompetitorDomain { get; set; }

    #endregion

    #region Opportunity

    public int OpportunityCount { get; set; }

    public int MultiCompetitorOpportunityCount { get; set; }

    #endregion

    #region Score

    // هر کدام ۰ تا ۱۰۰
    public decimal AuthorityScore { get; set; }

    public decimal QualityScore { get; set; }

    public decimal DiversityScore { get; set; }

    // null یعنی رقیبی با آمار نداریم
    public decimal? CompetitionScore { get; set; }

    public decimal TrendScore { get; set; }

    #endregion
}

// بینش قانون‌محور؛ متن را ajebhom یا AI از روی Code و Value می‌سازد
public class InsightItem
{
    public InsightCode Code { get; set; }

    public InsightType Type { get; set; }

    public InsightSeverity Severity { get; set; }

    // عددی که این بینش را ساخته، مثل ۷۳ برای تمرکز لینک
    public decimal? Value { get; set; }
}

public enum HealthStatus
{
    Weak,
    Medium,
    Good
}

public enum InsightType
{
    Positive,
    Warning,
    Problem
}

public enum InsightSeverity
{
    Low,
    Medium,
    High
}

public enum InsightCode
{
    HighAuthority,
    LowAuthority,
    HighLinkConcentration,
    HighRiskyDomains,
    RiskyAnchors,
    LowDofollow,
    GrowingReferringDomains,
    DecliningReferringDomains,
    UnnaturalSpike,
    LostDomains,
    BrokenPages,
    FarBehindCompetitors,
    AheadOfCompetitors
}
