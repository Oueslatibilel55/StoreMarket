using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class CoinEntryConfiguration : IEntityTypeConfiguration<CoinEntry>
{
    public void Configure(EntityTypeBuilder<CoinEntry> builder)
    {
        builder.ToTable("coin_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).IsRequired();
        builder.Property(e => e.Coins).HasPrecision(14, 3);
        builder.Property(e => e.LineTotal).HasPrecision(14, 3);
        builder.Property(e => e.CoinValueInTnd).HasPrecision(12, 3);
        builder.Property(e => e.OrderReference).HasMaxLength(20);
        builder.Property(e => e.ClientName).HasMaxLength(Order.MaxNameLength);
        builder.Property(e => e.ClientPhone).HasMaxLength(20);
        builder.Property(e => e.ProductName).HasMaxLength(Product.MaxNameLength);
        builder.Property(e => e.StoreName).HasMaxLength(Store.MaxNameLength);
        builder.Property(e => e.Reason).HasMaxLength(CoinEntry.MaxReasonLength);

        // Everything an entry points to outlives it (nothing is hard-deleted); the FKs only guard that.
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AffiliateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Order>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderLine>().WithMany().HasForeignKey(e => e.OrderLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CoinEntry>().WithMany().HasForeignKey(e => e.RelatedEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayoutRequest>().WithMany().HasForeignKey(e => e.PayoutRequestId).OnDelete(DeleteBehavior.Restrict);
        // A payout locks its coins once and releases them at most once (021).
        builder.HasIndex(e => new { e.PayoutRequestId, e.Type }).IsUnique().HasFilter("payout_request_id IS NOT NULL");

        // A delivered line is credited once, whatever happens (retries, replays) — the database guarantees it.
        builder.HasIndex(e => new { e.OrderLineId, e.Type }).IsUnique()
            .HasFilter($"order_line_id IS NOT NULL AND type = {(int)CoinEntryType.Earned}");
        builder.HasIndex(e => new { e.AffiliateId, e.CreatedAt });
        builder.HasIndex(e => new { e.StoreId, e.CreatedAt });

        builder.Ignore(e => e.DomainEvents);
    }
}

internal sealed class CoinAccountConfiguration : IEntityTypeConfiguration<CoinAccount>
{
    public void Configure(EntityTypeBuilder<CoinAccount> builder)
    {
        builder.ToTable("coin_accounts");
        builder.HasKey(a => a.AffiliateId);
        builder.Property(a => a.Balance).HasPrecision(16, 3);
        builder.Property(a => a.TotalEarned).HasPrecision(16, 3);
        builder.HasOne<User>().WithOne().HasForeignKey<CoinAccount>(a => a.AffiliateId).OnDelete(DeleteBehavior.Restrict);
    }
}
