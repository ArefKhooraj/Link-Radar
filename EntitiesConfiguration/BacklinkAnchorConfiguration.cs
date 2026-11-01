using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class BacklinkAnchorConfiguration : IEntityTypeConfiguration<BacklinkAnchor>
{
    public void Configure(EntityTypeBuilder<BacklinkAnchor> builder)
    {
        builder.HasKey(x => x.Id);

        #region Relation

        builder.HasOne(s => s.Analysis)
            .WithMany(s => s.BacklinkAnchors)
            .HasForeignKey(s => s.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        #endregion
    }
}