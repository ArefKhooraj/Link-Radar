using LinkRadar.Api.Entities.Common;

namespace LinkRadar.Api.Entities;

// تاریخچه‌ی هر بار اجرای یک step؛ ProcessState فقط وضعیت فعلی را دارد
public class ProcessStepLog : BaseEntity
{
    public int ProcessStateId { get; set; }

    public CurrentStep Step { get; set; }

    public int Attempt { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    // هزینه‌ی واقعی همین اجرا به دلار
    public decimal Cost { get; set; }

    #region Relation

    public ProcessState ProcessState { get; set; } = null!;

    #endregion
}
