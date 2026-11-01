using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

public class BacklinkAnchor : BaseEntity
{
    public int AnalysisId { get; set; }

    // null یعنی لینک بدون متن، معمولاً تصویری
    public string? Anchor { get; set; }

    public long Backlinks { get; set; }

    public long ReferringDomains { get; set; }

    public long ReferringDomainsNofollow { get; set; }

    public int Rank { get; set; }

    public int BacklinksSpamScore { get; set; }

    public DateTimeOffset? FirstSeen { get; set; }

    #region Relation

    public Analysis Analysis { get; set; } = null!;

    #endregion
}