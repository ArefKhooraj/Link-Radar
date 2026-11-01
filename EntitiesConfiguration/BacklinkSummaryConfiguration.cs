using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class BacklinkSummaryConfiguration : IEntityTypeConfiguration<BacklinkSummary>
{
    public void Configure(EntityTypeBuilder<BacklinkSummary> builder)
    {
        builder.HasKey(x => x.Id);

        #region Relation

        builder.HasOne(s => s.Analysis)
            .WithOne(s => s.BacklinkSummary)
            .HasForeignKey<BacklinkSummary>(s => s.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        #endregion
    }
}