using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class AnalysisReportConfiguration : IEntityTypeConfiguration<AnalysisReport>
{
    public void Configure(EntityTypeBuilder<AnalysisReport> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(s => s.Model).IsRequired().HasMaxLength(100);

        builder.OwnsOne(s => s.Summary, b => b.ToJson());

        // enum به‌صورت متن، تا اضافه شدن مقدار جدید وسط enum معنای گزارش‌های قبلی را عوض نکند
        builder.OwnsMany(s => s.Findings, b =>
        {
            b.ToJson();
            b.Property(s => s.Type).HasConversion<string>();
            b.OwnsOne(s => s.Title);
            b.OwnsOne(s => s.Description);
        });

        builder.OwnsMany(s => s.Actions, b =>
        {
            b.ToJson();
            b.Property(s => s.Priority).HasConversion<string>();
            b.OwnsOne(s => s.Title);
            b.OwnsOne(s => s.Description);
        });

        #region Relation

        builder.HasOne(s => s.Analysis)
            .WithOne(s => s.AnalysisReport)
            .HasForeignKey<AnalysisReport>(s => s.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        #endregion
    }
}
