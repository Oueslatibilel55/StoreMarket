using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Application.Interfaces;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Queries;

/// <summary>Coin views (019): ledger rows, an affiliate's totals, platform totals.</summary>
internal sealed class CoinQueries(AppDbContext db) : ICoinQueries
{
    private static readonly StoreOrderStatus[] Open = [StoreOrderStatus.Placed, StoreOrderStatus.Confirmed, StoreOrderStatus.Shipped];

    public async Task<CoinSummaryDto> SummaryAsync(Guid affiliateId, decimal coinValueInTnd, CancellationToken ct = default)
    {
        var account = await db.CoinAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.AffiliateId == affiliateId, ct).ConfigureAwait(false);
        var payouts = await db.PayoutRequests.AsNoTracking().Where(p => p.AffiliateId == affiliateId)
            .GroupBy(p => p.Status).Select(g => new { Status = g.Key, Coins = g.Sum(p => p.Coins) })
            .ToListAsync(ct).ConfigureAwait(false);
        return new CoinSummaryDto(account?.Balance ?? 0, account?.TotalEarned ?? 0, await ExpectedAsync(affiliateId, ct).ConfigureAwait(false), coinValueInTnd,
            payouts.Where(p => Domain.Entities.PayoutRequest.IsOpenStatus(p.Status)).Sum(p => p.Coins),
            payouts.Where(p => p.Status == PayoutStatus.Paid).Sum(p => p.Coins));
    }

    public async Task<PagedResult<CoinEntryDto>> ListAsync(CoinEntryQuery query, bool maskPhones, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var entries = Filter(query);
        var total = await entries.CountAsync(ct).ConfigureAwait(false);
        var items = await ProjectAsync(entries.OrderByDescending(e => e.CreatedAt).ThenBy(e => e.Id).Skip(paging.Skip).Take(paging.PageSize), maskPhones, ct)
            .ConfigureAwait(false);
        return new PagedResult<CoinEntryDto>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<IReadOnlyList<CoinEntryDto>> ExportAsync(CoinEntryQuery query, bool maskPhones, int max, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await ProjectAsync(Filter(query).OrderByDescending(e => e.CreatedAt).ThenBy(e => e.Id).Take(max), maskPhones, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CoinBreakdownRow>> BreakdownAsync(Guid affiliateId, CoinBreakdownBy by, DateTime? since, DateTime? until, CancellationToken ct = default)
    {
        var earned = Filter(new CoinEntryQuery(affiliateId, null, CoinEntryType.Earned, null, since, until, null, null));
        var rows = by == CoinBreakdownBy.Store
            ? await earned.Where(e => e.StoreId != null)
                .GroupBy(e => new { Id = e.StoreId!.Value, Name = e.StoreName ?? "" })
                .Select(g => new CoinBreakdownRow(g.Key.Id, g.Key.Name, null, g.Count(), g.Sum(e => e.Quantity ?? 0), g.Sum(e => e.LineTotal ?? 0), g.Sum(e => e.Coins)))
                .ToListAsync(ct).ConfigureAwait(false)
            : await earned.Where(e => e.ProductId != null)
                .GroupBy(e => new { Id = e.ProductId!.Value, Name = e.ProductName ?? "", Store = e.StoreName })
                .Select(g => new CoinBreakdownRow(g.Key.Id, g.Key.Name, g.Key.Store, g.Count(), g.Sum(e => e.Quantity ?? 0), g.Sum(e => e.LineTotal ?? 0), g.Sum(e => e.Coins)))
                .ToListAsync(ct).ConfigureAwait(false);
        return [.. rows.OrderByDescending(r => r.Coins).ThenBy(r => r.Name, StringComparer.CurrentCulture)];
    }

    private IQueryable<Domain.Entities.CoinEntry> Filter(CoinEntryQuery query)
    {
        var entries = db.CoinEntries.AsNoTracking();
        if (query.AffiliateId is { } affiliateId) entries = entries.Where(e => e.AffiliateId == affiliateId);
        if (query.StoreId is { } storeId) entries = entries.Where(e => e.StoreId == storeId);
        if (query.ProductId is { } productId) entries = entries.Where(e => e.ProductId == productId);
        if (query.Type is { } type) entries = entries.Where(e => e.Type == type);
        if (query.From is { } from)
        {
            var start = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
            entries = entries.Where(e => e.CreatedAt >= start);
        }
        if (query.To is { } to)
        {
            var end = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);
            entries = entries.Where(e => e.CreatedAt < end);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{EscapeLike(query.Search.Trim().TrimStart('#'))}%";
            entries = entries.Where(e => EF.Functions.ILike(e.OrderReference ?? "", term, "\\")
                || EF.Functions.ILike(e.ProductName ?? "", term, "\\") || EF.Functions.ILike(e.StoreName ?? "", term, "\\")
                || db.Users.Any(u => u.Id == e.AffiliateId && (EF.Functions.ILike(u.FullName, term, "\\")
                    || EF.Functions.ILike(u.Email, term, "\\") || EF.Functions.ILike(u.AffiliateCode ?? "", term, "\\"))));
        }
        return entries;
    }

    private async Task<List<CoinEntryDto>> ProjectAsync(IQueryable<Domain.Entities.CoinEntry> entries, bool maskPhones, CancellationToken ct)
    {
        var rows = await entries
            .Join(db.Users, e => e.AffiliateId, u => u.Id, (e, u) => new { e, u.FullName, u.AffiliateCode })
            .ToListAsync(ct).ConfigureAwait(false);
        return [.. rows.Select(x => new CoinEntryDto(
            x.e.Id, x.e.CreatedAt, x.e.AffiliateId, x.FullName, x.AffiliateCode, x.e.Type, x.e.Coins, x.e.OrderReference,
            x.e.StoreId, x.e.StoreName, x.e.ProductId, x.e.ProductName, x.e.ClientName,
            maskPhones ? Mask(x.e.ClientPhone) : x.e.ClientPhone,
            x.e.Quantity, x.e.LineTotal, x.e.AffiliatePercent, x.e.CoinValueInTnd, x.e.Reason, x.e.RelatedEntryId))];
    }

    public async Task<AdminCoinTotals> TotalsAsync(CancellationToken ct = default)
    {
        var accounts = await db.CoinAccounts.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new { Balance = g.Sum(a => a.Balance), Earned = g.Sum(a => a.TotalEarned), Count = g.Count() })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var expected = await ExpectedLines().SumAsync(l => l.ExpectedCoins ?? 0, ct).ConfigureAwait(false);
        return new AdminCoinTotals(accounts?.Balance ?? 0, accounts?.Earned ?? 0, expected, accounts?.Count ?? 0);
    }

    public async Task<(decimal Cached, decimal Sum)> CheckBalanceAsync(Guid affiliateId, CancellationToken ct = default)
    {
        var cached = await db.CoinAccounts.AsNoTracking().Where(a => a.AffiliateId == affiliateId).Select(a => (decimal?)a.Balance)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false) ?? 0;
        var sum = await db.CoinEntries.AsNoTracking().Where(e => e.AffiliateId == affiliateId).SumAsync(e => e.Coins, ct).ConfigureAwait(false);
        return (cached, sum);
    }

    /// <summary>Expected coins: lines with a code whose parcel is still on its way (computed, never in the ledger).</summary>
    private Task<decimal> ExpectedAsync(Guid affiliateId, CancellationToken ct) =>
        ExpectedLines().Where(l => l.AffiliateId == affiliateId).SumAsync(l => l.ExpectedCoins ?? 0, ct);

    private IQueryable<Domain.Entities.OrderLine> ExpectedLines() =>
        from o in db.Orders.AsNoTracking()
        from s in o.StoreOrders
        where Open.Contains(s.Status)
        from l in s.Lines
        where l.AffiliateId != null
        select l;

    /// <summary>"+21650112233" → "+216 •• ••• 233": enough to recognise a client, not to call them.</summary>
    private static string? Mask(string? phone) =>
        phone is { Length: >= 3 } ? $"+216 •• ••• {phone[^3..]}" : phone;

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);
}
