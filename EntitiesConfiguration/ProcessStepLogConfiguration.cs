using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class ProcessStepLogConfiguration : IEntityTypeConfiguration<ProcessStepLog>
{
    public void Configure(EntityTypeBuilder<ProcessStepLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(s => s.Step).HasConversion<string>().IsRequired().HasMaxLength(50);
        builder.Property(s => s.ErrorCode).HasMaxLength(100);
        builder.Property(s => s.ErrorMessage).HasMaxLength(2000);

        #region Relation

        builder.HasOne(s => s.ProcessState)
            .WithMany(s => s.StepLogs)
            .HasForeignKey(s => s.ProcessStateId)
            .OnDelete(DeleteBehavior.Cascade);

        #endregion
    }
}
