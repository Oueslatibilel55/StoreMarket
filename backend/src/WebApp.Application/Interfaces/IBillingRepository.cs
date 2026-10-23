using WebApp.Application.DTOs;
using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

/// <summary>Writes of store billing (025): offers, commission invoices, store payments.</summary>
public interface IBillingRepository
{
    Task<long> NextInvoiceNumberAsync(CancellationToken ct = default);
    Task<long> NextPaymentNumberAsync(CancellationToken ct = default);

    Task<BillingOffer?> GetOfferAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BillingOffer>> ActiveOffersAsync(CancellationToken ct = default);
    void Add(BillingOffer offer);

    Task<CommissionInvoice?> GetInvoiceAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CommissionInvoice>> UnpaidInvoicesAsync(Guid storeId, CancellationToken ct = default);
    Task<bool> InvoiceExistsAsync(Guid storeId, DateOnly month, CancellationToken ct = default);
    void Add(CommissionInvoice invoice);

    /// <summary>
    /// The store's ledger entries not billed yet and credited before <paramref name="before"/>: its <c>Earned</c> entries and
    /// the adjustments of them, oldest first, with what an invoice line freezes.
    /// </summary>
    Task<IReadOnlyList<CommissionLineInput>> UnbilledLinesAsync(Guid storeId, DateTime before, CancellationToken ct = default);

    Task<StorePayment?> GetPaymentAsync(Guid id, CancellationToken ct = default);
    /// <summary>Payments of the store waiting for an admin.</summary>
    Task<IReadOnlyList<StorePayment>> DeclaredPaymentsAsync(Guid storeId, CancellationToken ct = default);
    /// <summary>True once the store has an approved subscription payment (a "first payment only" offer no longer applies).</summary>
    Task<bool> HasPaidSubscriptionAsync(Guid storeId, CancellationToken ct = default);
    void Add(StorePayment payment);

    /// <summary>Stores billing looks at: not deleted.</summary>
    Task<IReadOnlyList<Guid>> StoreIdsAsync(CancellationToken ct = default);
}

/// <summary>Reads of store billing (025) for the admin and the store owner (projections, no tracking).</summary>
public interface IBillingQueries
{
    /// <summary>The store's whole statement; null when the store doesn't exist.</summary>
    Task<StoreBillingDto?> StoreAsync(Guid storeId, DateOnly today, int graceDays, CancellationToken ct = default);
    Task<BillingStatusDto?> StatusAsync(Guid storeId, DateOnly today, int graceDays, CancellationToken ct = default);
    /// <summary>An invoice with its lines and payments; with <paramref name="storeId"/>, only if it belongs to that store.</summary>
    Task<InvoiceDetailDto?> InvoiceAsync(Guid invoiceId, Guid? storeId, DateOnly today, int graceDays, CancellationToken ct = default);
    Task<StorePaymentDto?> PaymentAsync(Guid paymentId, CancellationToken ct = default);
    Task<PagedResult<StorePaymentDto>> PaymentsAsync(StorePaymentQuery query, CancellationToken ct = default);
    /// <summary>Stores with something late (in grace or overdue), waiting for approval, or suspended.</summary>
    Task<IReadOnlyList<OverdueStoreItem>> OverdueAsync(DateOnly today, int graceDays, CancellationToken ct = default);
    Task<IReadOnlyList<OfferDto>> OffersAsync(CancellationToken ct = default);
    Task<BillingRevenueDto> RevenueAsync(StatsPeriod period, CancellationToken ct = default);
}
