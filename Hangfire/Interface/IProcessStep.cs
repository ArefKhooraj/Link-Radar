using LinkRadar.Api.Entities;
using LinkRadar.Api.ResultPattern;

namespace LinkRadar.Api.Hangfire.Interface;

// step در موفقیت CurrentStep بعدی (و در صورت نیاز Status) را ست و همراه داده‌ی خودش ذخیره می‌کند.
// در خطا فقط OperationResult ناموفق برمی‌گرداند؛ ثبت خطا با ProcessEngine و retry با Hangfire است.
// هزینه‌ی هر تماس را درست بعد از آن، قبل از چک موفقیت، به TotalCost اضافه می‌کند تا ProcessEngine در ProcessStepLog ثبتش کند
public interface IProcessStep
{
    bool CanHandle(CurrentStep currentStep);

    Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken);
}
