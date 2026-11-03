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

// نوع هر فرصت، مثل خبری یا وبلاگ، تا کاربر بداند با هر کدام چه کار کند
public class ClassifyOpportunities(CommonServices<ClassifyOpportunities> commonServices, AiService aiService)
    : IProcessStep
{
    public bool CanHandle(CurrentStep currentStep) => currentStep is CurrentStep.ClassifyOpportunities;

    public async Task<OperationResult> Execute(ProcessState processState, CancellationToken cancellationToken)
    {
        var context = commonServices.Context;
        var logger = commonServices.Logger;

        // فرصتی که قبلاً دسته‌بندی شده دوباره پول نمی‌گیرد
        var opportunities = await context.LinkOpportunities
            .Where(s => s.AnalysisId == processState.AnalysisId && s.Category == null)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Start Classify Opportunities For This ProcessStateId = {ProcessStateId}, OpportunityCount = {OpportunityCount}",
            processState.Id, opportunities.Count);

        if (opportunities.Count > 0)
            await Classify(opportunities, processState, cancellationToken);

        processState.CurrentStep = CurrentStep.GenerateAiAnalysis;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Complete Classify Opportunities For This ProcessStateId = {ProcessStateId}, ClassifiedCount = {ClassifiedCount}, Next Step = {NextStep}",
            processState.Id, opportunities.Count(s => s.Category is not null), processState.CurrentStep);

        return OperationResult.SuccessResult();
    }

    private async Task Classify(List<LinkOpportunity> opportunities, ProcessState processState,
        CancellationToken cancellationToken)
    {
        var logger = commonServices.Logger;

        var aiResult = await aiService.GetCompletionV2Async<ClassifyOpportunitiesAiResponse>(
            ClassifyOpportunitiesRequestBuilder.Build(opportunities
                .Select(s => new ClassifyOpportunityInput(s.Domain, s.PageUrl, s.PageAnchor)).ToList()),
            cancellationToken);

        // دسته‌بندی فقط به گزارش کمک می‌کند؛ بدونش فرصت‌ها بدون نوع نمایش داده می‌شوند
        if (!aiResult.IsSuccess)
        {
            logger.LogWarning(
                "Classify Opportunities Failed For This ProcessStateId = {ProcessStateId}, Error = {Error}, Continue Without Category",
                processState.Id, aiResult.GetErrorMessage());
            return;
        }

        var aiResponse = aiResult.Result!;

        if (ModelPricing.TryCalculateCost(aiResponse.MapToCalculateCost(), out var aiCost))
            processState.TotalCost += aiCost;
        else
            logger.LogWarning("No Price For Model {Model}, ProcessStateId = {ProcessStateId}",
                aiResponse.Model, processState.Id);

        var byDomain = opportunities.ToDictionary(s => s.Domain, StringComparer.OrdinalIgnoreCase);

        foreach (var item in aiResponse.Data.Items)
        {
            // خروجی AI معتبر فرض نمی‌شود: دامنه‌ی ناشناخته یا دسته‌ی نامعتبر رد می‌شود
            if (!byDomain.TryGetValue(item.Domain, out var opportunity)
                || !Enum.TryParse<OpportunityCategory>(item.Category, ignoreCase: true, out var category))
            {
                logger.LogWarning(
                    "Reject Opportunity Category For This ProcessStateId = {ProcessStateId}, Domain = {Domain}, Category = {Category}",
                    processState.Id, item.Domain, item.Category);
                continue;
            }

            opportunity.Category = category;
        }

        logger.LogInformation(
            "Ai Classify Opportunities Completed For This ProcessStateId = {ProcessStateId}, ReturnedCount = {ReturnedCount}, Cost = {Cost}",
            processState.Id, aiResponse.Data.Items.Count, aiCost);
    }
}

#region Response

public class ClassifyOpportunitiesAiResponse
{
    public List<ClassifiedOpportunityItem> Items { get; set; } = [];
}

public class ClassifiedOpportunityItem
{
    public string Domain { get; set; } = null!;

    public string Category { get; set; } = null!;
}

#endregion
