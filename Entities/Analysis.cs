using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

public class Analysis : BaseEntity
{
    public int UserId { get; set; }


    public int EntityId { get; set; }


    public string Target { get; set; } = null!;

    public TargetType TargetType { get; set; }

    public string? Keyword { get; set; }

    public string CountryCode { get; set; } = null!;

    public string? BrandName { get; set; }

    #region Relation

    public User User { get; set; } = null!;

    public ProcessState ProcessState { get; set; } = null!;

    public BacklinkSummary BacklinkSummary { get; set; }

    public ICollection<Competitor> Competitors { get; set; }
    public ICollection<Backlink> Backlinks { get; set; } = [];
    
    public ICollection<BacklinkAnchor> BacklinkAnchors { get; set; } = [];
    
    public ICollection<ReferringDomain> ReferringDomains { get; set; } = [];

    #endregion
}

public enum TargetType
{
    Domain,
    Page
}