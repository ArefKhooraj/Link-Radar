using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class AnalysisConfiguration : IEntityTypeConfiguration<Analysis>
{
    public void Configure(EntityTypeBuilder<Analysis> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(s => s.Domain).IsRequired().HasMaxLength(253);
        builder.Property(s => s.CountryCode).IsRequired().HasMaxLength(2).IsFixedLength();
        builder.Property(s => s.BrandName).HasMaxLength(250);

        // یک درخواست تکراری از ajebhom پایش دوم نمی‌سازد
        builder.HasIndex(s => new { s.UserId, s.EntityId }).IsUnique();

        #region Relation

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        #endregion
    }
}
