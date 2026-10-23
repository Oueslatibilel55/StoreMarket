using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);

        builder.HasOne<Store>().WithMany().HasForeignKey(p => p.StoreId).OnDelete(DeleteBehavior.Restrict);
        // A category with products can't be deleted (the service checks first; the FK is the backstop).
        builder.HasOne<ProductCategory>().WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.Name).HasMaxLength(Product.MaxNameLength).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.Property(p => p.Description).HasMaxLength(Product.MaxDescriptionLength);
        builder.Property(p => p.Price).HasPrecision(12, 3);
        builder.Property(p => p.FromPrice).HasPrecision(12, 3);
        builder.HasIndex(p => p.FromPrice);
        builder.Property(p => p.CompareAtPrice).HasPrecision(12, 3);
        builder.Property(p => p.Sku).HasMaxLength(Product.MaxSkuLength);
        builder.Property(p => p.Status).IsRequired();
        builder.Property(p => p.ModerationReason).HasMaxLength(Store.MaxModerationReasonLength);

        // Photos in display order (uuid[]); the image FK can't be expressed on an array, so
        // ImageRepository.IsInUseAsync is what protects them.
        builder.PrimitiveCollection(p => p.ImageIds).HasField("_imageIds").UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();
        builder.PrimitiveCollection(p => p.VariantAxes).HasField("_variantAxes").UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();

        // Field values as a jsonb array of { key, value } on the product row.
        builder.OwnsMany(p => p.Values, v => v.ToJson());
        builder.Navigation(p => p.Values).HasField("_values").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Variants).WithOne().HasForeignKey("ProductId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Variants).HasField("_variants").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => new { p.StoreId, p.Status });
        builder.HasIndex(p => p.CategoryId);

        // Ratings (017), recomputed from the visible reviews; the shop sorts "top rated" on the score.
        builder.Property(p => p.AverageRating).HasPrecision(3, 2);
        builder.Property(p => p.RankingScore).HasPrecision(6, 4);
        builder.HasIndex(p => p.RankingScore);

        // Shop search (018): name + description.
        builder.HasSearchVector();
        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants");
        builder.HasKey(v => v.Id);
        builder.Property<Guid>("ProductId");
        builder.Property(v => v.Price).HasPrecision(12, 3);
        builder.Property(v => v.Sku).HasMaxLength(Product.MaxSkuLength);

        // One option per axis, as a jsonb array of { key, value }.
        builder.OwnsMany(v => v.Selection, s => s.ToJson());
        builder.Navigation(v => v.Selection).HasField("_selection").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
