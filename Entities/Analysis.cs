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

    // املاهای دیگر برند که در انکرهای واقعی دیده شده‌اند؛ DetectBrandAliases پرش می‌کند
    public List<string> BrandAliases { get; set; } = [];

    #region Relation

    public User User { get; set; } = null!;

    public ProcessState ProcessState { get; set; } = null!;

    public BacklinkSummary BacklinkSummary { get; set; }

    public ICollection<Competitor> Competitors { get; set; }
    public ICollection<Backlink> Backlinks { get; set; } = [];
    
    public ICollection<BacklinkAnchor> BacklinkAnchors { get; set; } = [];
    
    public ICollection<ReferringDomain> ReferringDomains { get; set; } = [];
    
    public ICollection<BacklinkTimeSeries> BacklinkTimeSeries { get; set; } = [];

    public AnalysisMetrics? AnalysisMetrics { get; set; }

    public AnalysisReport? AnalysisReport { get; set; }

    public ICollection<LinkOpportunity> LinkOpportunities { get; set; } = [];

    #endregion
}

public enum TargetType
{
    Domain,
    Page
}