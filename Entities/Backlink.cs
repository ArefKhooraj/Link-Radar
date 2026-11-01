using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

public class Backlink : BaseEntity
{
    public int AnalysisId { get; set; }

    public string DomainFrom { get; set; } = null!;

    public string UrlFrom { get; set; } = null!;

    public string UrlTo { get; set; } = null!;

    public string? Anchor { get; set; }

    public string? ItemType { get; set; }

    public bool DoFollow { get; set; }

    // مثل nofollow,ugc؛ در پاسخ DataForSEO آرایه است
    public string? Attributes { get; set; }

    public int Rank { get; set; }

    public int PageFromRank { get; set; }

    public int DomainFromRank { get; set; }

    public int BacklinkSpamScore { get; set; }

    public string? SemanticLocation { get; set; }

    public string? PageFromLanguage { get; set; }

    public string? TldFrom { get; set; }

    public bool IsBroken { get; set; }

    public int? UrlToStatusCode { get; set; }

    public DateTimeOffset? FirstSeen { get; set; }

    public DateTimeOffset? LastSeen { get; set; }

    #region Relation

    public Analysis Analysis { get; set; } = null!;

    #endregion
}