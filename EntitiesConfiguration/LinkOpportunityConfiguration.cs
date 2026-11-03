using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class LinkOpportunityConfiguration : IEntityTypeConfiguration<LinkOpportunity>
{
    public void Configure(EntityTypeBuilder<LinkOpportunity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(s => s.Domain).IsRequired().HasMaxLength(253);
        builder.Property(s => s.Score).HasPrecision(6, 2);
        builder.Property(s => s.PageCompetitorDomain).HasMaxLength(253);
        builder.Property(s => s.Category).HasConversion<string>().HasMaxLength(30);

        // یک دامنه یک بار برای هر پایش
        builder.HasIndex(s => new { s.AnalysisId, s.Domain }).IsUnique();

        #region Relation

        builder.HasOne(s => s.Analysis)
            .WithMany(s => s.LinkOpportunities)
            .HasForeignKey(s => s.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        #endregion
    }
}
