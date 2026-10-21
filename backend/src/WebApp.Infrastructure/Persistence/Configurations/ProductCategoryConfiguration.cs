using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("product_categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.NameFr).HasMaxLength(ProductCategory.MaxNameLength).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(ProductCategory.MaxNameLength);
        builder.Property(c => c.NameEn).HasMaxLength(ProductCategory.MaxNameLength);
        builder.Property(c => c.Slug).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();

        // A parent with children can't be deleted (the service checks first; the FK is the backstop).
        builder.HasOne<ProductCategory>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.ParentId, c.SortOrder });

        // Attributes belong to the category aggregate (only reached through it), deleted with it.
        // A regular child entity rather than an owned one: EF can't put a JSON column in an owned table.
        builder.HasMany(c => c.Attributes).WithOne().HasForeignKey("CategoryId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Attributes).HasField("_attributes").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class AttributeDefinitionConfiguration : IEntityTypeConfiguration<AttributeDefinition>
{
    public void Configure(EntityTypeBuilder<AttributeDefinition> builder)
    {
        builder.ToTable("product_attributes");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Key).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.Property<Guid>("CategoryId");
        builder.HasIndex("CategoryId", nameof(AttributeDefinition.Key)).IsUnique();
        builder.Property(a => a.NameFr).HasMaxLength(ProductCategory.MaxNameLength).IsRequired();
        builder.Property(a => a.NameAr).HasMaxLength(ProductCategory.MaxNameLength);
        builder.Property(a => a.NameEn).HasMaxLength(ProductCategory.MaxNameLength);
        builder.Property(a => a.Unit).HasMaxLength(AttributeDefinition.MaxUnitLength);
        builder.Property(a => a.Type).IsRequired();

        // Choice values as a jsonb array on the attribute row.
        builder.OwnsMany(a => a.Options, o => o.ToJson());
        builder.Navigation(a => a.Options).HasField("_options").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
