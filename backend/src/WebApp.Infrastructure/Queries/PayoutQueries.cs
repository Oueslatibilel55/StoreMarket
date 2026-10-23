using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;
using WebApp.Infrastructure.Persistence.Configurations;

namespace WebApp.Infrastructure.Queries;

/// <summary>Payout views (021): the affiliate's requests, the admin list and the verification screen.</summary>
internal sealed class PayoutQueries(AppDbContext db) : IPayoutQueries
{
    private static readonly PayoutStatus[] Open = [PayoutStatus.Requested, PayoutStatus.Verified, PayoutStatus.Scheduled];
    private static readonly PayoutStatus[] Ended = [PayoutStatus.Rejected, PayoutStatus.Cancelled];

    public async Task<PayoutDto?> GetAsync(Guid id, bool adminView, CancellationToken ct = default)
    {
        var payout = await db.PayoutRequests.AsNoTracking().Include(p => p.History).FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
        return payout is null ? null : (await MapAsync([payout], adminView, ct).ConfigureAwait(false))[0];
    }

    public async Task<(IReadOnlyList<PayoutDto> Items, PayoutBankDetails? LastBankDetails)> MineAsync(Guid affiliateId, CancellationToken ct = default)
    {
        var payouts = await db.PayoutRequests.AsNoTracking().Include(p => p.History)
            .Where(p => p.AffiliateId == affiliateId)
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        var bank = payouts.FirstOrDefault(p => p.Method == PayoutMethod.BankTransfer && p.Rib is not null);
        return (await MapAsync(payouts, adminView: false, ct).ConfigureAwait(false),
            bank is null ? null : new PayoutBankDetails(bank.BankName!, bank.AccountHolder!, bank.Rib!));
    }

    public async Task<PagedResult<PayoutListItem>> ListAsync(PayoutQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var rows = db.PayoutRequests.AsNoTracking()
            .Join(db.Users, p => p.AffiliateId, u => u.Id, (p, u) => new { p, u.FullName, u.Email, u.AffiliateCode });

        if (query.Status is { } status) rows = rows.Where(x => x.p.Status == status);
        if (query.Method is { } method) rows = rows.Where(x => x.p.Method == method);
        if (query.AffiliateId is { } affiliateId) rows = rows.Where(x => x.p.AffiliateId == affiliateId);
        if (query.ScheduledFrom is { } from)
        {
            var start = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
            rows = rows.Where(x => x.p.ScheduledFor >= start);
        }
        if (query.ScheduledTo is { } to)
        {
            var end = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);
            rows = rows.Where(x => x.p.ScheduledFor < end);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{EscapeLike(query.Search.Trim().TrimStart('#'))}%";
            rows = rows.Where(x => EF.Functions.ILike(x.p.Reference, term, "\\") || EF.Functions.ILike(x.FullName, term, "\\")
                || EF.Functions.ILike(x.Email, term, "\\") || EF.Functions.ILike(x.AffiliateCode ?? "", term, "\\"));
        }

        var total = await rows.CountAsync(ct).ConfigureAwait(false);
        // Planned ones first by date when filtering on dates; otherwise newest first.
        var ordered = query.ScheduledFrom is not null || query.ScheduledTo is not null
            ? rows.OrderBy(x => x.p.ScheduledFor).ThenBy(x => x.p.Id)
            : rows.OrderByDescending(x => x.p.CreatedAt).ThenByDescending(x => x.p.Id);
        var items = await ordered.Skip(paging.Skip).Take(paging.PageSize)
            .Select(x => new PayoutListItem(x.p.Id, x.p.Reference, x.p.CreatedAt, x.p.AffiliateId, x.FullName, x.AffiliateCode,
                x.p.Coins, x.p.AmountTnd, x.p.Method, x.p.Status, x.p.ScheduledFor, x.p.Place, x.p.PaidAt))
            .ToListAsync(ct).ConfigureAwait(false);
        return new PagedResult<PayoutListItem>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<AdminPayoutCounts> CountsAsync(CancellationToken ct = default)
    {
        var groups = await db.PayoutRequests.AsNoTracking().Where(p => Open.Contains(p.Status))
            .GroupBy(p => p.Status).Select(g => new { Status = g.Key, Count = g.Count(), Coins = g.Sum(p => p.Coins) })
            .ToListAsync(ct).ConfigureAwait(false);
        int Count(PayoutStatus s) => groups.Where(g => g.Status == s).Sum(g => g.Count);
        return new AdminPayoutCounts(Count(PayoutStatus.Requested), Count(PayoutStatus.Verified), Count(PayoutStatus.Scheduled), groups.Sum(g => g.Coins));
    }

    public async Task<AdminPayoutDetail?> DetailAsync(Guid id, CancellationToken ct = default)
    {
        var payout = await db.PayoutRequests.AsNoTracking().Include(p => p.History).FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
        if (payout is null) return null;
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == payout.AffiliateId, ct).ConfigureAwait(false);
        var account = await db.CoinAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.AffiliateId == user.Id, ct).ConfigureAwait(false);
        var affiliate = new PayoutAffiliate(user.Id, user.FullName, user.Email, user.Phone, user.AffiliateCode, user.IsActive, user.CanEarnAsAffiliate,
            account?.Balance ?? 0, account?.TotalEarned ?? 0);

        var others = (await ListAsync(new PayoutQuery(null, null, null, null, null, user.Id, 1, 50), ct).ConfigureAwait(false))
            .Items.Where(p => p.Id != payout.Id).ToList();
        var adjustments = await db.CoinEntries.AsNoTracking()
            .Where(e => e.AffiliateId == user.Id && e.Type == CoinEntryType.Adjustment)
            .SumAsync(e => e.Coins, ct).ConfigureAwait(false);

        return new AdminPayoutDetail((await MapAsync([payout], adminView: true, ct).ConfigureAwait(false))[0], affiliate,
            await CoveredAsync(payout, ct).ConfigureAwait(false), adjustments, others);
    }

    /// <summary>
    /// The earned entries this request pays, oldest coins first: earlier requests that were not rejected or cancelled
    /// took the first coins, this one takes the next <c>Coins</c>. The first and last entries can be partly covered.
    /// </summary>
    private async Task<List<PayoutCoveredEntry>> CoveredAsync(PayoutRequest payout, CancellationToken ct)
    {
        var before = await db.PayoutRequests.AsNoTracking()
            .Where(p => p.AffiliateId == payout.AffiliateId && p.Id != payout.Id && !Ended.Contains(p.Status) && p.CreatedAt < payout.CreatedAt)
            .SumAsync(p => p.Coins, ct).ConfigureAwait(false);
        var earned = await db.CoinEntries.AsNoTracking()
            .Where(e => e.AffiliateId == payout.AffiliateId && e.Type == CoinEntryType.Earned)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Id, e.CreatedAt, e.OrderReference, e.StoreId, e.StoreName, e.ProductName, e.Coins,
                // The store invoice that billed this commission (025): the ledger itself is never written.
                InvoiceId = db.Set<CommissionInvoiceLine>().Where(l => l.CoinEntryId == e.Id)
                    .Select(l => (Guid?)EF.Property<Guid>(l, CommissionInvoiceConfiguration.InvoiceKey)).FirstOrDefault(),
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var covered = new List<PayoutCoveredEntry>();
        decimal start = 0, windowStart = before, windowEnd = before + payout.Coins;
        foreach (var e in earned)
        {
            var end = start + e.Coins;
            var part = Math.Min(end, windowEnd) - Math.Max(start, windowStart);
            if (part > 0) covered.Add(new PayoutCoveredEntry(e.Id, e.CreatedAt, e.OrderReference, e.StoreId, e.StoreName, e.ProductName, e.Coins, part, e.InvoiceId));
            start = end;
            if (start >= windowEnd) break;
        }
        return covered;
    }

    private async Task<List<PayoutDto>> MapAsync(IReadOnlyList<PayoutRequest> payouts, bool adminView, CancellationToken ct)
    {
        var ids = payouts.Select(p => p.AffiliateId).Concat(payouts.SelectMany(p => p.History.Select(h => h.ActorId))).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.AffiliateCode })
            .ToDictionaryAsync(u => u.Id, ct).ConfigureAwait(false);

        return [.. payouts.Select(p =>
        {
            var affiliate = users.GetValueOrDefault(p.AffiliateId);
            var history = p.History.OrderBy(h => h.At).ThenBy(h => h.Id).Select(h => new PayoutEventDto(
                h.Status, h.At, h.ActorId == p.AffiliateId, adminView ? users.GetValueOrDefault(h.ActorId)?.FullName : null, h.Note)).ToList();
            return new PayoutDto(p.Id, p.Reference, p.CreatedAt, p.AffiliateId, affiliate?.FullName ?? "", affiliate?.AffiliateCode,
                p.Coins, p.CoinValueInTnd, p.AmountTnd, p.Method, p.BankName, p.AccountHolder, p.Rib, p.Note, p.Status,
                p.ScheduledFor, p.Place, p.PaidAt, p.PaidReference, p.RejectionReason, history);
        })];
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);
}
