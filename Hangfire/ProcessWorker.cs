using global::Hangfire;
using LinkRadar.Api.Dtos.ProcessJob;

namespace LinkRadar.Api.Hangfire;

public class ProcessWorker(ProcessEngine processEngine)
{
    // retry را ProcessEngine با RetryCount کنترل می‌کند، نه Hangfire
    [AutomaticRetry(Attempts = 0)]
    public async Task Run(ProcessJobDto processJobDto, CancellationToken cancellationToken)
    {
        await processEngine.Execute(processJobDto, cancellationToken);
    }
}
