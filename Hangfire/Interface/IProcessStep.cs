using LinkRadar.Api.Entities;
using LinkRadar.Api.ResultPattern;

namespace LinkRadar.Api.Hangfire.Interface;

// step در موفقیت CurrentStep بعدی (و در صورت نیاز Status) را ست و همراه داده‌ی خودش ذخیره می‌کند.
// در خطا فقط OperationResult ناموفق برمی‌گرداند؛ retry و Failed را ProcessEngine انجام می‌دهد
public interface IProcessStep
{
    bool CanHandle(CurrentStep currentStep);

    Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken);
}
