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
        var costBefore = processState.TotalCost;

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

        if (step is null)
        {
            var notFound = OperationResult.FailureResult(Error.Unexpected(ErrorCodes.Process.StepNotFound,
                $"No step is registered for {currentStep}"));

            await SaveFailure(processState, costBefore, stepLog, notFound, cancellationToken);
            throw new ProcessStepException(currentStep, notFound.GetErrorMessage());
        }

        OperationResult result;

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
            // پیام EF کلی است و علت واقعی در inner exception است
            var rootMessage = e.GetBaseException().Message;

            logger.LogError(e,
                "Get Exception In Step {Step} For This ProcessStateId = {ProcessStateId}, With This Message {Message}",
                currentStep, processState.Id, rootMessage);

            await SaveFailure(processState, costBefore, stepLog,
                OperationResult.FailureResult(Error.Unexpected(ErrorCodes.Process.StepException, rootMessage)),
                cancellationToken);

            // exception اصلی با stack trace به داشبورد Hangfire می‌رسد
            throw;
        }

        if (!result.IsSuccess)
        {
            await SaveFailure(processState, costBefore, stepLog, result, cancellationToken);
            throw new ProcessStepException(currentStep, result.GetErrorMessage());
        }

        stepLog.Cost = processState.TotalCost - costBefore;

        await HandleSuccess(processState, stepLog, processJobDto, cancellationToken);
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

        stepLog.FinishedAt = DateTimeOffset.UtcNow;
        stepLog.IsSuccess = true;
        context.ProcessStepLogs.Add(stepLog);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Step {Step} For This ProcessStateId = {ProcessStateId}, Next Step = {NextStep}, Status = {Status}",
            stepLog.Step, processState.Id, processState.CurrentStep, processState.Status);

        if (processState.Status == ProcessStatus.Running)
            jobScheduler.Enqueue(processJobDto);
    }

    // فقط خطا را ثبت می‌کند؛ retry با Hangfire است و Status همان Running می‌ماند تا Requeue کار کند
    private async Task SaveFailure(ProcessState failedState, decimal costBefore, ProcessStepLog stepLog,
        OperationResult result, CancellationToken cancellationToken)
    {
        var processStateId = failedState.Id;

        // پول تماس‌های موفق این اجرا پرداخت شده و با Clear نباید گم شود
        var totalCost = failedState.TotalCost;
        stepLog.Cost = totalCost - costBefore;

        // تغییرات نیمه‌کاره‌ی step (در حافظه) نباید همراه وضعیت خطا ذخیره شوند
        context.ChangeTracker.Clear();

        var processState = await context.ProcessStates.FirstAsync(s => s.Id == processStateId, cancellationToken);

        processState.TotalCost = totalCost;
        processState.RetryCount = stepLog.Attempt;
        processState.LastError = Truncate(result.GetErrorMessage());
        processState.LastChangeDate = DateTimeOffset.UtcNow;

        stepLog.FinishedAt = DateTimeOffset.UtcNow;
        stepLog.IsSuccess = false;
        stepLog.ErrorCode = result.Errors.FirstOrDefault()?.Code;
        stepLog.ErrorMessage = processState.LastError;
        context.ProcessStepLogs.Add(stepLog);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Step {Step} Failed For This ProcessStateId = {ProcessStateId}, Attempt = {Attempt}, Error = {Error}",
            stepLog.Step, processStateId, stepLog.Attempt, processState.LastError);
    }

    private static string Truncate(string value) => value.Length <= 2000 ? value : value[..2000];
}
