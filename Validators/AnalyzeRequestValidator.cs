using FluentValidation;
using LinkRadar.Api.Dtos.Analyze;

namespace LinkRadar.Api.Validators;

public class AnalyzeRequestValidator : AbstractValidator<AnalyzeRequestDto>
{
    public AnalyzeRequestValidator()
    {
        RuleFor(s => s.EntityId).GreaterThan(0);
        RuleFor(s => s.Domain).NotEmpty().MaximumLength(2048);
        RuleFor(s => s.CountryCode).NotEmpty().Length(2);
        RuleFor(s => s.BrandName).MaximumLength(250);
        RuleFor(s => s.Keywords).MaximumLength(200);
        RuleFor(s => s.Competitors).Must(s => s is null || s.Count <= 5);
    }
}