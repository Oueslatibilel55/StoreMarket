using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;
using WebApp.Infrastructure.Persistence.Configurations;

namespace WebApp.Infrastructure.Repositories;

internal sealed class BillingRepository(AppDbContext db) : IBillingRepository
{
    public async Task<long> NextInvoiceNumberAsync(CancellationToken ct = default) =>
        await db.Database
            .SqlQueryRaw<long>($"SELECT nextval('{CommissionInvoiceConfiguration.NumberSequence}') AS \"Value\"")
            .SingleAsync(ct).ConfigureAwait(false);

    public async Task<long> NextPaymentNumberAsync(CancellationToken ct = default) =>
        await db.Database
            .SqlQueryRaw<long>($"SELECT nextval('{StorePaymentConfiguration.NumberSequence}') AS \"Value\"")
            .SingleAsync(ct).ConfigureAwait(false);

    public Task<BillingOffer?> GetOfferAsync(Guid id, CancellationToken ct = default) =>
        db.BillingOffers.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<BillingOffer>> ActiveOffersAsync(CancellationToken ct = default) =>
        await db.BillingOffers.Where(o => o.IsActive).ToListAsync(ct).ConfigureAwait(false);

    public void Add(BillingOffer offer) => db.BillingOffers.Add(offer);

    public Task<CommissionInvoice?> GetInvoiceAsync(Guid id, CancellationToken ct = default) =>
        db.CommissionInvoices.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<CommissionInvoice>> UnpaidInvoicesAsync(Guid storeId, CancellationToken ct = default) =>
        await db.CommissionInvoices.Where(i => i.StoreId == storeId && i.Status != CommissionInvoiceStatus.Paid)
            .ToListAsync(ct).ConfigureAwait(false);

    public Task<bool> InvoiceExistsAsync(Guid storeId, DateOnly month, CancellationToken ct = default) =>
        db.CommissionInvoices.AnyAsync(i => i.StoreId == storeId && i.Month == month, ct);

    public void Add(CommissionInvoice invoice) => db.CommissionInvoices.Add(invoice);

    public async Task<IReadOnlyList<CommissionLineInput>> UnbilledLinesAsync(Guid storeId, DateTime before, CancellationToken ct = default)
    {
        var billed = db.Set<CommissionInvoiceLine>().Select(l => l.CoinEntryId);
        var entries = db.CoinEntries.AsNoTracking();

        var earned = await entries
            .Where(e => e.Type == CoinEntryType.Earned && e.StoreId == storeId && e.CreatedAt < before && !billed.Contains(e.Id))
            .Select(e => new CommissionLineInput(e.Id, e.Type, e.CreatedAt, e.OrderReference, e.ProductName, e.Quantity, e.LineTotal,
                e.AffiliatePercent, db.Users.Where(u => u.Id == e.AffiliateId).Select(u => u.AffiliateCode).FirstOrDefault(),
                e.Coins, e.CoinValueInTnd ?? 0))
            .ToListAsync(ct).ConfigureAwait(false);

        // An admin correction of one of the store's commissions is billed to the store too, at that order's coin value.
        var adjustments = await (
                from a in entries
                where a.Type == CoinEntryType.Adjustment && a.CreatedAt < before && !billed.Contains(a.Id)
                join r in entries on a.RelatedEntryId equals r.Id
                where r.StoreId == storeId
                select new CommissionLineInput(a.Id, a.Type, a.CreatedAt, r.OrderReference, r.ProductName, null, null, r.AffiliatePercent,
                    db.Users.Where(u => u.Id == a.AffiliateId).Select(u => u.AffiliateCode).FirstOrDefault(), a.Coins, r.CoinValueInTnd ?? 0))
            .ToListAsync(ct).ConfigureAwait(false);

        return [.. earned.Concat(adjustments).OrderBy(l => l.CreditedAt).ThenBy(l => l.CoinEntryId)];
    }

    public Task<StorePayment?> GetPaymentAsync(Guid id, CancellationToken ct = default) =>
        db.StorePayments.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<StorePayment>> DeclaredPaymentsAsync(Guid storeId, CancellationToken ct = default) =>
        await db.StorePayments.Where(p => p.StoreId == storeId && p.Status == StorePaymentStatus.Declared).ToListAsync(ct).ConfigureAwait(false);

    public Task<bool> HasPaidSubscriptionAsync(Guid storeId, CancellationToken ct = default) =>
        db.StorePayments.AnyAsync(p => p.StoreId == storeId && p.Kind == StorePaymentKind.Subscription && p.Status == StorePaymentStatus.Approved, ct);

    public void Add(StorePayment payment) => db.StorePayments.Add(payment);

    public async Task<IReadOnlyList<Guid>> StoreIdsAsync(CancellationToken ct = default) =>
        await db.Stores.AsNoTracking().Where(s => s.DeletedAt == null).OrderBy(s => s.CreatedAt).Select(s => s.Id).ToListAsync(ct).ConfigureAwait(false);
}
