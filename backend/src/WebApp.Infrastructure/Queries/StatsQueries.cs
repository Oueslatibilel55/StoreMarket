using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Queries;

/// <summary>
/// Dashboard numbers (023). Every figure is computed in SQL from the frozen order lines (014) and the ledger (019) —
/// never from current prices or settings. The definitions live here and nowhere else:
/// <list type="bullet">
/// <item><b>orders</b> = store orders placed in the period, split by their current status;</item>
/// <item><b>sales</b> = lines of store orders delivered in the period (items only, no delivery fee);</item>
/// <item><b>coins</b> = <c>Earned</c> ledger entries created in the period; <b>expected</b> = open lines now.</item>
/// </list>
/// </summary>
internal sealed class StatsQueries(AppDbContext db, ICoinQueries coins) : IStatsQueries
{
    private const int OverviewTop = 5;
    private const int DetailTop = 10;

    private static readonly StoreOrderStatus[] Open = [StoreOrderStatus.Placed, StoreOrderStatus.Confirmed, StoreOrderStatus.Shipped];

    /// <summary>One order line with its store order and order: what every order figure is built from.</summary>
    private sealed class LineFact
    {
        public Guid StoreOrderId { get; init; }
        public Guid StoreId { get; init; }
        public string StoreName { get; init; } = "";
        public StoreOrderStatus Status { get; init; }
        public DateTime PlacedAt { get; init; }
        public DateTime? DeliveredAt { get; init; }
        public Guid ProductId { get; init; }
        public string ProductName { get; init; } = "";
        public Guid? AffiliateId { get; init; }
        public int Quantity { get; init; }
        public decimal LineTotal { get; init; }
        public decimal? ExpectedCoins { get; init; }
    }

    private IQueryable<LineFact> Lines() =>
        from o in db.Orders.AsNoTracking()
        from s in o.StoreOrders
        from l in s.Lines
        select new LineFact
        {
            StoreOrderId = s.Id, StoreId = s.StoreId, StoreName = s.StoreName, Status = s.Status,
            PlacedAt = o.CreatedAt, DeliveredAt = s.DeliveredAt,
            ProductId = l.ProductId, ProductName = l.ProductName, AffiliateId = l.AffiliateId,
            Quantity = l.Quantity, LineTotal = l.LineTotal, ExpectedCoins = l.ExpectedCoins,
        };

    private IQueryable<LineFact> Delivered(Expression<Func<LineFact, bool>> scope, StatsPeriod period)
    {
        var (start, end) = (period.Start, period.End);
        return Lines().Where(scope).Where(f => f.Status == StoreOrderStatus.Delivered && f.DeliveredAt >= start && f.DeliveredAt < end);
    }

    private IQueryable<CoinEntry> Earned(Expression<Func<CoinEntry, bool>> scope, StatsPeriod period)
    {
        var (start, end) = (period.Start, period.End);
        return db.CoinEntries.AsNoTracking().Where(scope)
            .Where(e => e.Type == CoinEntryType.Earned && e.CreatedAt >= start && e.CreatedAt < end);
    }

    public async Task<AdminOverviewDto> OverviewAsync(StatsPeriod period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);
        var (start, end) = (period.Start, period.End);
        Expression<Func<LineFact, bool>> all = _ => true;

        var orders = await ConversionAsync(all, period, ct).ConfigureAwait(false);
        var sales = await SalesAsync(all, period, ct).ConfigureAwait(false);

        var ledger = await db.CoinEntries.AsNoTracking()
            .Where(e => e.CreatedAt >= start && e.CreatedAt < end && (e.Type == CoinEntryType.Earned || e.Type == CoinEntryType.Adjustment))
            .GroupBy(e => e.Type).Select(g => new { Type = g.Key, Coins = g.Sum(e => e.Coins) })
            .ToListAsync(ct).ConfigureAwait(false);
        var expected = await Lines().Where(f => f.AffiliateId != null && Open.Contains(f.Status))
            .SumAsync(f => f.ExpectedCoins ?? 0, ct).ConfigureAwait(false);

        var payouts = db.PayoutRequests.AsNoTracking();
        var pending = await payouts
            .Where(p => p.Status == PayoutStatus.Requested || p.Status == PayoutStatus.Verified || p.Status == PayoutStatus.Scheduled)
            .GroupBy(_ => 1).Select(g => new { Count = g.Count(), Coins = g.Sum(p => p.Coins), Tnd = g.Sum(p => p.AmountTnd) })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var paid = await payouts.Where(p => p.Status == PayoutStatus.Paid && p.PaidAt >= start && p.PaidAt < end)
            .GroupBy(_ => 1).Select(g => new { Count = g.Count(), Coins = g.Sum(p => p.Coins), Tnd = g.Sum(p => p.AmountTnd) })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        var stores = await db.Stores.AsNoTracking().Where(s => s.DeletedAt == null)
            .GroupBy(s => s.Status).Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct).ConfigureAwait(false);
        int StoresIn(StoreStatus status) => stores.FirstOrDefault(s => s.Status == status)?.Count ?? 0;

        // Store billing (025): what came in during the period, what was billed for its months, what is owed now.
        var (fromDay, toDay) = (period.From, period.To);
        var collected = await db.StorePayments.AsNoTracking()
            .Where(p => p.Status == StorePaymentStatus.Approved && p.PaidOn >= fromDay && p.PaidOn <= toDay)
            .GroupBy(p => p.Kind).Select(g => new { Kind = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(ct).ConfigureAwait(false);
        var firstMonth = new DateOnly(fromDay.Year, fromDay.Month, 1);
        var billed = await db.CommissionInvoices.AsNoTracking().Where(i => i.Month >= firstMonth && i.Month <= toDay)
            .SumAsync(i => i.Total, ct).ConfigureAwait(false);
        var due = await db.CommissionInvoices.AsNoTracking().Where(i => i.Status != CommissionInvoiceStatus.Paid)
            .SumAsync(i => i.Total - i.PaidAmount, ct).ConfigureAwait(false);
        var declaredPayments = await db.StorePayments.AsNoTracking().CountAsync(p => p.Status == StorePaymentStatus.Declared, ct).ConfigureAwait(false);
        var billingStats = new BillingStats(
            collected.FirstOrDefault(c => c.Kind == StorePaymentKind.Subscription)?.Amount ?? 0, billed,
            collected.FirstOrDefault(c => c.Kind == StorePaymentKind.Commission)?.Amount ?? 0, due, declaredPayments);

        var activeAffiliates = await Earned(_ => true, period).Select(e => e.AffiliateId).Distinct().CountAsync(ct).ConfigureAwait(false);
        var newUsers = await db.Users.AsNoTracking().CountAsync(u => u.CreatedAt >= start && u.CreatedAt < end, ct).ConfigureAwait(false);

        return new AdminOverviewDto(
            period, orders, sales,
            new CoinStats(
                ledger.FirstOrDefault(l => l.Type == CoinEntryType.Earned)?.Coins ?? 0,
                ledger.FirstOrDefault(l => l.Type == CoinEntryType.Adjustment)?.Coins ?? 0,
                expected, paid?.Coins ?? 0, paid?.Tnd ?? 0),
            new PayoutStats(pending?.Count ?? 0, pending?.Coins ?? 0, pending?.Tnd ?? 0, paid?.Count ?? 0, paid?.Tnd ?? 0),
            new StoreCounts(StoresIn(StoreStatus.Active), StoresIn(StoreStatus.Closed), StoresIn(StoreStatus.Disabled), StoresIn(StoreStatus.Suspended)),
            billingStats,
            activeAffiliates, newUsers,
            await DailyAsync(period, ct).ConfigureAwait(false),
            await TopStoresAsync(period, ct).ConfigureAwait(false),
            await TopProductsAsync(all, _ => true, period, OverviewTop, ct).ConfigureAwait(false),
            await TopAffiliatesAsync(_ => true, period, OverviewTop, ct).ConfigureAwait(false));
    }

    public async Task<AffiliateStatsDto?> AffiliateAsync(Guid affiliateId, StatsPeriod period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);
        // An affiliate has a code from sign-up / approval; a revoked one keeps it (and their history).
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == affiliateId && u.AffiliateCode != null, ct).ConfigureAwait(false)) return null;

        Expression<Func<LineFact, bool>> mine = f => f.AffiliateId == affiliateId;
        var earned = await Earned(e => e.AffiliateId == affiliateId, period).SumAsync(e => e.Coins, ct).ConfigureAwait(false);
        var expected = await Lines().Where(f => f.AffiliateId == affiliateId && Open.Contains(f.Status))
            .SumAsync(f => f.ExpectedCoins ?? 0, ct).ConfigureAwait(false);
        var partnerships = await PartnershipsAsync(p => p.AffiliateId == affiliateId, ct).ConfigureAwait(false);

        // Same per store / per product split as the affiliate's own wallet (020).
        var since = period.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var until = period.To.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var byStore = await coins.BreakdownAsync(affiliateId, CoinBreakdownBy.Store, since, until, ct).ConfigureAwait(false);
        var byProduct = await coins.BreakdownAsync(affiliateId, CoinBreakdownBy.Product, since, until, ct).ConfigureAwait(false);
        static StatsRow Row(CoinBreakdownRow r) => new(r.Id, r.Name, r.StoreName, r.Lines, r.Quantity, r.Sales, r.Coins);

        return new AffiliateStatsDto(
            period,
            await ConversionAsync(mine, period, ct).ConfigureAwait(false),
            await SalesAsync(mine, period, ct).ConfigureAwait(false),
            earned, expected, partnerships,
            [.. byStore.Select(Row)], [.. byProduct.Select(Row)]);
    }

    public async Task<StoreStatsDto?> StoreAsync(Guid storeId, StatsPeriod period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);
        var store = await db.Stores.AsNoTracking().Where(s => s.Id == storeId)
            .Select(s => new { s.AverageRating, s.RatingCount }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (store is null) return null;

        Expression<Func<LineFact, bool>> ofStore = f => f.StoreId == storeId;
        return new StoreStatsDto(
            period,
            await ConversionAsync(ofStore, period, ct).ConfigureAwait(false),
            await SalesAsync(ofStore, period, ct).ConfigureAwait(false),
            await Earned(e => e.StoreId == storeId, period).SumAsync(e => e.Coins, ct).ConfigureAwait(false),
            await PartnershipsAsync(p => p.StoreId == storeId, ct).ConfigureAwait(false),
            new RatingDto(store.AverageRating, store.RatingCount),
            await TopProductsAsync(ofStore, e => e.StoreId == storeId, period, DetailTop, ct).ConfigureAwait(false),
            await TopAffiliatesAsync(e => e.StoreId == storeId, period, DetailTop, ct).ConfigureAwait(false));
    }

    public async Task<ProductStatsDto?> ProductAsync(Guid productId, StatsPeriod period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);
        var product = await db.Products.AsNoTracking().Where(p => p.Id == productId)
            .Join(db.Stores, p => p.StoreId, s => s.Id, (p, s) => new
            {
                p.Id, p.Name, p.Slug, p.StoreId, StoreName = s.Name, p.Price,
                Percent = p.AffiliatePercent ?? s.DefaultAffiliatePercent,
                // Same rule as the public shop (CatalogQueries.VisibleProducts).
                Visible = p.Status == ProductStatus.Published && p.DeletedAt == null && p.DisabledAt == null
                    && (s.Status == StoreStatus.Active || s.Status == StoreStatus.Closed),
                p.AverageRating, p.RatingCount,
            })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (product is null) return null;

        Expression<Func<LineFact, bool>> ofProduct = f => f.ProductId == productId;
        return new ProductStatsDto(
            period, product.Id, product.Name, product.Slug, product.StoreId, product.StoreName, product.Price, product.Percent, product.Visible,
            new RatingDto(product.AverageRating, product.RatingCount),
            await ConversionAsync(ofProduct, period, ct).ConfigureAwait(false),
            await SalesAsync(ofProduct, period, ct).ConfigureAwait(false),
            await Earned(e => e.ProductId == productId, period).SumAsync(e => e.Coins, ct).ConfigureAwait(false),
            await TopAffiliatesAsync(e => e.ProductId == productId, period, DetailTop, ct).ConfigureAwait(false));
    }

    private async Task<ConversionStats> ConversionAsync(Expression<Func<LineFact, bool>> scope, StatsPeriod period, CancellationToken ct)
    {
        var (start, end) = (period.Start, period.End);
        var byStatus = await Lines().Where(scope).Where(f => f.PlacedAt >= start && f.PlacedAt < end)
            .Select(f => new { f.StoreOrderId, f.Status }).Distinct()
            .GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct).ConfigureAwait(false);
        int Count(params StoreOrderStatus[] statuses) => byStatus.Where(s => statuses.Contains(s.Status)).Sum(s => s.Count);

        return new ConversionStats(byStatus.Sum(s => s.Count), Count(Open), Count(StoreOrderStatus.Delivered),
            Count(StoreOrderStatus.Cancelled), Count(StoreOrderStatus.NotDelivered));
    }

    private async Task<SalesStats> SalesAsync(Expression<Func<LineFact, bool>> scope, StatsPeriod period, CancellationToken ct)
    {
        var delivered = Delivered(scope, period);
        var all = await TotalsAsync(delivered, ct).ConfigureAwait(false);
        var viaAffiliates = await TotalsAsync(delivered.Where(f => f.AffiliateId != null), ct).ConfigureAwait(false);
        return new SalesStats(all.Orders, all.Amount, all.Quantity, viaAffiliates.Orders, viaAffiliates.Amount);
    }

    private static async Task<(int Orders, decimal Amount, int Quantity)> TotalsAsync(IQueryable<LineFact> lines, CancellationToken ct)
    {
        var totals = await lines.GroupBy(_ => 1)
            .Select(g => new { Orders = g.Select(f => f.StoreOrderId).Distinct().Count(), Amount = g.Sum(f => f.LineTotal), Quantity = g.Sum(f => f.Quantity) })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return (totals?.Orders ?? 0, totals?.Amount ?? 0, totals?.Quantity ?? 0);
    }

    /// <summary>One point per day of the period (days without orders included, so a chart has no gaps).</summary>
    private async Task<IReadOnlyList<DailyStats>> DailyAsync(StatsPeriod period, CancellationToken ct)
    {
        var (start, end) = (period.Start, period.End);
        var storeOrders = db.Orders.AsNoTracking().SelectMany(o => o.StoreOrders, (o, s) => new { o.CreatedAt, s.Status, s.DeliveredAt, s.Subtotal });

        var placed = await storeOrders.Where(x => x.CreatedAt >= start && x.CreatedAt < end)
            .GroupBy(x => x.CreatedAt.Date).Select(g => new { Day = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => DateOnly.FromDateTime(x.Day), x => x.Count, ct).ConfigureAwait(false);
        var delivered = await storeOrders.Where(x => x.Status == StoreOrderStatus.Delivered && x.DeliveredAt >= start && x.DeliveredAt < end)
            .GroupBy(x => x.DeliveredAt!.Value.Date).Select(g => new { Day = g.Key, Count = g.Count(), Sales = g.Sum(x => x.Subtotal) })
            .ToDictionaryAsync(x => DateOnly.FromDateTime(x.Day), x => (x.Count, x.Sales), ct).ConfigureAwait(false);

        return [.. Enumerable.Range(0, period.Days).Select(i =>
        {
            var day = period.From.AddDays(i);
            var (count, sales) = delivered.GetValueOrDefault(day);
            return new DailyStats(day, placed.GetValueOrDefault(day), count, sales);
        })];
    }

    private async Task<IReadOnlyList<StatsRow>> TopStoresAsync(StatsPeriod period, CancellationToken ct)
    {
        var rows = await Delivered(_ => true, period)
            .GroupBy(f => new { f.StoreId, f.StoreName })
            .Select(g => new
            {
                g.Key.StoreId, g.Key.StoreName, Orders = g.Select(f => f.StoreOrderId).Distinct().Count(),
                Quantity = g.Sum(f => f.Quantity), Amount = g.Sum(f => f.LineTotal),
            })
            .OrderByDescending(x => x.Amount).ThenBy(x => x.StoreName).Take(OverviewTop)
            .ToListAsync(ct).ConfigureAwait(false);
        var ids = rows.Select(r => r.StoreId).ToList();
        var storeCoins = await Earned(e => e.StoreId != null && ids.Contains(e.StoreId.Value), period)
            .GroupBy(e => e.StoreId!.Value).Select(g => new { Id = g.Key, Coins = g.Sum(e => e.Coins) })
            .ToDictionaryAsync(x => x.Id, x => x.Coins, ct).ConfigureAwait(false);
        return [.. rows.Select(r => new StatsRow(r.StoreId, r.StoreName, null, r.Orders, r.Quantity, r.Amount, storeCoins.GetValueOrDefault(r.StoreId)))];
    }

    /// <summary>Best sellers of the period, with the coins affiliates earned on each.</summary>
    private async Task<IReadOnlyList<StatsRow>> TopProductsAsync(
        Expression<Func<LineFact, bool>> scope, Expression<Func<CoinEntry, bool>> coinScope, StatsPeriod period, int take, CancellationToken ct)
    {
        var rows = await Delivered(scope, period)
            .GroupBy(f => new { f.ProductId, f.ProductName, f.StoreName })
            .Select(g => new
            {
                g.Key.ProductId, g.Key.ProductName, g.Key.StoreName, Orders = g.Select(f => f.StoreOrderId).Distinct().Count(),
                Quantity = g.Sum(f => f.Quantity), Amount = g.Sum(f => f.LineTotal),
            })
            .OrderByDescending(x => x.Amount).ThenBy(x => x.ProductName).Take(take)
            .ToListAsync(ct).ConfigureAwait(false);
        var ids = rows.Select(r => r.ProductId).ToList();
        var productCoins = await Earned(coinScope, period).Where(e => e.ProductId != null && ids.Contains(e.ProductId.Value))
            .GroupBy(e => e.ProductId!.Value).Select(g => new { Id = g.Key, Coins = g.Sum(e => e.Coins) })
            .ToDictionaryAsync(x => x.Id, x => x.Coins, ct).ConfigureAwait(false);
        return [.. rows.Select(r => new StatsRow(r.ProductId, r.ProductName, r.StoreName, r.Orders, r.Quantity, r.Amount, productCoins.GetValueOrDefault(r.ProductId)))];
    }

    /// <summary>Affiliates who earned the most coins in the period (count = credited lines, amount = their sales).</summary>
    private async Task<IReadOnlyList<StatsRow>> TopAffiliatesAsync(Expression<Func<CoinEntry, bool>> scope, StatsPeriod period, int take, CancellationToken ct)
    {
        var rows = await Earned(scope, period)
            .GroupBy(e => e.AffiliateId)
            .Select(g => new { Id = g.Key, Lines = g.Count(), Quantity = g.Sum(e => e.Quantity ?? 0), Amount = g.Sum(e => e.LineTotal ?? 0), Coins = g.Sum(e => e.Coins) })
            .OrderByDescending(x => x.Coins).ThenBy(x => x.Id).Take(take)
            .ToListAsync(ct).ConfigureAwait(false);
        var ids = rows.Select(r => r.Id).ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.FullName, u.AffiliateCode), ct).ConfigureAwait(false);
        return [.. rows.Select(r =>
        {
            var (name, code) = users.GetValueOrDefault(r.Id);
            return new StatsRow(r.Id, name ?? "", code, r.Lines, r.Quantity, r.Amount, r.Coins);
        })];
    }

    private async Task<PartnershipCounts> PartnershipsAsync(Expression<Func<Partnership, bool>> scope, CancellationToken ct)
    {
        var byStatus = await db.Partnerships.AsNoTracking().Where(scope)
            .GroupBy(p => p.Status).Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct).ConfigureAwait(false);
        int Count(PartnershipStatus status) => byStatus.FirstOrDefault(s => s.Status == status)?.Count ?? 0;
        return new PartnershipCounts(Count(PartnershipStatus.Requested), Count(PartnershipStatus.Accepted),
            Count(PartnershipStatus.Refused), Count(PartnershipStatus.Ended));
    }
}
