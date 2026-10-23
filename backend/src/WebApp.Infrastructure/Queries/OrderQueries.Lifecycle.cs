using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Infrastructure.Queries;

/// <summary>Store-owner and admin order views (016).</summary>
internal sealed partial class OrderQueries
{
    public async Task<PagedResult<StoreOrderListItem>> ListForStoreAsync(Guid storeId, StoreOrderQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);

        var rows =
            from o in db.Orders.AsNoTracking()
            from s in o.StoreOrders
            where s.StoreId == storeId
            select new { o, s };
        if (query.Status is { } status) rows = rows.Where(x => x.s.Status == status);
        if (query.WithAffiliate is { } withAffiliate) rows = rows.Where(x => x.s.Lines.Any(l => l.AffiliateId != null) == withAffiliate);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{EscapeLike(query.Search.Trim())}%";
            var digits = new string(query.Search.Where(char.IsDigit).ToArray());
            var phone = digits.Length >= 3 ? $"%{digits}%" : null;
            rows = rows.Where(x => EF.Functions.ILike(x.o.Reference, term, "\\") || EF.Functions.ILike(x.o.ClientName, term, "\\")
                || (phone != null && EF.Functions.Like(x.o.ClientPhone, phone)));
        }

        var total = await rows.CountAsync(ct).ConfigureAwait(false);
        // Work queue: oldest open orders first would hide new ones; newest first, like a mailbox.
        var items = await rows
            .OrderByDescending(x => x.o.CreatedAt).ThenBy(x => x.s.Id)
            .Skip(paging.Skip).Take(paging.PageSize)
            .Select(x => new StoreOrderListItem(
                x.s.Id, x.o.Reference, x.o.CreatedAt, x.o.ClientName, x.o.City,
                x.s.Lines.Sum(l => l.Quantity), x.s.AmountToCollect, x.s.Status, x.s.PaymentStatus,
                x.s.Lines.Any(l => l.AffiliateId != null), x.o.PlacedByAffiliateId != null))
            .ToListAsync(ct).ConfigureAwait(false);

        return new PagedResult<StoreOrderListItem>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<StoreOrderCounts> CountForStoreAsync(Guid storeId, CancellationToken ct = default)
    {
        var counts = await db.Orders.AsNoTracking()
            .SelectMany(o => o.StoreOrders)
            .Where(s => s.StoreId == storeId)
            .GroupBy(s => s.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct).ConfigureAwait(false);
        return new StoreOrderCounts(counts.ToDictionary(c => c.Key, c => c.Count));
    }

    public async Task<StoreOrderDetailDto?> GetForStoreAsync(Guid storeId, Guid storeOrderId, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking()
            .Where(o => o.StoreOrders.Any(s => s.Id == storeOrderId && s.StoreId == storeId))
            .Include(o => o.StoreOrders.Where(s => s.Id == storeOrderId)).ThenInclude(s => s.Lines)
            .Include(o => o.StoreOrders.Where(s => s.Id == storeOrderId)).ThenInclude(s => s.History)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (order is null) return null;

        var s = order.StoreOrders.Single();
        var images = await MainImagesAsync(s.Lines.Select(l => l.ProductId), ct).ConfigureAwait(false);
        StoreOrderStatus[] all = [StoreOrderStatus.Confirmed, StoreOrderStatus.Shipped, StoreOrderStatus.Delivered, StoreOrderStatus.NotDelivered, StoreOrderStatus.Cancelled];

        return new StoreOrderDetailDto(
            s.Id, order.Reference, order.CreatedAt, s.Status, s.PaymentStatus,
            order.ClientName, order.ClientPhone, order.City, order.Address, order.Note, order.PlacedByAffiliate,
            s.DeliveryFee, s.Subtotal, s.AmountToCollect,
            [.. s.Lines.OrderBy(l => l.CreatedAt).Select(l => new StoreOrderLineDto(
                l.Id, l.ProductId, l.ProductName, l.ProductSlug, images.GetValueOrDefault(l.ProductId),
                [.. l.Options.Select(o => new CartOptionLabel(new LocalizedName(o.AxisFr, o.AxisAr, o.AxisEn), new LocalizedName(o.ValueFr, o.ValueAr, o.ValueEn)))],
                l.UnitPrice, l.Quantity, l.LineTotal, l.AffiliateCode, l.AffiliatePercent,
                l.AffiliatePercent is { } p ? Math.Round(l.LineTotal * p / 100m, 3) : null))],
            History(s),
            [.. all.Where(to => s.Status != StoreOrderStatus.Delivered && Order.IsAllowed(OrderActorRole.Store, s.Status, to))]);
    }

    public async Task<PagedResult<AdminOrderListItem>> ListForAdminAsync(AdminOrderQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var orders = db.Orders.AsNoTracking();

        if (query.Status is { } status) orders = orders.Where(o => o.StoreOrders.Any(s => s.Status == status));
        if (query.StoreId is { } storeId) orders = orders.Where(o => o.StoreOrders.Any(s => s.StoreId == storeId));
        if (query.ClientId is { } clientId) orders = orders.Where(o => o.ClientId == clientId);
        if (query.AffiliateId is { } affiliateId)
            orders = orders.Where(o => o.PlacedByAffiliateId == affiliateId || o.StoreOrders.Any(s => s.Lines.Any(l => l.AffiliateId == affiliateId)));
        if (query.ProductId is { } productId) orders = orders.Where(o => o.StoreOrders.Any(s => s.Lines.Any(l => l.ProductId == productId)));
        if (query.From is not null || query.To is not null)
        {
            DateTime? start = query.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            DateTime? end = query.To?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            orders = query.DateField == OrderDateField.Delivered
                ? orders.Where(o => o.StoreOrders.Any(s => s.DeliveredAt != null
                    && (start == null || s.DeliveredAt >= start) && (end == null || s.DeliveredAt < end)
                    && (query.StoreId == null || s.StoreId == query.StoreId)))
                : orders.Where(o => (start == null || o.CreatedAt >= start) && (end == null || o.CreatedAt < end));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{EscapeLike(query.Search.Trim())}%";
            var digits = new string(query.Search.Where(char.IsDigit).ToArray());
            var phone = digits.Length >= 3 ? $"%{digits}%" : null;
            orders = orders.Where(o => EF.Functions.ILike(o.Reference, term, "\\") || EF.Functions.ILike(o.ClientName, term, "\\")
                || (phone != null && EF.Functions.Like(o.ClientPhone, phone))
                || o.StoreOrders.Any(s => EF.Functions.ILike(s.StoreName, term, "\\")));
        }

        var total = await orders.CountAsync(ct).ConfigureAwait(false);
        var items = await orders
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip(paging.Skip).Take(paging.PageSize)
            .Select(o => new AdminOrderListItem(
                o.Id, o.Reference, o.CreatedAt, o.ClientName, o.ClientPhone, o.City, o.PlacedByAffiliateId != null, o.Total,
                o.StoreOrders.Select(s => new AdminStoreOrderRef(s.Id, s.StoreId, s.StoreName, s.Status, s.AmountToCollect)).ToList()))
            .ToListAsync(ct).ConfigureAwait(false);

        return new PagedResult<AdminOrderListItem>(items, paging.Page, paging.PageSize, total);
    }

    public Task<OrderDto?> GetForAdminAsync(string reference, CancellationToken ct = default) =>
        DetailAsync(db.Orders.AsNoTracking().Where(o => o.Reference == reference), ct);

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);
}
