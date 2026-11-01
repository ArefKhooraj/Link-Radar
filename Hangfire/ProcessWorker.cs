using LinkRadar.Api.Dtos.ProcessJob;

namespace LinkRadar.Api.Hangfire;

public class ProcessWorker(ProcessEngine processEngine)
{
    // retry با AutomaticRetry پیش‌فرض Hangfire (۱۰ بار)؛ بعد از آن از داشبورد Requeue می‌شود
    public async Task Run(ProcessJobDto processJobDto, CancellationToken cancellationToken)
    {
        await processEngine.Execute(processJobDto, cancellationToken);
    }
}
