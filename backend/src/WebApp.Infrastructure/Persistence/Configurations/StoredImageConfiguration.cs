using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Entities;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class StoredImageConfiguration : IEntityTypeConfiguration<StoredImage>
{
    public void Configure(EntityTypeBuilder<StoredImage> builder)
    {
        builder.ToTable("images");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Path).HasMaxLength(StoredImage.MaxPathLength).IsRequired();
        builder.HasIndex(i => i.Path).IsUnique();
        builder.Property(i => i.ContentType).HasMaxLength(32).IsRequired();
        builder.Property(i => i.OriginalName).HasMaxLength(StoredImage.MaxOriginalNameLength);
        builder.Property(i => i.Purpose).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(i => i.UploadedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.UploadedById, i.CreatedAt });
    }
}
