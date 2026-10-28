using LinkRadar.Api.Context;
using LinkRadar.Api.Dtos.ProcessJob;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.ResultPattern;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Hangfire;

public class ProcessEngine(
    IEnumerable<IProcessStep> steps,
    LinkRadarDbContext context,
    JobScheduler jobScheduler,
    ILogger<ProcessEngine> logger)
{
    private const int MaxStepAttempts = 3;

    public async Task Execute(ProcessJobDto processJobDto, CancellationToken cancellationToken)
    {
        var processState = await context.ProcessStates
            .FirstOrDefaultAsync(s => s.Id == processJobDto.ProcessStateId, cancellationToken);

        if (processState is null)
        {
            logger.LogError("Can Not Find ProcessState With This Id = {ProcessStateId}",
                processJobDto.ProcessStateId);
            return;
        }

        // Waiting با webhook ادامه پیدا می‌کند؛ Completed و Failed تمام شده‌اند
        if (processState.Status != ProcessStatus.Running)
        {
            logger.LogInformation(
                "Skip ProcessState With This Id = {ProcessStateId} Because Status = {Status}",
                processState.Id, processState.Status);
            return;
        }

        var currentStep = processState.CurrentStep;
        var attempt = processState.RetryCount + 1;

        var stepLog = new ProcessStepLog
        {
            ProcessStateId = processState.Id,
            Step = currentStep,
            Attempt = attempt,
            StartedAt = DateTimeOffset.UtcNow,
            CreateDate = DateTimeOffset.UtcNow
        };

        logger.LogInformation(
            "Start Step {Step} For This ProcessStateId = {ProcessStateId}, Attempt = {Attempt}",
            currentStep, processState.Id, attempt);

        var step = steps.FirstOrDefault(s => s.CanHandle(currentStep));

        OperationResult result;

        if (step is null)
        {
            result = OperationResult.FailureResult(Error.Unexpected(ErrorCodes.Process.StepNotFound,
                $"No step is registered for {currentStep}"));
        }
        else
        {
            try
            {
                result = await step.Execute(processState, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (DbUpdateConcurrencyException)
            {
                // job دیگری همین پردازش را جلو برده
                logger.LogWarning(
                    "Concurrency Conflict In Step {Step} For This ProcessStateId = {ProcessStateId}, Skip",
                    currentStep, processState.Id);
                return;
            }
            catch (Exception e)
            {
                // خطای پیش‌بینی‌نشده‌ی کد؛ مثل خطای گذرا تا سقف تلاش‌ها retry می‌شود
                logger.LogError(e,
                    "Get Exception In Step {Step} For This ProcessStateId = {ProcessStateId}, With This Message {Message}",
                    currentStep, processState.Id, e.Message);

                result = OperationResult.FailureResult(new Error(ErrorCodes.Process.StepException, e.Message,
                    ErrorType.Unexpected, IsTransient: true));
            }
        }

        stepLog.FinishedAt = DateTimeOffset.UtcNow;
        stepLog.IsSuccess = result.IsSuccess;
        stepLog.ErrorCode = result.Errors.FirstOrDefault()?.Code;
        stepLog.ErrorMessage = result.IsSuccess ? null : Truncate(result.GetErrorMessage());

        if (result.IsSuccess)
            await HandleSuccess(processState, stepLog, processJobDto, cancellationToken);
        else
            await HandleFailure(processState.Id, stepLog, result, processJobDto, cancellationToken);
    }

    private async Task HandleSuccess(ProcessState processState, ProcessStepLog stepLog,
        ProcessJobDto processJobDto, CancellationToken cancellationToken)
    {
        // step خودش CurrentStep بعدی یا Status جدید را ست و ذخیره کرده است
        processState.RetryCount = 0;
        processState.LastError = null;
        processState.StepStartedAt = DateTimeOffset.UtcNow;
        processState.LastChangeDate = DateTimeOffset.UtcNow;

        if (processState.Status == ProcessStatus.Completed)
            processState.CompletedAt = DateTimeOffset.UtcNow;

        context.ProcessStepLogs.Add(stepLog);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Step {Step} For This ProcessStateId = {ProcessStateId}, Next Step = {NextStep}, Status = {Status}",
            stepLog.Step, processState.Id, processState.CurrentStep, processState.Status);

        if (processState.Status == ProcessStatus.Running)
            jobScheduler.Enqueue(processJobDto);
    }

    private async Task HandleFailure(int processStateId, ProcessStepLog stepLog, OperationResult result,
        ProcessJobDto processJobDto, CancellationToken cancellationToken)
    {
        // تغییرات نیمه‌کاره‌ی step (در حافظه) نباید همراه وضعیت خطا ذخیره شوند
        context.ChangeTracker.Clear();

        var processState = await context.ProcessStates.FirstAsync(s => s.Id == processStateId, cancellationToken);

        processState.RetryCount = stepLog.Attempt;
        processState.LastError = Truncate(result.GetErrorMessage());
        processState.LastChangeDate = DateTimeOffset.UtcNow;

        context.ProcessStepLogs.Add(stepLog);

        if (result.IsTransient && stepLog.Attempt < MaxStepAttempts)
        {
            await context.SaveChangesAsync(cancellationToken);

            var delay = TimeSpan.FromSeconds(30 * stepLog.Attempt);

            logger.LogWarning(
                "Step {Step} Failed For This ProcessStateId = {ProcessStateId}, Retry In {Delay}, Error = {Error}",
                stepLog.Step, processStateId, delay, processState.LastError);

            jobScheduler.Schedule(processJobDto, delay);
            return;
        }

        processState.Status = ProcessStatus.Failed;
        processState.CompletedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        // webhook خطا به ajebhom بعد از ساخت سرویس Webhook از همین‌جا فرستاده می‌شود
        logger.LogError(
            "Process Failed At Step {Step} For This ProcessStateId = {ProcessStateId}, Attempt = {Attempt}, Error = {Error}",
            stepLog.Step, processStateId, stepLog.Attempt, processState.LastError);
    }

    private static string Truncate(string value) => value.Length <= 2000 ? value : value[..2000];
}
