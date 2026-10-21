using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    /// <summary>Order numbers behind the <c>CMD-2026-000123</c> references.</summary>
    public const string NumberSequence = "order_number_seq";

    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.HasIndex(o => o.Number).IsUnique();
        builder.Property(o => o.Reference).HasMaxLength(20).IsRequired();
        builder.HasIndex(o => o.Reference).IsUnique();

        // Orders outlive everything: users and stores are never hard-deleted, the FKs only protect that.
        builder.HasOne<User>().WithMany().HasForeignKey(o => o.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(o => o.PlacedByAffiliateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => new { o.ClientId, o.CreatedAt });
        builder.HasIndex(o => new { o.PlacedByAffiliateId, o.CreatedAt });

        builder.Property(o => o.ClientName).HasMaxLength(Order.MaxNameLength).IsRequired();
        builder.Property(o => o.ClientPhone).HasMaxLength(20).IsRequired();
        builder.Property(o => o.City).HasMaxLength(30).IsRequired();
        builder.Property(o => o.Address).HasMaxLength(Order.MaxAddressLength).IsRequired();
        builder.Property(o => o.Note).HasMaxLength(Order.MaxNoteLength);
        builder.Property(o => o.CoinValueInTnd).HasPrecision(12, 3);
        builder.Property(o => o.ItemsTotal).HasPrecision(14, 3);
        builder.Property(o => o.DeliveryTotal).HasPrecision(14, 3);
        builder.Property(o => o.Total).HasPrecision(14, 3);

        builder.HasMany(o => o.StoreOrders).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.StoreOrders).HasField("_storeOrders").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(o => o.PlacedByAffiliate);
        builder.Ignore(o => o.DomainEvents);
    }
}

internal sealed class StoreOrderConfiguration : IEntityTypeConfiguration<StoreOrder>
{
    public void Configure(EntityTypeBuilder<StoreOrder> builder)
    {
        builder.ToTable("store_orders");
        builder.HasKey(s => s.Id);
        builder.Property<Guid>("OrderId");

        builder.HasOne<Store>().WithMany().HasForeignKey(s => s.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.StoreId, s.Status });
        builder.Property(s => s.StoreName).HasMaxLength(Store.MaxNameLength).IsRequired();
        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.DeliveryFee).HasPrecision(12, 3);
        builder.Property(s => s.Subtotal).HasPrecision(14, 3);
        builder.Property(s => s.AmountToCollect).HasPrecision(14, 3);

        builder.HasMany(s => s.Lines).WithOne().HasForeignKey("StoreOrderId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(s => s.History).WithOne().HasForeignKey("StoreOrderId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(s => s.PaymentMethod).IsRequired();
        builder.Property(s => s.PaymentStatus).IsRequired();
        // Two people moving the same parcel at once (owner + admin, double click…): the second save fails instead of
        // overwriting — PostgreSQL xmin as concurrency token, no extra column.
        builder.Property<uint>("Version").IsRowVersion();
    }
}

internal sealed class StoreOrderEventConfiguration : IEntityTypeConfiguration<StoreOrderEvent>
{
    public void Configure(EntityTypeBuilder<StoreOrderEvent> builder)
    {
        builder.ToTable("store_order_events");
        builder.HasKey(e => e.Id);
        builder.Property<Guid>("StoreOrderId");
        builder.HasIndex("StoreOrderId", nameof(StoreOrderEvent.At));
        builder.Property(e => e.Reason).HasMaxLength(StoreOrder.MaxReasonLength);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("order_lines");
        builder.HasKey(l => l.Id);
        builder.Property<Guid>("StoreOrderId");

        // Products are soft-deleted only; the variant may disappear from the product, the snapshot keeps its options.
        builder.HasOne<Product>().WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.AffiliateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Partnership>().WithMany().HasForeignKey(l => l.PartnershipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.AffiliateId, l.CreatedAt });

        builder.Property(l => l.ProductName).HasMaxLength(Product.MaxNameLength).IsRequired();
        builder.Property(l => l.ProductSlug).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.Property(l => l.UnitPrice).HasPrecision(12, 3);
        builder.Property(l => l.LineTotal).HasPrecision(14, 3);
        builder.Property(l => l.AffiliateCode).HasMaxLength(12);
        builder.Property(l => l.ExpectedCoins).HasPrecision(14, 3);

        // Variant options frozen as a jsonb array.
        builder.OwnsMany(l => l.Options, o => o.ToJson());
        builder.Navigation(l => l.Options).HasField("_options").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
