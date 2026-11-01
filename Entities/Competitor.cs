using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

public class Competitor : BaseEntity
{
    public int AnalysisId { get; set; }

    public string Domain { get; set; } = null!;

    public CompetitorSource Source { get; set; }

    #region Relation

    public Analysis Analysis { get; set; } = null!;
    
    

    #endregion
}

public enum CompetitorSource
{
    User,
    Detected
}