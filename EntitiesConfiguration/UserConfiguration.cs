using LinkRadar.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkRadar.Api.EntitiesConfiguration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ApiKey).IsRequired();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(250);
        builder.Property(s => s.IsActive).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.CreateDate).HasDefaultValue(DateTimeOffset.UtcNow);
        
    }
}