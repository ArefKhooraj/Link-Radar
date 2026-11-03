using LinkRadar.Api.Context;
using LinkRadar.Api.DataForSeoServices;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.ResultPattern;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

// سایت‌هایی که لینک را برداشته‌اند؛ کاربر می‌تواند پیگیری کند و لینک را برگرداند
public class GetLostBacklinks(
    DataForSeoService dataForSeoService,
    ILogger<GetLostBacklinks> logger,
    LinkRadarDbContext context) : IProcessStep
{
    private const int MaxLostBacklinks = 50;

    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.GetLostBacklinks;

    public async Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken)
    {
        var target = await context.Analyses.Where(s => s.Id == processState.AnalysisId)
            .Select(s => s.Target)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(target))
        {
            logger.LogError("Can Not Find Target For This ProcessStateId = {ProcessStateId}, AnalysisId = {AnalysisId}",
                processState.Id, processState.AnalysisId);

            return OperationResult.FailureResult(Error.NotFound(ErrorCodes.Analysis.InvalidTarget,
                "Can Not Find Target For This Analysis Id"));
        }

        logger.LogInformation("Start Get Lost Backlinks For This ProcessStateId = {ProcessStateId}, Target = {Target}",
            processState.Id, target);

        var dataForSeoResult = await dataForSeoService.PostAsync<BacklinksResult>(
            "backlinks/backlinks/live",
            new BacklinksRequest(target, BacklinksStatusType: "lost", Limit: MaxLostBacklinks),
            cancellationToken);

        // تماس خطادار هم ممکن است هزینه داشته باشد
        processState.TotalCost += dataForSeoResult.Result?.Cost ?? 0;

        if (!dataForSeoResult.IsSuccess)
        {
            logger.LogWarning(
                "Get Lost Backlinks Failed For This ProcessStateId = {ProcessStateId}, Target = {Target}, Error = {Error}",
                processState.Id, target, dataForSeoResult.GetErrorMessage());

            return OperationResult.FailureResult(dataForSeoResult.Errors);
        }

        var callResult = dataForSeoResult.Result!;
        var items = callResult.Result?.Items ?? [];

        var lostBacklinks = GetBacklinks.MapBacklinks(items, processState.AnalysisId, isLost: true);

        if (lostBacklinks.Count < items.Count)
            logger.LogWarning(
                "Skip Incomplete Lost Backlinks For This ProcessStateId = {ProcessStateId}, SkippedCount = {SkippedCount}",
                processState.Id, items.Count - lostBacklinks.Count);

        context.Backlinks.AddRange(lostBacklinks);

        processState.CurrentStep = CurrentStep.GetAnchors;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Get Lost Backlinks For This ProcessStateId = {ProcessStateId}, SavedCount = {SavedCount}, TotalCount = {TotalCount}, Cost = {Cost}, Next Step = {NextStep}",
            processState.Id, lostBacklinks.Count, callResult.Result?.TotalCount, callResult.Cost, processState.CurrentStep);

        return OperationResult.SuccessResult();
    }
}
