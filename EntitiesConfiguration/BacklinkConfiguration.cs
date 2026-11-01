using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class BacklinkConfiguration : IEntityTypeConfiguration<Backlink>
{
    public void Configure(EntityTypeBuilder<Backlink> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(s => s.DomainFrom).IsRequired().HasMaxLength(253);
        builder.Property(s => s.UrlFrom).IsRequired();
        builder.Property(s => s.UrlTo).IsRequired();
        builder.Property(s => s.ItemType).HasMaxLength(50);
        builder.Property(s => s.Attributes).HasMaxLength(200);
        builder.Property(s => s.SemanticLocation).HasMaxLength(50);
        builder.Property(s => s.PageFromLanguage).HasMaxLength(50);
        builder.Property(s => s.TldFrom).HasMaxLength(63);

        #region Relation

        builder.HasOne(s => s.Analysis)
            .WithMany(s => s.Backlinks)
            .HasForeignKey(s => s.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        #endregion
    }
}