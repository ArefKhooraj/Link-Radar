using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

public class BacklinkSummary : BaseEntity
{
    public int AnalysisId { get; set; }

    public int Rank { get; set; }

    public long Backlinks { get; set; }

    public long ReferringDomains { get; set; }

    public long ReferringDomainsNofollow { get; set; }

    public long ReferringMainDomains { get; set; }

    public long ReferringPages { get; set; }

    public long ReferringPagesNofollow { get; set; }

    public int BacklinksSpamScore { get; set; }

    public int TargetSpamScore { get; set; }

    public long BrokenBacklinks { get; set; }

    public DateTimeOffset? FirstSeen { get; set; }

    #region Relation

    public Analysis Analysis { get; set; } = null!;

    #endregion
}