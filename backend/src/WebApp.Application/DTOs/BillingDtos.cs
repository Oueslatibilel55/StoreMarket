using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

// -- Requests -------------------------------------------------------------------

public sealed record SetSubscriptionPriceRequest(decimal MonthlyPrice, string? Reason);

public sealed record SaveOfferRequest(
    string? NameFr, string? NameAr, string? NameEn, int? Months, int? DiscountPercent, decimal? DiscountAmount,
    DateOnly? StartsOn, DateOnly? EndsOn, OfferTarget Target, IReadOnlyList<Guid>? TargetIds, bool FirstPaymentOnly, bool IsActive)
{
    public OfferDetails ToDetails() =>
        new(NameFr, NameAr, NameEn, Months, DiscountPercent, DiscountAmount, StartsOn, EndsOn, Target, TargetIds, FirstPaymentOnly, IsActive);
}

/// <summary>An admin records months of subscription paid in person. No offer id = no discount.</summary>
public sealed record RecordSubscriptionPaymentRequest(int Months, Guid? OfferId, StorePaymentMethod Method, string? Reference, string? Note, DateOnly? PaidOn);

/// <summary>An admin records a payment of (part of) a commission invoice.</summary>
public sealed record RecordInvoicePaymentRequest(decimal Amount, StorePaymentMethod Method, string? Reference, string? Note, DateOnly? PaidOn);

/// <summary>
/// The owner says "I paid": <c>Months</c> of subscription (the best offer applies), or <c>Amount</c> of an invoice.
/// </summary>
public sealed record DeclarePaymentRequest(
    StorePaymentKind Kind, int? Months, Guid? InvoiceId, decimal? Amount, StorePaymentMethod Method, string? Reference, string? Note,
    DateOnly? PaidOn, Guid? ReceiptImageId);

public sealed record RejectStorePaymentRequest(string? Reason);

public sealed record StorePaymentQuery(StorePaymentStatus? Status, StorePaymentKind? Kind, Guid? StoreId, int? Page, int? PageSize);

// -- Read models -----------------------------------------------------------------

public sealed record OfferDto(
    Guid Id, LocalizedName Name, int? Months, int? DiscountPercent, decimal? DiscountAmount, DateOnly? StartsOn, DateOnly? EndsOn,
    OfferTarget Target, IReadOnlyList<Guid> TargetIds, bool FirstPaymentOnly, bool IsActive, int TimesUsed, decimal TotalDiscount);

public sealed record QuotedOffer(Guid Id, LocalizedName Name, decimal Discount, decimal Amount);

/// <summary>What N months cost this store today, and every offer that applies (best first).</summary>
public sealed record SubscriptionQuote(int Months, decimal MonthlyPrice, decimal Gross, IReadOnlyList<QuotedOffer> Offers);

public sealed record StorePaymentDto(
    Guid Id, string Number, Guid StoreId, string StoreName, StorePaymentKind Kind, StorePaymentStatus Status, decimal Amount,
    StorePaymentMethod Method, string? Reference, string? Note, DateOnly PaidOn, string? ReceiptUrl,
    int? Months, decimal? MonthlyPrice, string? OfferName, decimal Discount, DateOnly? CoversFrom, DateOnly? CoversTo,
    Guid? InvoiceId, string? InvoiceReference, bool DeclaredByOwner, DateTime CreatedAt, DateTime? DecidedAt, string? RejectionReason);

public sealed record InvoiceSummaryDto(
    Guid Id, string Reference, DateOnly Month, DateOnly DueOn, decimal Total, decimal PaidAmount, CommissionInvoiceStatus Status,
    int LineCount, bool PaymentDeclared, bool Overdue);

public sealed record InvoiceLineDto(
    Guid CoinEntryId, CoinEntryType Type, DateTime CreditedAt, string? OrderReference, string? ProductName, int? Quantity,
    decimal? LineTotal, int? AffiliatePercent, string? AffiliateCode, decimal Coins, decimal CoinValueInTnd, decimal Amount);

public sealed record InvoiceDetailDto(
    InvoiceSummaryDto Invoice, Guid StoreId, string StoreName, string? StoreAddress, string StoreCity, DateTime IssuedAt,
    IReadOnlyList<InvoiceLineDto> Lines, IReadOnlyList<StorePaymentDto> Payments);

/// <summary>
/// The owner's banner and the admin's at-a-glance state: something late (and the grace days left), overdue (suspension
/// due), waiting for an admin, suspended; what is owed now and the next deadline; the last rejection reason.
/// </summary>
public sealed record BillingStatusDto(
    bool Late, int? DaysLeft, bool Overdue, bool WaitingForApproval, bool Suspended, DateOnly PaidUntil,
    decimal CommissionsDue, DateOnly? NextDue, string? LastRejectionReason);

/// <summary>Commissions of the current month, not invoiced yet.</summary>
public sealed record CurrentMonthDto(DateOnly Month, int Lines, decimal Amount);

/// <summary>
/// One month of the store's statement: subscription paid that month (approved payments by payment date) and the
/// commission invoice of that month (billed / paid / left).
/// </summary>
public sealed record MonthStatementRow(
    DateOnly Month, decimal SubscriptionPaid, int SubscriptionMonthsPaid,
    Guid? InvoiceId, string? InvoiceReference, decimal CommissionsBilled, decimal CommissionsPaid, CommissionInvoiceStatus? InvoiceStatus);

public sealed record StoreBillingDto(
    Guid StoreId, string StoreName, StoreStatus Status, decimal MonthlyPrice, int GraceDays, BillingStatusDto State,
    CurrentMonthDto CurrentMonth, IReadOnlyList<MonthStatementRow> Months, IReadOnlyList<InvoiceSummaryDto> Invoices,
    IReadOnlyList<StorePaymentDto> Payments, IReadOnlyList<OfferDto> Offers);

/// <summary>A store that owes something, for the admin's overdue list.</summary>
public sealed record OverdueStoreItem(
    Guid StoreId, string StoreName, string StoreSlug, StoreStatus Status, DateOnly PaidUntil, decimal CommissionsDue,
    bool Late, int? DaysLeft, bool Overdue, bool WaitingForApproval);

public sealed record RevenueMonth(DateOnly Month, decimal Subscriptions, decimal CommissionsBilled, decimal CommissionsCollected);

public sealed record BillingRevenueDto(StatsPeriod Period, IReadOnlyList<RevenueMonth> Months, IReadOnlyList<OfferDto> Offers);

/// <summary>What one run of the billing checks did (the hourly job, or the admin's "run now").</summary>
public sealed record BillingRunResult(int InvoicesIssued, int StoresSuspended, int StoresReactivated);
