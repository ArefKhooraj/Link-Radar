namespace LinkRadar.Api.Dtos.Analyze;

public record AnalyzeRequestDto(
    int EntityId,
    string Domain,
    string CountryCode,
    string? BrandName,
    string? Keywords,
    List<string>? Competitors);