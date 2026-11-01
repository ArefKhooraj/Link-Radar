using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

public class LinkGapDomain : BaseEntity
{
    public int CompetitorId { get; set; }

    public string Domain { get; set; } = null!;

    public int Rank { get; set; }

    public long Backlinks { get; set; }

    public int BacklinksSpamScore { get; set; }

    public DateTimeOffset? FirstSeen { get; set; }

    #region Relation

    public Competitor Competitor { get; set; } = null!;

    #endregion
}