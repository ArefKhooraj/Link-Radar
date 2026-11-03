using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

// دامنه‌ای که به رقبا لینک داده ولی به کاربر نه؛ بعد از حذف رقبا و دامنه‌های پرریسک
public class LinkOpportunity : BaseEntity
{
    public int AnalysisId { get; set; }

    public string Domain { get; set; } = null!;

    // به چند رقیب لینک داده است
    public int CompetitorCount { get; set; }

    public int Rank { get; set; }

    public int BacklinksSpamScore { get; set; }

    public decimal Score { get; set; }

    // صفحه‌ای از این دامنه که به رقیب لینک داده؛ GetOpportunityPages پرش می‌کند
    public string? PageUrl { get; set; }

    public string? PageAnchor { get; set; }

    public string? PageCompetitorDomain { get; set; }

    // نوع سایت، مثل خبری یا وبلاگ؛ ClassifyOpportunities پرش می‌کند
    public OpportunityCategory? Category { get; set; }

    #region Relation

    public Analysis Analysis { get; set; } = null!;

    #endregion
}

public enum OpportunityCategory
{
    News,
    Blog,
    Directory,
    Forum,
    Marketplace,
    Business,
    Agency,
    Education,
    Government,
    Other
}
