using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class CompetitorConfiguration : IEntityTypeConfiguration<Competitor>
{
    public void Configure(EntityTypeBuilder<Competitor> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(s => s.Domain).IsRequired().HasMaxLength(253);
        builder.Property(s => s.Source).HasConversion<string>().HasMaxLength(20);

        // یک رقیب دو بار برای یک پایش ثبت نمی‌شود
        builder.HasIndex(s => new { s.AnalysisId, s.Domain }).IsUnique();

        #region Relation

        builder.HasOne(s => s.Analysis)
            .WithMany(s => s.Competitors)
            .HasForeignKey(s => s.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        #endregion
    }
}