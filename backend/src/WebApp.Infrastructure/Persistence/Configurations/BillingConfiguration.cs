using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class BillingOfferConfiguration : IEntityTypeConfiguration<BillingOffer>
{
    public void Configure(EntityTypeBuilder<BillingOffer> builder)
    {
        builder.ToTable("billing_offers");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.NameFr).HasMaxLength(BillingOffer.MaxNameLength).IsRequired();
        builder.Property(o => o.NameAr).HasMaxLength(BillingOffer.MaxNameLength);
        builder.Property(o => o.NameEn).HasMaxLength(BillingOffer.MaxNameLength);
        builder.Property(o => o.DiscountAmount).HasPrecision(12, 3);
        // Store category ids or store ids (uuid[]): which ones depends on the target, so no FK.
        builder.PrimitiveCollection(o => o.TargetIds).HasField("_targetIds").UsePropertyAccessMode(PropertyAccessMode.Field).IsRequired();
        builder.Ignore(o => o.DomainEvents);
    }
}

internal sealed class CommissionInvoiceConfiguration : IEntityTypeConfiguration<CommissionInvoice>
{
    /// <summary>Numbers of the <c>COM-YYYYMM-NNNNNN</c> references.</summary>
    public const string NumberSequence = "commission_invoice_number_seq";
    /// <summary>Shadow key of a line's invoice.</summary>
    public const string InvoiceKey = "InvoiceId";

    public void Configure(EntityTypeBuilder<CommissionInvoice> builder)
    {
        builder.ToTable("commission_invoices");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Reference).HasMaxLength(24).IsRequired();
        builder.HasIndex(i => i.Reference).IsUnique();
        builder.Property(i => i.StoreName).HasMaxLength(Store.MaxNameLength).IsRequired();
        builder.Property(i => i.Total).HasPrecision(14, 3);
        builder.Property(i => i.PaidAmount).HasPrecision(14, 3);
        builder.Property(i => i.Status).IsRequired();

        builder.HasOne<Store>().WithMany().HasForeignKey(i => i.StoreId).OnDelete(DeleteBehavior.Restrict);
        // One invoice per store and month — the job may run twice, the database says no.
        builder.HasIndex(i => new { i.StoreId, i.Month }).IsUnique();
        builder.HasIndex(i => new { i.Status, i.DueOn });

        builder.HasMany(i => i.Lines).WithOne().HasForeignKey(InvoiceKey).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);

        // An admin approving a payment while the owner declares another: the second save fails.
        builder.Property<uint>("Version").IsRowVersion();
        builder.Ignore(i => i.DomainEvents);
    }
}

internal sealed class CommissionInvoiceLineConfiguration : IEntityTypeConfiguration<CommissionInvoiceLine>
{
    public void Configure(EntityTypeBuilder<CommissionInvoiceLine> builder)
    {
        builder.ToTable("commission_invoice_lines");
        builder.HasKey(l => l.Id);
        builder.Property<Guid>(CommissionInvoiceConfiguration.InvoiceKey);
        builder.HasOne<CoinEntry>().WithMany().HasForeignKey(l => l.CoinEntryId).OnDelete(DeleteBehavior.Restrict);
        // Each ledger entry is billed exactly once — the ledger itself stays untouched (append-only, 019).
        builder.HasIndex(l => l.CoinEntryId).IsUnique();
        builder.Property(l => l.OrderReference).HasMaxLength(20);
        builder.Property(l => l.ProductName).HasMaxLength(Product.MaxNameLength);
        builder.Property(l => l.AffiliateCode).HasMaxLength(20);
        builder.Property(l => l.LineTotal).HasPrecision(14, 3);
        builder.Property(l => l.Coins).HasPrecision(14, 3);
        builder.Property(l => l.CoinValueInTnd).HasPrecision(12, 3);
        builder.Property(l => l.Amount).HasPrecision(14, 3);
    }
}

internal sealed class StorePaymentConfiguration : IEntityTypeConfiguration<StorePayment>
{
    /// <summary>Numbers of the <c>PMT-YYYY-NNNNNN</c> references.</summary>
    public const string NumberSequence = "store_payment_number_seq";

    public void Configure(EntityTypeBuilder<StorePayment> builder)
    {
        builder.ToTable("store_payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Number).HasMaxLength(20).IsRequired();
        builder.HasIndex(p => p.Number).IsUnique();
        builder.Property(p => p.Kind).IsRequired();
        builder.Property(p => p.Status).IsRequired();
        builder.Property(p => p.Amount).HasPrecision(14, 3);
        builder.Property(p => p.MonthlyPrice).HasPrecision(12, 3);
        builder.Property(p => p.Discount).HasPrecision(14, 3);
        builder.Property(p => p.Reference).HasMaxLength(StorePayment.MaxReferenceLength);
        builder.Property(p => p.Note).HasMaxLength(StorePayment.MaxNoteLength);
        builder.Property(p => p.OfferName).HasMaxLength(BillingOffer.MaxNameLength);
        builder.Property(p => p.RejectionReason).HasMaxLength(StorePayment.MaxReasonLength);

        builder.HasOne<Store>().WithMany().HasForeignKey(p => p.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CommissionInvoice>().WithMany().HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BillingOffer>().WithMany().HasForeignKey(p => p.OfferId).OnDelete(DeleteBehavior.Restrict);
        // The receipt photo can't be deleted while a payment shows it (ImageRepository.IsInUseAsync checks first).
        builder.HasOne<StoredImage>().WithMany().HasForeignKey(p => p.ReceiptImageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.DeclaredById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.DecidedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.StoreId, p.CreatedAt });
        builder.HasIndex(p => new { p.Status, p.CreatedAt });

        // Two admins deciding the same declaration: the second save fails (xmin).
        builder.Property<uint>("Version").IsRowVersion();
        builder.Ignore(p => p.DomainEvents);
    }
}
