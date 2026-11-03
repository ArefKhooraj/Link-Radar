using LinkRadar.Api.Context;
using LinkRadar.Api.DataForSeoServices;

namespace LinkRadar.Api.Services;

public sealed class CommonServices<T>(
    LinkRadarDbContext context,
    DataForSeoService dataForSeoService,
    ILogger<T> logger) where T : class
{
    public LinkRadarDbContext Context { get; } = context;
    public DataForSeoService DataForSeoService { get; } = dataForSeoService;
    public ILogger<T> Logger { get; } = logger;
};