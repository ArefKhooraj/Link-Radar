using System.Linq.Expressions;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Context;

public class LinkRadarDbContext(DbContextOptions<LinkRadarDbContext> options) : DbContext(options)
{
    #region DbSet

    public DbSet<User> Users { get; set; }

    public DbSet<Webhook> Webhooks { get; set; }

    public DbSet<Analysis> Analyses { get; set; }

    public DbSet<ProcessState> ProcessStates { get; set; }

    public DbSet<ProcessStepLog> ProcessStepLogs { get; set; }

    public DbSet<BacklinkSummary> BacklinkSummaries { get; set; }

    public DbSet<Backlink> Backlinks { get; set; }

    public DbSet<BacklinkAnchor> BacklinkAnchors { get; set; }

    public DbSet<ReferringDomain> ReferringDomains { get; set; }
    
    public DbSet<BacklinkTimeSeries> BacklinkTimeSeries { get; set; }
    
    public DbSet<LinkGapDomain> LinkGapDomains { get; set; }

    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LinkRadarDbContext).Assembly);
    }
}