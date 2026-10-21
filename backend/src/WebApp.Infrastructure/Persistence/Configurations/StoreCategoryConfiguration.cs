using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Entities;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class StoreCategoryConfiguration : IEntityTypeConfiguration<StoreCategory>
{
    public void Configure(EntityTypeBuilder<StoreCategory> builder)
    {
        builder.ToTable("store_categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.NameFr).HasMaxLength(StoreCategory.MaxNameLength).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(StoreCategory.MaxNameLength);
        builder.Property(c => c.NameEn).HasMaxLength(StoreCategory.MaxNameLength);
        builder.Property(c => c.Slug).HasMaxLength(StoreCategory.MaxSlugLength).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.Property(c => c.Icon).HasMaxLength(StoreCategory.MaxIconLength);
        builder.HasIndex(c => new { c.IsActive, c.SortOrder });

        // An image in use can't be deleted (the service checks first; the FK is the backstop).
        builder.HasOne<StoredImage>().WithMany().HasForeignKey(c => c.ImageId).OnDelete(DeleteBehavior.Restrict);
    }
}
