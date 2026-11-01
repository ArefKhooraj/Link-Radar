using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

public class BacklinkTimeSeries : BaseEntity
{
    public int AnalysisId { get; set; }

    // آخرین روز ماه؛ ماه جاری هنوز کامل نشده
    public DateTimeOffset Date { get; set; }

    public int Rank { get; set; }

    public long Backlinks { get; set; }

    public long ReferringDomains { get; set; }

    public long ReferringDomainsNofollow { get; set; }

    public long ReferringMainDomains { get; set; }

    public long NewBacklinks { get; set; }

    public long LostBacklinks { get; set; }

    public long NewReferringDomains { get; set; }

    public long LostReferringDomains { get; set; }

    #region Relation

    public Analysis Analysis { get; set; } = null!;

    #endregion
}