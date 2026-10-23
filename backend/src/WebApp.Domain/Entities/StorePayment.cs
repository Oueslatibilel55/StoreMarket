using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Domain.Entities;

/// <summary>How and when a store paid, and an optional note — shared by every kind of payment.</summary>
public sealed record PaymentDetails(StorePaymentMethod Method, string? Reference, string? Note, DateOnly PaidOn, Guid? ReceiptImageId);

/// <summary>
/// Money a store gives the platform (025): months of subscription or (part of) a commission invoice. The owner declares
/// it ("I paid", optional receipt photo) and an admin approves or rejects it; an admin can also record a payment received
/// in person, approved at once. Only an approved payment counts — the caller applies it (paid-until, invoice) in the same
/// transaction. Prices, offer and discount are frozen here: a later price or offer change never rewrites a payment.
/// </summary>
public sealed class StorePayment : AggregateRoot
{
    public const int MaxMonths = 36;
    public const int MaxReferenceLength = 120;
    public const int MaxNoteLength = 500;
    public const int MaxReasonLength = 500;
    public const decimal MaxAmount = 10_000_000m;

    /// <summary><c>PMT-2026-000012</c>.</summary>
    public string Number { get; private set; } = null!;
    public Guid StoreId { get; private set; }
    public StorePaymentKind Kind { get; private set; }
    public StorePaymentStatus Status { get; private set; }
    public decimal Amount { get; private set; }
    public StorePaymentMethod Method { get; private set; }
    /// <summary>Bank reference, receipt number…</summary>
    public string? Reference { get; private set; }
    public string? Note { get; private set; }
    public DateOnly PaidOn { get; private set; }
    public Guid? ReceiptImageId { get; private set; }

    // Subscription
    public int? Months { get; private set; }
    public decimal? MonthlyPrice { get; private set; }
    public Guid? OfferId { get; private set; }
    public string? OfferName { get; private set; }
    public decimal Discount { get; private set; }
    /// <summary>Days covered, set on approval.</summary>
    public DateOnly? CoversFrom { get; private set; }
    public DateOnly? CoversTo { get; private set; }

    // Commission
    public Guid? InvoiceId { get; private set; }

    /// <summary>The owner who declared it; null when an admin recorded it.</summary>
    public Guid? DeclaredById { get; private set; }
    public Guid? DecidedById { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    private StorePayment() { }

    public static string NumberFor(int year, long number) => $"PMT-{year}-{number:D6}";

    public bool IsDeclared => Status == StorePaymentStatus.Declared;
    public bool IsApproved => Status == StorePaymentStatus.Approved;

    /// <summary>
    /// <paramref name="months"/> months at the store's price, less the offer's discount (the caller checked the offer
    /// applies). <paramref name="adminId"/> set = recorded by an admin (approved now), else declared by <paramref name="ownerId"/>.
    /// </summary>
    public static Result<StorePayment> ForSubscription(long number, Store store, int months, BillingOffer? offer, PaymentDetails details,
        Guid? ownerId, Guid? adminId, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (months < 1 || months > MaxMonths) return Result.Fail<StorePayment>(DomainErrors.Billing.MonthsInvalid);
        var gross = months * store.SubscriptionMonthlyPrice;
        var discount = offer?.DiscountOn(gross) ?? 0m;

        var payment = New(number, store.Id, StorePaymentKind.Subscription, gross - discount, details, ownerId, adminId, utcNow, allowZero: true);
        if (!payment.IsSuccess) return payment;
        var p = payment.Value;
        p.Months = months;
        p.MonthlyPrice = store.SubscriptionMonthlyPrice;
        p.OfferId = offer?.Id;
        p.OfferName = offer?.NameFr;
        p.Discount = discount;
        return payment;
    }

    /// <summary>All or part of an invoice; never more than what is left on it.</summary>
    public static Result<StorePayment> ForInvoice(long number, CommissionInvoice invoice, decimal amount, PaymentDetails details,
        Guid? ownerId, Guid? adminId, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        if (invoice.IsPaid) return Result.Fail<StorePayment>(DomainErrors.Billing.InvoicePaid);
        if (amount > invoice.Remaining) return Result.Fail<StorePayment>(DomainErrors.Billing.AmountTooHigh);

        var payment = New(number, invoice.StoreId, StorePaymentKind.Commission, amount, details, ownerId, adminId, utcNow, allowZero: false);
        if (payment.IsSuccess) payment.Value.InvoiceId = invoice.Id;
        return payment;
    }

    private static Result<StorePayment> New(long number, Guid storeId, StorePaymentKind kind, decimal amount, PaymentDetails d,
        Guid? ownerId, Guid? adminId, DateTime utcNow, bool allowZero)
    {
        ArgumentNullException.ThrowIfNull(d);
        if ((allowZero ? amount < 0 : amount <= 0) || amount > MaxAmount || decimal.Round(amount, 3) != amount)
            return Result.Fail<StorePayment>(DomainErrors.Billing.AmountInvalid);
        if (!Enum.IsDefined(d.Method)) return Result.Fail<StorePayment>(DomainErrors.Billing.MethodInvalid);
        var reference = Optional(d.Reference);
        if (reference?.Length > MaxReferenceLength) return Result.Fail<StorePayment>(DomainErrors.Billing.ReferenceTooLong);
        var note = Optional(d.Note);
        if (note?.Length > MaxNoteLength) return Result.Fail<StorePayment>(DomainErrors.Billing.NoteTooLong);

        var byAdmin = adminId is not null;
        return Result.Ok(new StorePayment
        {
            Number = NumberFor(utcNow.Year, number),
            StoreId = storeId,
            Kind = kind,
            Status = byAdmin ? StorePaymentStatus.Approved : StorePaymentStatus.Declared,
            Amount = amount,
            Method = d.Method,
            Reference = reference,
            Note = note,
            PaidOn = d.PaidOn,
            ReceiptImageId = d.ReceiptImageId,
            DeclaredById = byAdmin ? null : ownerId,
            DecidedById = adminId,
            DecidedAt = byAdmin ? utcNow : null,
        });
    }

    /// <summary>An admin checked a declared payment: it counts from now (the caller applies it).</summary>
    public Result Approve(Guid adminId, DateTime utcNow)
    {
        if (!IsDeclared) return Result.Fail(DomainErrors.Billing.NotDeclared);
        Status = StorePaymentStatus.Approved;
        DecidedById = adminId;
        DecidedAt = utcNow;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>Refused with a reason, shown to the owner; it never counts.</summary>
    public Result Reject(Guid adminId, string? reason, DateTime utcNow)
    {
        if (!IsDeclared) return Result.Fail(DomainErrors.Billing.NotDeclared);
        var text = Optional(reason);
        if (text is null) return Result.Fail(DomainErrors.Billing.ReasonRequired);
        if (text.Length > MaxReasonLength) return Result.Fail(DomainErrors.Billing.ReasonTooLong);
        Status = StorePaymentStatus.Rejected;
        RejectionReason = text;
        DecidedById = adminId;
        DecidedAt = utcNow;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>The days an approved subscription payment covers (from <see cref="Store.ExtendSubscription"/>).</summary>
    public void Cover(DateOnly from, DateOnly to)
    {
        CoversFrom = from;
        CoversTo = to;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
