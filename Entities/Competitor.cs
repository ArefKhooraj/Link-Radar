using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

public class Competitor : BaseEntity
{
    public int AnalysisId { get; set; }

    public string Domain { get; set; } = null!;

    public CompetitorSource Source { get; set; }

    public DateTimeOffset? LinkGapFetchedAt { get; set; }

    // null یعنی آمار این رقیب هنوز گرفته نشده؛ ستون‌های آمار در خطای دائمی null می‌مانند
    public DateTimeOffset? SummaryFetchedAt { get; set; }

    public int? Rank { get; set; }

    public long? Backlinks { get; set; }

    public long? ReferringDomains { get; set; }

    public long? ReferringDomainsNofollow { get; set; }

    public long? ReferringMainDomains { get; set; }

    public long? BrokenBacklinks { get; set; }

    public DateTimeOffset? FirstSeen { get; set; }

    // درصد از لینک‌هایی که محلشان مشخص است
    public decimal? ContentLinksPct { get; set; }

    public decimal? FooterLinksPct { get; set; }

    #region Relation

    public Analysis Analysis { get; set; } = null!;

    public ICollection<LinkGapDomain> LinkGapDomains { get; set; } = [];

    #endregion
}

public enum CompetitorSource
{
    User,
    Detected
}