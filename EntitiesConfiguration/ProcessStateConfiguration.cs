using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class ProcessStateConfiguration : IEntityTypeConfiguration<ProcessState>
{
    public void Configure(EntityTypeBuilder<ProcessState> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(s => s.CurrentStep).HasConversion<string>().IsRequired().HasMaxLength(50);
        builder.Property(s => s.Status).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(s => s.LastError).HasMaxLength(2000);
        builder.Property(s => s.RowVersion).IsRowVersion();

        // job دوره‌ای timeout با این ایندکس پردازش‌های گیرکرده را پیدا می‌کند
        builder.HasIndex(s => new { s.Status, s.StepStartedAt });

        #region Relation

        builder.HasOne(s => s.Analysis)
            .WithOne(s => s.ProcessState)
            .HasForeignKey<ProcessState>(s => s.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        #endregion
    }
}
