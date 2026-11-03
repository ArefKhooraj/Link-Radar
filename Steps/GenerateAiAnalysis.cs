using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinkRadar.Api.AIRequestModels;
using LinkRadar.Api.AiServices;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Hangfire.Interface;
using LinkRadar.Api.Mapper;
using LinkRadar.Api.ResultPattern;
using LinkRadar.Api.Services;
using LinkRadar.Api.Statics;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Steps;

// متن گزارش و برنامه‌ی کاری؛ AI فقط توضیح می‌دهد و عددها از ComputeMetrics می‌آیند
public class GenerateAiAnalysis(CommonServices<GenerateAiAnalysis> commonServices, AiService aiService)
    : IProcessStep
{
    private const int MaxOpportunities = 15;
    private const int MaxLostDomains = 10;
    private const int MaxRiskyDomains = 10;
    private const int RiskySpamScore = 50;

    private static readonly JsonSerializerOptions FactsJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.GenerateAiAnalysis;

    public async Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken)
    {
        var context = commonServices.Context;
        var logger = commonServices.Logger;
        var analysisId = processState.AnalysisId;

        var analysis = await context.Analyses.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == analysisId, cancellationToken);
        var summary = await context.BacklinkSummaries.AsNoTracking()
            .FirstOrDefaultAsync(s => s.AnalysisId == analysisId, cancellationToken);
        var metrics = await context.AnalysisMetrics.AsNoTracking()
            .FirstOrDefaultAsync(s => s.AnalysisId == analysisId, cancellationToken);

        if (analysis is null || summary is null || metrics is null)
        {
            logger.LogError(
                "Can Not Find Analysis, Summary Or Metrics For This ProcessStateId = {ProcessStateId}, AnalysisId = {AnalysisId}",
                processState.Id, analysisId);

            return OperationResult.FailureResult(Error.NotFound(ErrorCodes.Analysis.InvalidTarget,
                "Analysis, Summary Or Metrics Not Found"));
        }

        logger.LogInformation("Start Generate Ai Analysis For This ProcessStateId = {ProcessStateId}, Target = {Target}",
            processState.Id, analysis.Target);

        var factsJson = await BuildFacts(analysis, summary, metrics, cancellationToken);

        var aiResult = await aiService.GetCompletionV2Async<AiAnalysisResponse>(
            GenerateAiAnalysisRequestBuilder.Build(factsJson), cancellationToken);

        // گزارش اصلی‌ترین خروجی است؛ بدون آن پایش کامل نیست و Hangfire تکرار می‌کند
        if (!aiResult.IsSuccess)
        {
            logger.LogWarning("Generate Ai Analysis Failed For This ProcessStateId = {ProcessStateId}, Error = {Error}",
                processState.Id, aiResult.GetErrorMessage());

            return OperationResult.FailureResult(aiResult.Errors);
        }

        var aiResponse = aiResult.Result!;

        if (ModelPricing.TryCalculateCost(aiResponse.MapToCalculateCost(), out var aiCost))
            processState.TotalCost += aiCost;
        else
            logger.LogWarning("No Price For Model {Model}, ProcessStateId = {ProcessStateId}",
                aiResponse.Model, processState.Id);

        // خلاصه بدون یکی از دو زبان قابل نمایش نیست؛ هزینه بالا ثبت شده و Hangfire تکرار می‌کند
        if (!IsComplete(aiResponse.Data.Summary))
        {
            logger.LogWarning("Ai Analysis Summary Is Incomplete For This ProcessStateId = {ProcessStateId}",
                processState.Id);

            return OperationResult.FailureResult(Error.ExternalService(ErrorCodes.Ai.RequestFailed,
                "Report Summary Is Missing A Language", isTransient: true));
        }

        var report = new AnalysisReport
        {
            AnalysisId = analysisId,
            Summary = aiResponse.Data.Summary,
            Model = aiResponse.Model,
            // موردی که یکی از دو زبانش خالی است کنار می‌رود تا دو زبان یکی بمانند
            Findings = aiResponse.Data.Findings
                .Where(s => Enum.TryParse<InsightType>(s.Type, true, out _) && IsComplete(s.Title) && IsComplete(s.Description))
                .Select(s => new ReportFinding
                {
                    Title = s.Title,
                    Description = s.Description,
                    Type = Enum.Parse<InsightType>(s.Type, true)
                })
                .ToList(),
            Actions = aiResponse.Data.Actions
                .Where(s => Enum.TryParse<ActionPriority>(s.Priority, true, out _) && IsComplete(s.Title) && IsComplete(s.Description))
                .Select(s => new ReportAction
                {
                    Title = s.Title,
                    Description = s.Description,
                    Priority = Enum.Parse<ActionPriority>(s.Priority, true)
                })
                .ToList()
        };

        // دوباره‌اجراشدنی: گزارش قبلی همین پایش جایگزین می‌شود
        await context.AnalysisReports.Where(s => s.AnalysisId == analysisId).ExecuteDeleteAsync(cancellationToken);

        context.AnalysisReports.Add(report);

        processState.CurrentStep = CurrentStep.SendToUserWebhook;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Generate Ai Analysis For This ProcessStateId = {ProcessStateId}, FindingCount = {FindingCount}, ActionCount = {ActionCount}, Cost = {Cost}, Next Step = {NextStep}",
            processState.Id, report.Findings.Count, report.Actions.Count, aiCost, processState.CurrentStep);

        return OperationResult.SuccessResult();
    }

    private static bool IsComplete(LocalizedText? text) =>
        !string.IsNullOrWhiteSpace(text?.Ar) && !string.IsNullOrWhiteSpace(text.En);

    private async Task<string> BuildFacts(Analysis analysis, BacklinkSummary summary, AnalysisMetrics metrics,
        CancellationToken cancellationToken)
    {
        var context = commonServices.Context;
        var analysisId = analysis.Id;

        var competitors = await context.Set<Competitor>().AsNoTracking()
            .Where(s => s.AnalysisId == analysisId && s.ReferringDomains != null)
            .Select(s => new { s.Domain, s.Rank, s.ReferringDomains, s.Backlinks })
            .ToListAsync(cancellationToken);

        var opportunities = await context.LinkOpportunities.AsNoTracking()
            .Where(s => s.AnalysisId == analysisId)
            .OrderByDescending(s => s.Score)
            .Take(MaxOpportunities)
            .Select(s => new { s.Domain, s.Category, s.CompetitorCount, s.Rank, s.PageUrl })
            .ToListAsync(cancellationToken);

        var riskyDomains = await context.ReferringDomains.AsNoTracking()
            .Where(s => s.AnalysisId == analysisId && s.BacklinksSpamScore > RiskySpamScore)
            .OrderByDescending(s => s.Backlinks)
            .Take(MaxRiskyDomains)
            .Select(s => new { s.Domain, s.BacklinksSpamScore, s.Backlinks })
            .ToListAsync(cancellationToken);

        var backlinks = await context.Backlinks.AsNoTracking()
            .Where(s => s.AnalysisId == analysisId)
            .Select(s => new { s.DomainFrom, s.IsLost, s.Rank, s.LastSeen })
            .ToListAsync(cancellationToken);
        var referringDomainNames = await context.ReferringDomains.AsNoTracking()
            .Where(s => s.AnalysisId == analysisId)
            .Select(s => s.Domain)
            .ToListAsync(cancellationToken);

        // مثل ComputeMetrics: دامنه‌ای که هنوز لینک زنده دارد از دست رفته نیست
        var liveDomains = backlinks.Where(s => !s.IsLost).Select(s => s.DomainFrom).Concat(referringDomainNames).ToList();
        var lostDomains = backlinks
            .Where(s => s.IsLost && !liveDomains.Any(live => ComputeMetrics.IsSameOrSubdomain(s.DomainFrom, live)))
            .OrderByDescending(s => s.Rank)
            .Take(MaxLostDomains)
            .Select(s => new { Domain = s.DomainFrom, s.Rank, s.LastSeen })
            .ToList();

        var facts = new
        {
            site = new
            {
                domain = analysis.Target,
                brand = analysis.BrandName,
                country = analysis.CountryCode,
                rank = summary.Rank,
                backlinks = summary.Backlinks,
                referringDomains = summary.ReferringDomains
            },
            healthScore = metrics.HealthScore,
            healthStatus = metrics.HealthStatus,
            metrics = metrics.Detail,
            insights = metrics.Insights,
            competitors,
            topOpportunities = opportunities,
            riskyDomains,
            lostDomains
        };

        return JsonSerializer.Serialize(facts, FactsJsonOptions);
    }
}

#region Response

public class AiAnalysisResponse
{
    public LocalizedText Summary { get; set; } = null!;

    public List<AiAnalysisFinding> Findings { get; set; } = [];

    public List<AiAnalysisAction> Actions { get; set; } = [];
}

public class AiAnalysisFinding
{
    public LocalizedText Title { get; set; } = null!;

    public LocalizedText Description { get; set; } = null!;

    public string Type { get; set; } = null!;
}

public class AiAnalysisAction
{
    public LocalizedText Title { get; set; } = null!;

    public LocalizedText Description { get; set; } = null!;

    public string Priority { get; set; } = null!;
}

#endregion
