using LinkRadar.Api.Context;
using LinkRadar.Api.DataForSeoServices;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.ResultPattern;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

public class GetTimeSeries(
    LinkRadarDbContext context,
    ILogger<GetTimeSeries> logger,
    DataForSeoService dataForSeoService) : IProcessStep
{
    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.GetTimeSeries;

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
                "Can Not Find This Target"));
        }

        // این دو endpoint فقط دامنه می‌گیرند؛ برای حالت صفحه داده‌ی کل دامنه می‌آید
        var domain = GetDomain(target);

        logger.LogInformation("Start Get Time Series For This ProcessStateId = {ProcessStateId}, Domain = {Domain}",
            processState.Id, domain);

        var dateTo = DateTime.UtcNow;
        var dateFrom = dateTo.AddMonths(-24);

        var summaryResult = await dataForSeoService.PostAsync<TimeSeriesSummaryResult>(
            "backlinks/timeseries_summary/live",
            new TimeSeriesSummaryRequest(domain, dateFrom.ToString("yyyy-MM-dd"), dateTo.ToString("yyyy-MM-dd")),
            cancellationToken);

        // تماس خطادار هم ممکن است هزینه داشته باشد
        processState.TotalCost += summaryResult.Result?.Cost ?? 0;

        if (!summaryResult.IsSuccess)
        {
            logger.LogWarning(
                "Get Time Series Summary Failed For This ProcessStateId = {ProcessStateId}, Domain = {Domain}, Error = {Error}",
                processState.Id, domain, summaryResult.GetErrorMessage());

            return OperationResult.FailureResult(summaryResult.Errors);
        }

        var newLostResult = await dataForSeoService.PostAsync<TimeSeriesNewLostResult>(
            "backlinks/timeseries_new_lost_summary/live",
            new TimeSeriesNewLostRequest(domain, dateFrom.ToString("yyyy-MM-dd"), dateTo.ToString("yyyy-MM-dd")),
            cancellationToken);

        processState.TotalCost += newLostResult.Result?.Cost ?? 0;

        if (!newLostResult.IsSuccess)
        {
            logger.LogWarning(
                "Get Time Series New Lost Failed For This ProcessStateId = {ProcessStateId}, Domain = {Domain}, Error = {Error}",
                processState.Id, domain, newLostResult.GetErrorMessage());

            return OperationResult.FailureResult(newLostResult.Errors);
        }

        var summaryItems = summaryResult.Result!.Result?.Items ?? [];
        var newLostItems = newLostResult.Result!.Result?.Items ?? [];

        var newLostByDate = newLostItems
            .Where(s => s.Date is not null)
            .GroupBy(s => s.Date!.Value)
            .ToDictionary(s => s.Key, s => s.First());

        // Date اجباری است؛ یک ردیف ناقص نباید ذخیره‌ی بقیه را خراب کند
        var timeSeries = summaryItems
            .Where(s => s.Date is not null)
            .Select(s =>
            {
                newLostByDate.TryGetValue(s.Date!.Value, out var newLost);

                return new BacklinkTimeSeries
                {
                    AnalysisId = processState.AnalysisId,
                    Date = s.Date.Value,
                    Rank = s.Rank ?? 0,
                    Backlinks = s.Backlinks ?? 0,
                    ReferringDomains = s.ReferringDomains ?? 0,
                    ReferringDomainsNofollow = s.ReferringDomainsNofollow ?? 0,
                    ReferringMainDomains = s.ReferringMainDomains ?? 0,
                    NewBacklinks = newLost?.NewBacklinks ?? 0,
                    LostBacklinks = newLost?.LostBacklinks ?? 0,
                    NewReferringDomains = newLost?.NewReferringDomains ?? 0,
                    LostReferringDomains = newLost?.LostReferringDomains ?? 0
                };
            })
            .ToList();

        var unmatchedCount = timeSeries.Count(s => !newLostByDate.ContainsKey(s.Date));

        if (unmatchedCount > 0 || timeSeries.Count < summaryItems.Count)
            logger.LogWarning(
                "Incomplete Time Series For This ProcessStateId = {ProcessStateId}, SkippedCount = {SkippedCount}, NoNewLostCount = {NoNewLostCount}",
                processState.Id, summaryItems.Count - timeSeries.Count, unmatchedCount);

        // Summary بک‌لینک داشته ولی لیست خالی برگشته؛ پردازش ادامه پیدا می‌کند
        if (timeSeries.Count == 0)
            logger.LogWarning("Time Series List Is Empty For This ProcessStateId = {ProcessStateId}, Domain = {Domain}",
                processState.Id, domain);

        context.BacklinkTimeSeries.AddRange(timeSeries);

        processState.CurrentStep = CurrentStep.GetLinkGap;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Get Time Series For This ProcessStateId = {ProcessStateId}, SavedCount = {SavedCount}, Cost = {Cost}, Next Step = {NextStep}",
            processState.Id, timeSeries.Count, summaryResult.Result!.Cost + newLostResult.Result!.Cost, processState.CurrentStep);

        return OperationResult.SuccessResult();
    }

    private static string GetDomain(string target)
    {
        var host = Uri.TryCreate(target, UriKind.Absolute, out var uri) ? uri.Host : target;

        return host.StartsWith("www.") ? host[4..] : host;
    }
}

#region Request

public record TimeSeriesSummaryRequest(
    string Target,
    string DateFrom,
    string DateTo,
    string GroupRange = "month",
    bool IncludeSubdomains = true,
    string RankScale = "one_hundred");

public record TimeSeriesNewLostRequest(
    string Target,
    string DateFrom,
    string DateTo,
    string GroupRange = "month",
    bool IncludeSubdomains = true);

#endregion

#region Response

public class TimeSeriesSummaryResult
{
    public List<TimeSeriesSummaryItem>? Items { get; set; }
}

public class TimeSeriesSummaryItem
{
    public DateTimeOffset? Date { get; set; }
    public int? Rank { get; set; }
    public long? Backlinks { get; set; }
    public long? ReferringDomains { get; set; }
    public long? ReferringDomainsNofollow { get; set; }
    public long? ReferringMainDomains { get; set; }
}

public class TimeSeriesNewLostResult
{
    public List<TimeSeriesNewLostItem>? Items { get; set; }
}

public class TimeSeriesNewLostItem
{
    public DateTimeOffset? Date { get; set; }
    public long? NewBacklinks { get; set; }
    public long? LostBacklinks { get; set; }
    public long? NewReferringDomains { get; set; }
    public long? LostReferringDomains { get; set; }
}

#endregion