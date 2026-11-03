using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

// متن نهایی گزارش که AI از روی معیارها نوشته؛ عددها از AnalysisMetrics می‌آیند، نه از AI
// هر متن به عربی و انگلیسی است تا کاربر ajebhom بدون تماس دوباره زبان را عوض کند
public class AnalysisReport : BaseEntity
{
    public int AnalysisId { get; set; }

    public LocalizedText Summary { get; set; } = null!;

    public List<ReportFinding> Findings { get; set; } = [];

    public List<ReportAction> Actions { get; set; } = [];

    public string Model { get; set; } = null!;

    #region Relation

    public Analysis Analysis { get; set; } = null!;

    #endregion
}

// هر دو زبان در یک تماس AI نوشته می‌شوند تا یافته‌ها و ترتیبشان در دو زبان یکی باشد
public class LocalizedText
{
    public string Ar { get; set; } = null!;

    public string En { get; set; } = null!;
}

public class ReportFinding
{
    public LocalizedText Title { get; set; } = null!;

    public LocalizedText Description { get; set; } = null!;

    public InsightType Type { get; set; }
}

public class ReportAction
{
    public LocalizedText Title { get; set; } = null!;

    public LocalizedText Description { get; set; } = null!;

    public ActionPriority Priority { get; set; }
}

public enum ActionPriority
{
    High,
    Medium,
    Low
}
