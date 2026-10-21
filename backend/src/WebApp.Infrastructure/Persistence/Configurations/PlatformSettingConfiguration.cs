using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Entities;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class PlatformSettingConfiguration : IEntityTypeConfiguration<PlatformSetting>
{
    public void Configure(EntityTypeBuilder<PlatformSetting> builder)
    {
        builder.ToTable("platform_settings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Key).HasMaxLength(PlatformSetting.MaxKeyLength).IsRequired();
        builder.HasIndex(s => s.Key).IsUnique();
        builder.Property(s => s.Value).HasMaxLength(PlatformSetting.MaxValueLength).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UpdatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
