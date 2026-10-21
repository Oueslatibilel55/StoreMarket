using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("stores");
        builder.HasKey(s => s.Id);

        // One store per owner in v1.
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.OwnerId).IsUnique();

        builder.Property(s => s.Name).HasMaxLength(Store.MaxNameLength).IsRequired();
        builder.Property(s => s.Slug).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.HasIndex(s => s.Slug).IsUnique();
        builder.Property(s => s.Description).HasMaxLength(Store.MaxDescriptionLength);
        builder.Property(s => s.Phone).HasMaxLength(20).IsRequired();
        builder.Property(s => s.City).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Address).HasMaxLength(Store.MaxAddressLength);
        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.DeliveryFee).HasPrecision(10, 3);
        builder.Property(s => s.SubscriptionMonthlyPrice).HasPrecision(10, 3);
        builder.Property(s => s.FacebookUrl).HasMaxLength(Store.MaxUrlLength);
        builder.Property(s => s.InstagramUrl).HasMaxLength(Store.MaxUrlLength);
        builder.Property(s => s.WebsiteUrl).HasMaxLength(Store.MaxUrlLength);
        builder.Property(s => s.ModerationReason).HasMaxLength(Store.MaxModerationReasonLength);

        // Governorate codes as a PostgreSQL text[] column.
        builder.PrimitiveCollection(s => s.DeliveryCities)
            .HasField("_deliveryCities")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        // A category or an image in use can't be deleted (services check first; the FKs are the backstop).
        builder.HasOne<StoreCategory>().WithMany().HasForeignKey(s => s.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StoredImage>().WithMany().HasForeignKey(s => s.LogoImageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StoredImage>().WithMany().HasForeignKey(s => s.BannerImageId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.Status, s.CategoryId });
        builder.Ignore(s => s.DomainEvents);
    }
}
