using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Entities;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("carts");
        builder.HasKey(c => c.Id);

        // One cart per user.
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => c.UserId).IsUnique();

        builder.HasMany(c => c.Lines).WithOne().HasForeignKey("CartId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class CartLineConfiguration : IEntityTypeConfiguration<CartLine>
{
    public void Configure(EntityTypeBuilder<CartLine> builder)
    {
        builder.ToTable("cart_lines");
        builder.HasKey(l => l.Id);
        builder.Property<Guid>("CartId");

        // Products are never hard-deleted (009), so the FK is safe. No FK on the variant: an owner's edit may
        // remove a combination — the cart then shows the line as unavailable instead of losing it.
        builder.HasOne<Product>().WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(l => l.AffiliateCode).HasMaxLength(12);

        builder.HasIndex("CartId", nameof(CartLine.ProductId), nameof(CartLine.VariantId)).IsUnique().AreNullsDistinct(false);
    }
}
