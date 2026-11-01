using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class LinkGapDomainConfiguration : IEntityTypeConfiguration<LinkGapDomain>
{
    public void Configure(EntityTypeBuilder<LinkGapDomain> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(s => s.Domain).IsRequired().HasMaxLength(253);

        // ادغام رقبا: یک دامنه به چند رقیب لینک داده است
        builder.HasIndex(s => s.Domain);

        #region Relation

        builder.HasOne(s => s.Competitor)
            .WithMany(s => s.LinkGapDomains)
            .HasForeignKey(s => s.CompetitorId)
            .OnDelete(DeleteBehavior.Cascade);

        #endregion
    }
}