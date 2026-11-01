using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class BacklinkTimeSeriesConfiguration : IEntityTypeConfiguration<BacklinkTimeSeries>
{
    public void Configure(EntityTypeBuilder<BacklinkTimeSeries> builder)
    {
        builder.HasKey(x => x.Id);

        #region Relation

        builder.HasOne(s => s.Analysis)
            .WithMany(s => s.BacklinkTimeSeries)
            .HasForeignKey(s => s.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        #endregion
    }
}