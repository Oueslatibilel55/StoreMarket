using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Target).IsRequired();
        builder.Property(r => r.AuthorName).HasMaxLength(Review.MaxAuthorNameLength).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(Review.MaxCommentLength);
        builder.Property(r => r.Reply).HasMaxLength(Review.MaxReplyLength);
        builder.Property(r => r.HiddenReason).HasMaxLength(Review.MaxReasonLength);
        builder.ToTable(t => t.HasCheckConstraint("ck_reviews_stars", "stars BETWEEN 1 AND 5"));

        // Orders and catalogue rows are never hard-deleted (soft delete); restrict keeps it that way.
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Order>().WithMany().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StoreOrder>().WithMany().HasForeignKey(r => r.StoreOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderLine>().WithMany().HasForeignKey(r => r.OrderLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Store>().WithMany().HasForeignKey(r => r.StoreId).OnDelete(DeleteBehavior.Restrict);

        // One review per order line (product) and one per store order (store) — the database backs up the service check.
        builder.HasIndex(r => r.OrderLineId).IsUnique()
            .HasFilter($"target = {(int)ReviewTarget.Product}")
            .HasDatabaseName("ix_reviews_order_line_id_product");
        builder.HasIndex(r => r.StoreOrderId).IsUnique()
            .HasFilter($"target = {(int)ReviewTarget.Store}")
            .HasDatabaseName("ix_reviews_store_order_id_store");

        builder.HasIndex(r => new { r.ProductId, r.CreatedAt });
        builder.HasIndex(r => new { r.StoreId, r.Target, r.CreatedAt });
        builder.HasIndex(r => new { r.AuthorId, r.CreatedAt });
        builder.Ignore(r => r.DomainEvents);
    }
}
