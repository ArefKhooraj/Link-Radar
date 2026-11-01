using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

public class ReferringDomain : BaseEntity
{
    public int AnalysisId { get; set; }

    public string Domain { get; set; } = null!;

    public int Rank { get; set; }

    public long Backlinks { get; set; }

    public int BacklinksSpamScore { get; set; }

    public long BrokenBacklinks { get; set; }

    public long ReferringPages { get; set; }

    public long ReferringPagesNofollow { get; set; }

    public DateTimeOffset? FirstSeen { get; set; }

    #region Relation

    public Analysis Analysis { get; set; } = null!;

    #endregion
}