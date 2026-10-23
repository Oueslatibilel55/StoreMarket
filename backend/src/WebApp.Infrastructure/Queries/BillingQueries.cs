using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Queries;

/// <summary>
/// Store billing reads (025). The "late / closes in N days / overdue" state always comes from
/// <see cref="BillingRules.Evaluate"/> over the same dues the billing service checks, so the banner and the suspension agree.
/// </summary>
internal sealed class BillingQueries(AppDbContext db) : IBillingQueries
{
    private sealed record InvoiceRow(Guid Id, Guid StoreId, string Reference, DateOnly Month, DateOnly DueOn, decimal Total, decimal PaidAmount,
        CommissionInvoiceStatus Status, int LineCount);

    /// <summary>Filter first: a positional record projection can't be filtered in SQL afterwards.</summary>
    private IQueryable<InvoiceRow> Invoices(Expression<Func<CommissionInvoice, bool>> filter) =>
        db.CommissionInvoices.AsNoTracking().Where(filter).Select(i => new InvoiceRow(i.Id, i.StoreId, i.Reference, i.Month, i.DueOn, i.Total, i.PaidAmount,
            i.Status, i.Lines.Count));

    public async Task<StoreBillingDto?> StoreAsync(Guid storeId, DateOnly today, int graceDays, CancellationToken ct = default)
    {
        var store = await db.Stores.AsNoTracking().Where(s => s.Id == storeId)
            .Select(s => new { s.Id, s.Name, s.Status, s.SubscriptionMonthlyPrice, s.PaidUntil, s.CategoryId, s.CreatedAt })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (store is null) return null;

        var invoices = (await Invoices(i => i.StoreId == storeId).ToListAsync(ct).ConfigureAwait(false)).OrderByDescending(i => i.Month).ToList();
        var payments = await PaymentDtos(db.StorePayments.AsNoTracking().Where(p => p.StoreId == storeId).OrderByDescending(p => p.CreatedAt))
            .ToListAsync(ct).ConfigureAwait(false);
        var state = Status(store.Status, store.PaidUntil, invoices, payments, today, graceDays);

        // Commissions credited but not on an invoice yet (this month, or a month the job hasn't reached).
        var billed = db.Set<CommissionInvoiceLine>().Select(l => l.CoinEntryId);
        var unbilled = await db.CoinEntries.AsNoTracking()
            .Where(e => e.Type == CoinEntryType.Earned && e.StoreId == storeId && !billed.Contains(e.Id))
            .GroupBy(_ => 1).Select(g => new { Count = g.Count(), Amount = g.Sum(e => e.Coins * (e.CoinValueInTnd ?? 0)) })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var current = new CurrentMonthDto(new DateOnly(today.Year, today.Month, 1), unbilled?.Count ?? 0, decimal.Round(unbilled?.Amount ?? 0, 3));

        // One row per month since the store opened, newest first.
        var approved = payments.Where(p => p.Status == StorePaymentStatus.Approved).ToList();
        var months = new List<MonthStatementRow>();
        for (var m = new DateOnly(today.Year, today.Month, 1); m >= new DateOnly(store.CreatedAt.Year, store.CreatedAt.Month, 1); m = m.AddMonths(-1))
        {
            var month = m;
            var subscriptions = approved.Where(p => p.Kind == StorePaymentKind.Subscription && p.PaidOn.Year == month.Year && p.PaidOn.Month == month.Month).ToList();
            var invoice = invoices.FirstOrDefault(i => i.Month == month);
            months.Add(new MonthStatementRow(month, subscriptions.Sum(p => p.Amount), subscriptions.Sum(p => p.Months ?? 0),
                invoice?.Id, invoice?.Reference, invoice?.Total ?? 0, invoice?.PaidAmount ?? 0, invoice?.Status));
        }

        var offers = (await OffersAsync(ct).ConfigureAwait(false))
            .Where(o => o.IsActive && (o.EndsOn is null || o.EndsOn >= today) && o.Target switch
            {
                OfferTarget.StoreCategories => o.TargetIds.Contains(store.CategoryId),
                OfferTarget.Stores => o.TargetIds.Contains(store.Id),
                _ => true,
            })
            .ToList();

        return new StoreBillingDto(store.Id, store.Name, store.Status, store.SubscriptionMonthlyPrice, graceDays, state, current, months,
            [.. invoices.Select(i => Summary(i, payments, today, graceDays))], payments, offers);
    }

    public async Task<BillingStatusDto?> StatusAsync(Guid storeId, DateOnly today, int graceDays, CancellationToken ct = default)
    {
        var store = await db.Stores.AsNoTracking().Where(s => s.Id == storeId).Select(s => new { s.Status, s.PaidUntil })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (store is null) return null;
        var invoices = await Invoices(i => i.StoreId == storeId && i.Status != CommissionInvoiceStatus.Paid).ToListAsync(ct).ConfigureAwait(false);
        var payments = await PaymentDtos(db.StorePayments.AsNoTracking()
                .Where(p => p.StoreId == storeId && (p.Status == StorePaymentStatus.Declared || p.Status == StorePaymentStatus.Rejected)))
            .ToListAsync(ct).ConfigureAwait(false);
        return Status(store.Status, store.PaidUntil, invoices, payments, today, graceDays);
    }

    public async Task<InvoiceDetailDto?> InvoiceAsync(Guid invoiceId, Guid? storeId, DateOnly today, int graceDays, CancellationToken ct = default)
    {
        var invoice = await Invoices(i => i.Id == invoiceId && (storeId == null || i.StoreId == storeId))
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (invoice is null) return null;
        var head = await db.CommissionInvoices.AsNoTracking().Where(i => i.Id == invoiceId)
            .Join(db.Stores, i => i.StoreId, s => s.Id, (i, s) => new { i.StoreName, i.CreatedAt, s.Address, s.City })
            .FirstAsync(ct).ConfigureAwait(false);
        var lines = await db.Set<CommissionInvoiceLine>().AsNoTracking()
            .Where(l => EF.Property<Guid>(l, Persistence.Configurations.CommissionInvoiceConfiguration.InvoiceKey) == invoiceId)
            .OrderBy(l => l.CreditedAt).ThenBy(l => l.CoinEntryId)
            .Select(l => new InvoiceLineDto(l.CoinEntryId, l.Type, l.CreditedAt, l.OrderReference, l.ProductName, l.Quantity, l.LineTotal,
                l.AffiliatePercent, l.AffiliateCode, l.Coins, l.CoinValueInTnd, l.Amount))
            .ToListAsync(ct).ConfigureAwait(false);
        var payments = await PaymentDtos(db.StorePayments.AsNoTracking().Where(p => p.InvoiceId == invoiceId).OrderByDescending(p => p.CreatedAt))
            .ToListAsync(ct).ConfigureAwait(false);

        return new InvoiceDetailDto(Summary(invoice, payments, today, graceDays), invoice.StoreId, head.StoreName, head.Address, head.City,
            head.CreatedAt, lines, payments);
    }

    public Task<StorePaymentDto?> PaymentAsync(Guid paymentId, CancellationToken ct = default) =>
        PaymentDtos(db.StorePayments.AsNoTracking().Where(p => p.Id == paymentId)).FirstOrDefaultAsync(ct);

    public async Task<PagedResult<StorePaymentDto>> PaymentsAsync(StorePaymentQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var payments = db.StorePayments.AsNoTracking();
        if (query.Status is { } status) payments = payments.Where(p => p.Status == status);
        if (query.Kind is { } kind) payments = payments.Where(p => p.Kind == kind);
        if (query.StoreId is { } storeId) payments = payments.Where(p => p.StoreId == storeId);

        var total = await payments.CountAsync(ct).ConfigureAwait(false);
        // Declarations waiting the longest come first in the queue; otherwise newest first.
        var ordered = query.Status == StorePaymentStatus.Declared
            ? payments.OrderBy(p => p.CreatedAt)
            : payments.OrderByDescending(p => p.CreatedAt);
        var items = await PaymentDtos(ordered.ThenBy(p => p.Id).Skip(paging.Skip).Take(paging.PageSize)).ToListAsync(ct).ConfigureAwait(false);
        return new PagedResult<StorePaymentDto>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<IReadOnlyList<OverdueStoreItem>> OverdueAsync(DateOnly today, int graceDays, CancellationToken ct = default)
    {
        var stores = await db.Stores.AsNoTracking().Where(s => s.DeletedAt == null)
            .Select(s => new { s.Id, s.Name, s.Slug, s.Status, s.PaidUntil }).ToListAsync(ct).ConfigureAwait(false);
        var invoices = (await Invoices(i => i.Status != CommissionInvoiceStatus.Paid).ToListAsync(ct).ConfigureAwait(false))
            .ToLookup(i => i.StoreId);
        var payments = (await PaymentDtos(db.StorePayments.AsNoTracking()
                .Where(p => p.Status == StorePaymentStatus.Declared || p.Status == StorePaymentStatus.Rejected))
            .ToListAsync(ct).ConfigureAwait(false)).ToLookup(p => p.StoreId);

        return [.. stores
            .Select(s =>
            {
                var state = Status(s.Status, s.PaidUntil, [.. invoices[s.Id]], [.. payments[s.Id]], today, graceDays);
                return new OverdueStoreItem(s.Id, s.Name, s.Slug, s.Status, s.PaidUntil, state.CommissionsDue, state.Late, state.DaysLeft,
                    state.Overdue, state.WaitingForApproval);
            })
            .Where(i => i.Late || i.Status == StoreStatus.Suspended || i.WaitingForApproval)
            .OrderByDescending(i => i.Status == StoreStatus.Suspended).ThenByDescending(i => i.Overdue)
            .ThenBy(i => i.DaysLeft ?? int.MaxValue).ThenBy(i => i.StoreName, StringComparer.CurrentCulture)];
    }

    public async Task<IReadOnlyList<OfferDto>> OffersAsync(CancellationToken ct = default)
    {
        var usage = await db.StorePayments.AsNoTracking()
            .Where(p => p.OfferId != null && p.Status == StorePaymentStatus.Approved)
            .GroupBy(p => p.OfferId!.Value).Select(g => new { Id = g.Key, Count = g.Count(), Discount = g.Sum(p => p.Discount) })
            .ToDictionaryAsync(x => x.Id, ct).ConfigureAwait(false);
        var offers = await db.BillingOffers.AsNoTracking().OrderByDescending(o => o.IsActive).ThenBy(o => o.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
        return [.. offers.Select(o => new OfferDto(o.Id, new LocalizedName(o.NameFr, o.NameAr, o.NameEn), o.Months, o.DiscountPercent,
            o.DiscountAmount, o.StartsOn, o.EndsOn, o.Target, o.TargetIds, o.FirstPaymentOnly, o.IsActive,
            usage.GetValueOrDefault(o.Id)?.Count ?? 0, usage.GetValueOrDefault(o.Id)?.Discount ?? 0))];
    }

    public async Task<BillingRevenueDto> RevenueAsync(StatsPeriod period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);
        var first = new DateOnly(period.From.Year, period.From.Month, 1);
        var collected = await db.StorePayments.AsNoTracking()
            .Where(p => p.Status == StorePaymentStatus.Approved && p.PaidOn >= first && p.PaidOn <= period.To)
            .GroupBy(p => new { p.Kind, p.PaidOn.Year, p.PaidOn.Month })
            .Select(g => new { g.Key.Kind, g.Key.Year, g.Key.Month, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(ct).ConfigureAwait(false);
        var billed = await db.CommissionInvoices.AsNoTracking()
            .Where(i => i.Month >= first && i.Month <= period.To)
            .GroupBy(i => i.Month).Select(g => new { Month = g.Key, Total = g.Sum(i => i.Total) })
            .ToDictionaryAsync(x => x.Month, x => x.Total, ct).ConfigureAwait(false);

        decimal Collected(StorePaymentKind kind, DateOnly m) =>
            collected.Where(c => c.Kind == kind && c.Year == m.Year && c.Month == m.Month).Sum(c => c.Amount);
        var months = new List<RevenueMonth>();
        for (var m = first; m <= period.To; m = m.AddMonths(1))
            months.Add(new RevenueMonth(m, Collected(StorePaymentKind.Subscription, m), billed.GetValueOrDefault(m), Collected(StorePaymentKind.Commission, m)));
        return new BillingRevenueDto(period, months, await OffersAsync(ct).ConfigureAwait(false));
    }

    // -- Shared ---------------------------------------------------------------------

    private IQueryable<StorePaymentDto> PaymentDtos(IQueryable<StorePayment> payments) =>
        payments.Select(p => new StorePaymentDto(
            p.Id, p.Number, p.StoreId, db.Stores.Where(s => s.Id == p.StoreId).Select(s => s.Name).First(), p.Kind, p.Status, p.Amount,
            p.Method, p.Reference, p.Note, p.PaidOn,
            db.Images.Where(i => i.Id == p.ReceiptImageId).Select(i => ImageUrls.RequestPath + "/" + i.Path).FirstOrDefault(),
            p.Months, p.MonthlyPrice, p.OfferName, p.Discount, p.CoversFrom, p.CoversTo,
            p.InvoiceId, db.CommissionInvoices.Where(i => i.Id == p.InvoiceId).Select(i => i.Reference).FirstOrDefault(),
            p.DeclaredById != null, p.CreatedAt, p.DecidedAt, p.RejectionReason));

    private static InvoiceSummaryDto Summary(InvoiceRow i, IReadOnlyList<StorePaymentDto> payments, DateOnly today, int graceDays) =>
        new(i.Id, i.Reference, i.Month, i.DueOn, i.Total, i.PaidAmount, i.Status, i.LineCount,
            payments.Any(p => p.InvoiceId == i.Id && p.Status == StorePaymentStatus.Declared),
            i.Status != CommissionInvoiceStatus.Paid && today > i.DueOn.AddDays(graceDays));

    /// <summary>Same dues as <c>BillingService.RecheckAsync</c>: the subscription's paid-until and every unpaid invoice.</summary>
    private static BillingStatusDto Status(StoreStatus status, DateOnly paidUntil, IReadOnlyList<InvoiceRow> invoices,
        IReadOnlyList<StorePaymentDto> payments, DateOnly today, int graceDays)
    {
        var declared = payments.Where(p => p.Status == StorePaymentStatus.Declared).ToList();
        var unpaid = invoices.Where(i => i.Status != CommissionInvoiceStatus.Paid).ToList();
        var dues = unpaid
            .Select(i => new BillingDue(i.DueOn, false, declared.Any(p => p.InvoiceId == i.Id)))
            .Prepend(new BillingDue(paidUntil, false, declared.Any(p => p.Kind == StorePaymentKind.Subscription)));
        var state = BillingRules.Evaluate(dues, today, graceDays);

        var nextDue = unpaid.Select(i => (DateOnly?)i.DueOn).Append(paidUntil).Min();
        // The last decision is a rejection: the owner sees why (until they declare again or the admin approves another payment).
        var last = payments.Where(p => p.DecidedAt != null).OrderByDescending(p => p.DecidedAt).FirstOrDefault();
        var rejection = last?.Status == StorePaymentStatus.Rejected && declared.Count == 0 ? last.RejectionReason : null;

        // "Waiting for approval" shows for any pending declaration, late or not.
        return new BillingStatusDto(state.Late, state.DaysLeft, state.Overdue, declared.Count > 0, status == StoreStatus.Suspended,
            paidUntil, unpaid.Sum(i => i.Total - i.PaidAmount), nextDue, rejection);
    }
}
