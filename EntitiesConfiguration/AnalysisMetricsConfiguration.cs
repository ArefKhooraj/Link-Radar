using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class AnalysisMetricsConfiguration : IEntityTypeConfiguration<AnalysisMetrics>
{
    public void Configure(EntityTypeBuilder<AnalysisMetrics> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(s => s.HealthScore).HasPrecision(5, 2);
        builder.Property(s => s.HealthStatus).HasConversion<string>().HasMaxLength(20);

        builder.OwnsOne(s => s.Detail, b => b.ToJson());
        // enum به‌صورت متن، تا اضافه شدن مقدار جدید وسط enum معنای بینش‌های قبلی را عوض نکند
        builder.OwnsMany(s => s.Insights, b =>
        {
            b.ToJson();
            b.Property(s => s.Code).HasConversion<string>();
            b.Property(s => s.Type).HasConversion<string>();
            b.Property(s => s.Severity).HasConversion<string>();
        });

        #region Relation

        builder.HasOne(s => s.Analysis)
            .WithOne(s => s.AnalysisMetrics)
            .HasForeignKey<AnalysisMetrics>(s => s.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        #endregion
    }
}
