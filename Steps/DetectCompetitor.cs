using LinkRadar.Api.AIRequestModels;
using LinkRadar.Api.AiServices;
using LinkRadar.Api.Context;
using LinkRadar.Api.DataForSeoServices;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Extensions;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.Mapper;
using LinkRadar.Api.ResultPattern;
using LinkRadar.Api.Statics;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

public class DetectCompetitor(
    DataForSeoService dataForSeoService,
    AiService aiService,
    LinkRadarDbContext context,
    ILogger<DetectCompetitor> logger) : IProcessStep
{
    private const int MaxCompetitors = 5;

    // این دو عدد از دو تست almosafer و ajebhom آمده‌اند و باید با داده‌ی بیشتر تنظیم شوند
    private const long MinTargetKeywordsForDataOnly = 1000;
    private const decimal MinKeywordOverlap = 0.15m;

    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.DetectCompetitors;

    public async Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken)
    {
        var analysis = await context.Analyses.Where(s => s.Id == processState.AnalysisId)
            .Select(s => new { s.Target, s.TargetType, s.Keyword, s.CountryCode, s.BrandName })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (analysis is null)
        {
            logger.LogError("Can Not Find Analysis For This ProcessStateId = {ProcessStateId}, AnalysisId = {AnalysisId}",
                processState.Id, processState.AnalysisId);

            return OperationResult.FailureResult(Error.NotFound(ErrorCodes.Analysis.InvalidTarget, "Analysis Not Found"));
        }

        var existingDomains = await context.Set<Competitor>()
            .Where(s => s.AnalysisId == processState.AnalysisId)
            .Select(s => s.Domain)
            .ToListAsync(cancellationToken);

        var slots = MaxCompetitors - existingDomains.Count;

        logger.LogInformation(
            "Start Detect Competitors For This ProcessStateId = {ProcessStateId}, Target = {Target}, ExistingCount = {ExistingCount}",
            processState.Id, analysis.Target, existingDomains.Count);

        // کاربر همه‌ی رقبا را داده؛ پولی خرج نمی‌شود
        if (slots <= 0)
            return await SaveAndMoveNext(processState, [], cancellationToken);

        // یک برند فقط یک بار: خود سایت و رقبای کاربر هم از همین راه حذف می‌شوند
        var usedBrands = existingDomains.Select(GetBrandLabel).Append(GetBrandLabel(GetDomain(analysis.Target)))
            .ToHashSet();

        var candidates = new List<CompetitorCandidate>();
        long targetKeywords = 0;

        if (DataForSeoLocations.TryGetLocationCode(analysis.CountryCode, out var locationCode))
        {
            var dataForSeoResult = await dataForSeoService.PostAsync<CompetitorsDomainResult>(
                "dataforseo_labs/google/competitors_domain/live",
                new CompetitorsDomainRequest(analysis.Target, locationCode, DataForSeoLocations.LanguageCode),
                cancellationToken);

            // تماس خطادار هم ممکن است هزینه داشته باشد
            processState.TotalCost += dataForSeoResult.Result?.Cost ?? 0;

            if (dataForSeoResult.IsSuccess)
            {
                var items = dataForSeoResult.Result!.Result?.Items ?? [];
                var targetDomain = GetDomain(analysis.Target);

                targetKeywords = items.FirstOrDefault(s => s.Domain == targetDomain)
                    ?.FullDomainMetrics?.Organic?.Count ?? 0;

                candidates = BuildCandidates(items, usedBrands);
            }
            else if (dataForSeoResult.IsTransient)
            {
                logger.LogWarning(
                    "Competitors Domain Failed For This ProcessStateId = {ProcessStateId}, Error = {Error}",
                    processState.Id, dataForSeoResult.GetErrorMessage());

                return OperationResult.FailureResult(dataForSeoResult.Errors);
            }
            else
            {
                // خطای دائمی مثل کشور پشتیبانی‌نشده؛ AI بدون نامزد ادامه می‌دهد
                logger.LogWarning(
                    "Competitors Domain Unavailable For This ProcessStateId = {ProcessStateId}, Error = {Error}, Continue With Ai",
                    processState.Id, dataForSeoResult.GetErrorMessage());
            }
        }
        else
        {
            logger.LogWarning("No Location Code For This ProcessStateId = {ProcessStateId}, CountryCode = {CountryCode}",
                processState.Id, analysis.CountryCode);
        }

        // سایت با داده‌ی کافی: قانون عددی کافی است و هزینه‌ی AI و جست‌وجو حذف می‌شود
        if (targetKeywords >= MinTargetKeywordsForDataOnly)
        {
            var selected = candidates.Where(s => s.Overlap >= MinKeywordOverlap)
                .OrderByDescending(s => s.Intersections)
                .Take(slots)
                .Select(s => s.Domain)
                .ToList();

            if (selected.Count == slots)
            {
                logger.LogInformation(
                    "Competitors Selected From Data For This ProcessStateId = {ProcessStateId}, TargetKeywords = {TargetKeywords}",
                    processState.Id, targetKeywords);

                return await SaveAndMoveNext(processState, selected, cancellationToken);
            }
        }

        var aiResult = await aiService.GetCompletionV2Async<DetectCompetitorsAiResponse>(
            DetectCompetitorsRequestBuilder.Build(analysis.Target, analysis.BrandName, analysis.Keyword,
                analysis.CountryCode, slots, existingDomains,
                candidates.Select(s => new DetectCompetitorsCandidate(s.Domain, s.Intersections, s.TotalKeywords)).ToList()),
            cancellationToken);

        if (!aiResult.IsSuccess)
        {
            logger.LogWarning("Ai Detect Competitors Failed For This ProcessStateId = {ProcessStateId}, Error = {Error}",
                processState.Id, aiResult.GetErrorMessage());

            return OperationResult.FailureResult(aiResult.Errors);
        }

        var aiResponse = aiResult.Result!;

        if (ModelPricing.TryCalculateCost(aiResponse.MapToCalculateCost(), out var aiCost))
            processState.TotalCost += aiCost;
        else
            logger.LogWarning("No Price For Model {Model}, ProcessStateId = {ProcessStateId}",
                aiResponse.Model, processState.Id);

        logger.LogInformation(
            "Ai Detect Competitors Completed For This ProcessStateId = {ProcessStateId}, WebSearchCalls = {WebSearchCalls}, Cost = {Cost}",
            processState.Id, aiResponse.WebSearchCalls, aiCost);

        var aiDomains = new List<string>();

        foreach (var input in aiResponse.Data.Competitors)
        {
            if (aiDomains.Count == slots)
                break;

            // خروجی AI معتبر فرض نمی‌شود
            if (!TargetHelper.TryParse(input, out var domain, out var type) || type != TargetType.Domain)
            {
                logger.LogWarning("Invalid Ai Competitor For This ProcessStateId = {ProcessStateId}, Competitor = {Competitor}",
                    processState.Id, input);
                continue;
            }

            if (usedBrands.Add(GetBrandLabel(domain)))
                aiDomains.Add(domain);
        }

        return await SaveAndMoveNext(processState, aiDomains, cancellationToken);
    }

    private async Task<OperationResult> SaveAndMoveNext(ProcessState processState, List<string> domains,
        CancellationToken cancellationToken)
    {
        context.Set<Competitor>().AddRange(domains.Select(domain => new Competitor
        {
            AnalysisId = processState.AnalysisId,
            Domain = domain,
            Source = CompetitorSource.Detected
        }));

        processState.CurrentStep = CurrentStep.GetBacklinks;

        await context.SaveChangesAsync(cancellationToken);

        if (domains.Count == 0)
            logger.LogWarning("No New Competitor Detected For This ProcessStateId = {ProcessStateId}", processState.Id);

        logger.LogInformation(
            "Complete Detect Competitors For This ProcessStateId = {ProcessStateId}, DetectedCount = {DetectedCount}, Next Step = {NextStep}",
            processState.Id, domains.Count, processState.CurrentStep);

        return OperationResult.SuccessResult();
    }

    private static List<CompetitorCandidate> BuildCandidates(List<CompetitorsDomainItem> items, HashSet<string> usedBrands)
    {
        var seenBrands = new HashSet<string>(usedBrands);

        return items
            .Where(s => !string.IsNullOrWhiteSpace(s.Domain))
            .OrderByDescending(s => s.Intersections ?? 0)
            .Where(s => seenBrands.Add(GetBrandLabel(s.Domain!)))
            .Select(s =>
            {
                var intersections = s.Intersections ?? 0;
                var totalKeywords = s.FullDomainMetrics?.Organic?.Count ?? 0;

                return new CompetitorCandidate(s.Domain!, intersections, totalKeywords,
                    totalKeywords == 0 ? 0 : (decimal)intersections / totalKeywords);
            })
            .ToList();
    }

    // برای حالت صفحه، دامنه از آدرس درمی‌آید
    private static string GetDomain(string target)
    {
        var host = Uri.TryCreate(target, UriKind.Absolute, out var uri) ? uri.Host : target;

        return host.StartsWith("www.") ? host[4..] : host;
    }

    // rehlat.com و rehlat.com.sa یک برند حساب می‌شوند
    private static string GetBrandLabel(string domain) => domain.Split('.')[0];

    private record CompetitorCandidate(string Domain, long Intersections, long TotalKeywords, decimal Overlap);
}

#region Result

public class CompetitorsDomainResult
{
    public List<CompetitorsDomainItem>? Items { get; set; }
}

public class CompetitorsDomainItem
{
    public string? Domain { get; set; }
    public long? Intersections { get; set; }
    public CompetitorsDomainMetrics? FullDomainMetrics { get; set; }
}

public class CompetitorsDomainMetrics
{
    public CompetitorsDomainOrganic? Organic { get; set; }
}

public class CompetitorsDomainOrganic
{
    public long? Count { get; set; }
}

public class DetectCompetitorsAiResponse
{
    public List<string> Competitors { get; set; } = [];
}

#endregion

#region Request

public record CompetitorsDomainRequest(
    string Target,
    int LocationCode,
    string LanguageCode,
    bool ExcludeTopDomains = true,
    int Limit = 20)
{
    public string[] ItemTypes { get; init; } = ["organic"];
}

#endregion
