using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Queries;

/// <summary>Order views for clients, guests and affiliates (014). Everything shown comes from the order's own snapshot.</summary>
internal sealed partial class OrderQueries(AppDbContext db) : IOrderQueries
{
    private IQueryable<Order> PlacedBy(Guid userId) =>
        db.Orders.AsNoTracking().Where(o => o.ClientId == userId || o.PlacedByAffiliateId == userId);

    public async Task<PagedResult<OrderListItem>> ListForUserAsync(Guid userId, OrderListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var orders = PlacedBy(userId);

        var total = await orders.CountAsync(ct).ConfigureAwait(false);
        var page = await orders
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip(paging.Skip).Take(paging.PageSize)
            .Include(o => o.StoreOrders).ThenInclude(s => s.Lines)
            .AsSplitQuery()
            .ToListAsync(ct).ConfigureAwait(false);

        var items = page.Select(o => new OrderListItem(
            o.Id, o.Reference, o.CreatedAt, o.ClientName, o.PlacedByAffiliate,
            o.StoreOrders.SelectMany(s => s.Lines).Sum(l => l.Quantity),
            [.. o.StoreOrders.Select(s => s.StoreName)],
            [.. o.StoreOrders.Select(s => s.Status)],
            o.Total)).ToList();
        return new PagedResult<OrderListItem>(items, paging.Page, paging.PageSize, total);
    }

    public Task<OrderDto?> GetForUserAsync(Guid userId, string reference, CancellationToken ct = default) =>
        DetailAsync(PlacedBy(userId).Where(o => o.Reference == reference), ct);

    public Task<OrderDto?> GetByReferenceAndPhoneAsync(string reference, string normalizedPhone, CancellationToken ct = default) =>
        DetailAsync(db.Orders.AsNoTracking().Where(o => o.Reference == reference && o.ClientPhone == normalizedPhone), ct);

    public async Task<AffiliateOrdersDto> ListForAffiliateAsync(Guid affiliateId, OrderListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);

        var lines =
            from o in db.Orders.AsNoTracking()
            from s in o.StoreOrders
            from l in s.Lines
            where l.AffiliateId == affiliateId
            select new { o, s, l };

        var summary = await lines
            .GroupBy(_ => 1)
            .Select(g => new AffiliateOrderSummary(g.Count(), g.Sum(x => x.l.LineTotal),
                g.Where(x => x.s.Status == StoreOrderStatus.Placed || x.s.Status == StoreOrderStatus.Confirmed || x.s.Status == StoreOrderStatus.Shipped)
                    .Sum(x => x.l.ExpectedCoins ?? 0),
                g.Where(x => x.s.Status == StoreOrderStatus.Delivered).Sum(x => x.l.ExpectedCoins ?? 0)))
            .FirstOrDefaultAsync(ct).ConfigureAwait(false) ?? new AffiliateOrderSummary(0, 0, 0, 0);

        var items = await lines
            .OrderByDescending(x => x.o.CreatedAt).ThenBy(x => x.l.Id)
            .Skip(paging.Skip).Take(paging.PageSize)
            .Select(x => new AffiliateOrderLine(
                x.l.Id, x.o.Reference, x.o.CreatedAt, x.s.StoreName, x.l.ProductName, x.o.ClientName, x.l.Quantity, x.l.LineTotal,
                x.l.AffiliatePercent ?? 0, x.l.ExpectedCoins ?? 0, x.s.Status, x.o.PlacedByAffiliateId == affiliateId))
            .ToListAsync(ct).ConfigureAwait(false);

        return new AffiliateOrdersDto(summary, new PagedResult<AffiliateOrderLine>(items, paging.Page, paging.PageSize, summary.Lines));
    }

    private async Task<OrderDto?> DetailAsync(IQueryable<Order> query, CancellationToken ct)
    {
        var order = await query.Include(o => o.StoreOrders).ThenInclude(s => s.Lines)
            .Include(o => o.StoreOrders).ThenInclude(s => s.History).AsSplitQuery()
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (order is null) return null;

        var storeIds = order.StoreOrders.Select(s => s.StoreId).ToList();
        var slugs = await db.Stores.AsNoTracking().Where(s => storeIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Slug, ct).ConfigureAwait(false);
        // Current main photo of each product (the product row itself is never deleted).
        var productIds = order.StoreOrders.SelectMany(s => s.Lines).Select(l => l.ProductId).Distinct().ToList();
        var mainImages = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ImageIds }).ToListAsync(ct).ConfigureAwait(false);
        var imageIds = mainImages.Where(p => p.ImageIds.Count > 0).Select(p => p.ImageIds[0]).ToList();
        var urls = await db.Images.AsNoTracking().Where(i => imageIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => ImageUrls.For(i.Path), ct).ConfigureAwait(false);
        var imageOf = mainImages.ToDictionary(p => p.Id, p => p.ImageIds.Count > 0 ? urls.GetValueOrDefault(p.ImageIds[0]) : null);

        return new OrderDto(
            order.Id, order.Reference, order.CreatedAt, order.ClientName, order.ClientPhone, order.City, order.Address, order.Note,
            order.PlacedByAffiliate, order.ItemsTotal, order.DeliveryTotal, order.Total,
            [.. order.StoreOrders.Select(s => new StoreOrderDto(
                s.Id, s.StoreId, s.StoreName, slugs.GetValueOrDefault(s.StoreId), s.Status, s.PaymentStatus, s.DeliveryFee, s.Subtotal, s.AmountToCollect,
                [.. s.Lines.OrderBy(l => l.CreatedAt).Select(l => new OrderLineDto(
                    l.Id, l.ProductId, l.ProductName, l.ProductSlug, imageOf.GetValueOrDefault(l.ProductId),
                    [.. l.Options.Select(o => new CartOptionLabel(new LocalizedName(o.AxisFr, o.AxisAr, o.AxisEn), new LocalizedName(o.ValueFr, o.ValueAr, o.ValueEn)))],
                    l.UnitPrice, l.Quantity, l.LineTotal, l.AffiliateCode, l.AffiliatePercent))],
                History(s),
                Order.IsAllowed(OrderActorRole.Client, s.Status, StoreOrderStatus.Cancelled)))]);
    }

    private static List<StoreOrderEventDto> History(StoreOrder s) =>
        [.. s.History.OrderBy(e => e.At).Select(e => new StoreOrderEventDto(e.From, e.To, e.ActorRole, e.Reason, e.NotDeliveredReason, e.At))];

    /// <summary>Main photo of each product (the product row is never deleted).</summary>
    private async Task<Dictionary<Guid, string?>> MainImagesAsync(IEnumerable<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        var mainImages = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.ImageIds }).ToListAsync(ct).ConfigureAwait(false);
        var imageIds = mainImages.Where(p => p.ImageIds.Count > 0).Select(p => p.ImageIds[0]).ToList();
        var urls = await db.Images.AsNoTracking().Where(i => imageIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => ImageUrls.For(i.Path), ct).ConfigureAwait(false);
        return mainImages.ToDictionary(p => p.Id, p => p.ImageIds.Count > 0 ? urls.GetValueOrDefault(p.ImageIds[0]) : null);
    }
}
