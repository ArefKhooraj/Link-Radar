using LinkRadar.Api.Context;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Configuration;

public static class DataBaseConfiguration
{
    public static IServiceCollection AddSqlServerConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<LinkRadarDbContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("LinkRadarConnectionString"));
        });
        return builder.Services;
    }
}