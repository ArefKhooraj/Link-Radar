using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

public class ProcessState : BaseEntity
{
    public int AnalysisId { get; set; }

    public CurrentStep CurrentStep { get; set; }

    public ProcessStatus Status { get; set; }

    // تعداد تلاش‌های ناموفق step فعلی؛ با رفتن به step بعدی صفر می‌شود
    public int RetryCount { get; set; }

    public string? LastError { get; set; }

    // برای پیدا کردن پردازش‌های گیرکرده یا منتظر قدیمی
    public DateTimeOffset StepStartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    // جمع هزینه‌ی واقعی تماس‌های این پردازش به دلار
    public decimal TotalCost { get; set; }

    // جلوی ذخیره‌ی همزمان دو job روی یک پردازش را می‌گیرد
    public byte[] RowVersion { get; set; } = null!;

    #region Relation

    public Analysis Analysis { get; set; } = null!;

    public ICollection<ProcessStepLog> StepLogs { get; set; } = [];

    #endregion
}

public enum ProcessStatus
{
    Running,
    Waiting,
    Completed,
    Failed
}

public enum CurrentStep
{
    CheckDomainSummary,
    DetectCompetitors,
    GetBacklinks,
    GetAnchors,
    GetReferringDomains,
    GetTimeSeries,
    GetLinkGap,
    GetCompetitorSummaries,
    ComputeMetrics,
    GenerateAiAnalysis,
    SendToUserWebhook
}
