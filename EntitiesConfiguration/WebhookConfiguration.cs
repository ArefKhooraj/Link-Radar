using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class WebhookConfiguration : IEntityTypeConfiguration<Webhook>
{
    public void Configure(EntityTypeBuilder<Webhook> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(s => s.WebhookUrl).IsRequired().HasMaxLength(200);
        builder.Property(s => s.HeaderName).IsRequired().HasMaxLength(200);
        builder.Property(s => s.HeaderValue).IsRequired().HasMaxLength(200);

        #region Relation

        builder.HasOne(s => s.User)
            .WithMany(s => s.Webhooks)
            .HasForeignKey(s => s.UserId);

        #endregion
    }
}