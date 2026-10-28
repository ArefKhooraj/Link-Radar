using global::Hangfire;
using LinkRadar.Api.Dtos.ProcessJob;

namespace LinkRadar.Api.Hangfire;

public class JobScheduler(IBackgroundJobClient backgroundJobClient, ILogger<JobScheduler> logger)
{
    public string Enqueue(ProcessJobDto processJobDto)
    {
        var jobId = backgroundJobClient.Enqueue<ProcessWorker>(s => s.Run(processJobDto, CancellationToken.None));

        logger.LogInformation("Job Enqueued For This ProcessStateId = {ProcessStateId}, JobId = {JobId}",
            processJobDto.ProcessStateId, jobId);

        return jobId;
    }

    public string Schedule(ProcessJobDto processJobDto, TimeSpan delay)
    {
        var jobId = backgroundJobClient.Schedule<ProcessWorker>(
            s => s.Run(processJobDto, CancellationToken.None), delay);

        logger.LogInformation(
            "Job Scheduled For This ProcessStateId = {ProcessStateId}, JobId = {JobId}, Delay = {Delay}",
            processJobDto.ProcessStateId, jobId, delay);

        return jobId;
    }
}
