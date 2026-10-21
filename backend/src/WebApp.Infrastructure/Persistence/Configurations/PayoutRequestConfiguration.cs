using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Infrastructure.Persistence.Configurations;

internal sealed class PayoutRequestConfiguration : IEntityTypeConfiguration<PayoutRequest>
{
    /// <summary>Numbers of the <c>PAY-YYYY-NNNNNN</c> references.</summary>
    public const string NumberSequence = "payout_number_seq";

    public void Configure(EntityTypeBuilder<PayoutRequest> builder)
    {
        builder.ToTable("payout_requests");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Reference).HasMaxLength(20).IsRequired();
        builder.HasIndex(p => p.Reference).IsUnique();

        builder.Property(p => p.Coins).HasPrecision(14, 3);
        builder.Property(p => p.CoinValueInTnd).HasPrecision(12, 3);
        builder.Property(p => p.AmountTnd).HasPrecision(14, 3);
        builder.Property(p => p.Method).IsRequired();
        builder.Property(p => p.Status).IsRequired();
        builder.Property(p => p.BankName).HasMaxLength(PayoutRequest.MaxBankNameLength);
        builder.Property(p => p.AccountHolder).HasMaxLength(PayoutRequest.MaxAccountHolderLength);
        builder.Property(p => p.Rib).HasMaxLength(PayoutRequest.RibLength);
        builder.Property(p => p.Note).HasMaxLength(PayoutRequest.MaxNoteLength);
        builder.Property(p => p.Place).HasMaxLength(PayoutRequest.MaxPlaceLength);
        builder.Property(p => p.PaidReference).HasMaxLength(PayoutRequest.MaxPaidReferenceLength);
        builder.Property(p => p.RejectionReason).HasMaxLength(PayoutRequest.MaxReasonLength);

        builder.HasOne<User>().WithMany().HasForeignKey(p => p.AffiliateId).OnDelete(DeleteBehavior.Restrict);
        // One open request per affiliate — the database backs up the service check.
        builder.HasIndex(p => p.AffiliateId).IsUnique()
            .HasFilter($"status IN ({(int)PayoutStatus.Requested}, {(int)PayoutStatus.Verified}, {(int)PayoutStatus.Scheduled})")
            .HasDatabaseName("ix_payout_requests_affiliate_id_open");
        builder.HasIndex(p => new { p.AffiliateId, p.CreatedAt });
        builder.HasIndex(p => new { p.Status, p.ScheduledFor });

        builder.HasMany(p => p.History).WithOne().HasForeignKey("PayoutRequestId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Two admins (or the admin and the affiliate) moving the same request at once: the second save fails —
        // PostgreSQL xmin as concurrency token, no extra column.
        builder.Property<uint>("Version").IsRowVersion();
        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class PayoutEventConfiguration : IEntityTypeConfiguration<PayoutEvent>
{
    public void Configure(EntityTypeBuilder<PayoutEvent> builder)
    {
        builder.ToTable("payout_events");
        builder.HasKey(e => e.Id);
        builder.Property<Guid>("PayoutRequestId");
        builder.HasIndex("PayoutRequestId", nameof(PayoutEvent.At));
        builder.Property(e => e.Note).HasMaxLength(PayoutRequest.MaxNoteLength);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
