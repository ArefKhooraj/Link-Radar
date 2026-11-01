using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class ReferringDomainConfiguration : IEntityTypeConfiguration<ReferringDomain>
{
    public void Configure(EntityTypeBuilder<ReferringDomain> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(s => s.Domain).IsRequired().HasMaxLength(253);

        #region Relation

        builder.HasOne(s => s.Analysis)
            .WithMany(s => s.ReferringDomains)
            .HasForeignKey(s => s.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        #endregion
    }
}