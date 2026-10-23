using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Queries;

/// <summary>Review views (017): the client's "to rate / my reviews", the store owner's list and the admin list.</summary>
internal sealed class ReviewQueries(AppDbContext db, TimeProvider clock) : IReviewQueries
{
    /// <summary>Enough delivered parcels for any real client; the oldest ones drop out first.</summary>
    private const int MaxDeliveredStoreOrders = 200;

    public async Task<MyReviewsDto> MineAsync(Guid userId, string userPhone, CancellationToken ct = default)
    {
        // Same ownership rule as ReviewRepository.FindReviewableAsync: own orders, or guest / affiliate-placed ones
        // with the account's phone.
        var delivered = await (
                from o in db.Orders.AsNoTracking()
                where o.ClientId == userId || (o.ClientId == null && o.ClientPhone == userPhone && o.PlacedByAffiliateId != userId)
                from s in o.StoreOrders
                where s.Status == StoreOrderStatus.Delivered
                join st in db.Stores on s.StoreId equals st.Id
                where st.OwnerId != userId
                orderby s.DeliveredAt descending
                select new
                {
                    o.Reference,
                    StoreOrderId = s.Id,
                    s.StoreName,
                    StoreSlug = st.DeletedAt == null ? st.Slug : null,
                    DeliveredAt = s.DeliveredAt ?? s.UpdatedAt,
                    Lines = s.Lines.Select(l => new { l.Id, l.ProductId, l.ProductName }).ToList(),
                })
            .Take(MaxDeliveredStoreOrders)
            .ToListAsync(ct).ConfigureAwait(false);

        var storeOrderIds = delivered.Select(d => d.StoreOrderId).ToList();
        var reviewed = await db.Reviews.AsNoTracking().Where(r => storeOrderIds.Contains(r.StoreOrderId))
            .Select(r => new { r.Target, r.StoreOrderId, r.OrderLineId }).ToListAsync(ct).ConfigureAwait(false);
        var reviewedLines = reviewed.Where(r => r.Target == ReviewTarget.Product).Select(r => r.OrderLineId!.Value).ToHashSet();
        var reviewedStores = reviewed.Where(r => r.Target == ReviewTarget.Store).Select(r => r.StoreOrderId).ToHashSet();

        var products = await ProductInfoAsync(delivered.SelectMany(d => d.Lines.Select(l => l.ProductId)), ct).ConfigureAwait(false);
        var toWrite = new List<ReviewToWriteDto>();
        foreach (var d in delivered)
        {
            foreach (var line in d.Lines.Where(l => !reviewedLines.Contains(l.Id)))
            {
                var product = products.GetValueOrDefault(line.ProductId);
                toWrite.Add(new ReviewToWriteDto(ReviewTarget.Product, d.Reference, d.StoreOrderId, line.Id, d.StoreName, d.StoreSlug,
                    line.ProductName, product?.Slug, product?.ImageUrl, d.DeliveredAt));
            }
            if (!reviewedStores.Contains(d.StoreOrderId))
                toWrite.Add(new ReviewToWriteDto(ReviewTarget.Store, d.Reference, d.StoreOrderId, null, d.StoreName, d.StoreSlug,
                    null, null, null, d.DeliveredAt));
        }

        var written = await MyReviewsAsync(db.Reviews.Where(r => r.AuthorId == userId), ct).ConfigureAwait(false);
        return new MyReviewsDto(toWrite, written);
    }

    public async Task<MyReviewDto?> GetMineAsync(Guid userId, Guid reviewId, CancellationToken ct = default) =>
        await MyReviewsAsync(db.Reviews.Where(r => r.Id == reviewId && r.AuthorId == userId), ct).ConfigureAwait(false) is [var review] ? review : null;

    private async Task<IReadOnlyList<MyReviewDto>> MyReviewsAsync(IQueryable<Review> reviews, CancellationToken ct)
    {
        var rows = await (
                from r in reviews.AsNoTracking()
                join o in db.Orders on r.OrderId equals o.Id
                join st in db.Stores on r.StoreId equals st.Id
                orderby r.CreatedAt descending
                select new
                {
                    Review = r,
                    o.Reference,
                    st.Name,
                    StoreSlug = st.DeletedAt == null ? st.Slug : null,
                    ProductName = db.Set<OrderLine>().Where(l => l.Id == r.OrderLineId).Select(l => l.ProductName).FirstOrDefault(),
                })
            .ToListAsync(ct).ConfigureAwait(false);

        var products = await ProductInfoAsync(rows.Where(x => x.Review.ProductId is not null).Select(x => x.Review.ProductId!.Value), ct)
            .ConfigureAwait(false);
        var now = clock.GetUtcNow().UtcDateTime;
        return [.. rows.Select(x =>
        {
            var r = x.Review;
            var product = r.ProductId is { } pid ? products.GetValueOrDefault(pid) : null;
            return new MyReviewDto(r.Id, r.Target, x.Reference, r.StoreOrderId, r.OrderLineId, x.Name, x.StoreSlug, x.ProductName,
                product?.Slug, product?.ImageUrl, r.Stars, r.Comment, r.CreatedAt, r.EditedAt, r.EditableUntil, r.CanEdit(now),
                r.Reply, r.RepliedAt, r.IsHidden, r.HiddenReason);
        })];
    }

    private sealed record ProductInfo(string? Slug, string? ImageUrl);

    /// <summary>Current slug (null once deleted — nothing to link to) and main photo of the products.</summary>
    private async Task<Dictionary<Guid, ProductInfo>> ProductInfoAsync(IEnumerable<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Slug, p.DeletedAt, p.ImageIds }).ToListAsync(ct).ConfigureAwait(false);
        var imageIds = products.Where(p => p.ImageIds.Count > 0).Select(p => p.ImageIds[0]).ToList();
        var urls = imageIds.Count == 0
            ? []
            : await db.Images.AsNoTracking().Where(i => imageIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => ImageUrls.For(i.Path), ct)
                .ConfigureAwait(false);
        return products.ToDictionary(p => p.Id, p => new ProductInfo(
            p.DeletedAt == null ? p.Slug : null,
            p.ImageIds.Count > 0 ? urls.GetValueOrDefault(p.ImageIds[0]) : null));
    }

    // -- Store owner ----------------------------------------------------------------

    public async Task<StoreReviewsDto> ForOwnerAsync(Guid storeId, StoreReviewQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var all = db.Reviews.AsNoTracking().Where(r => r.StoreId == storeId);
        var visible = all.Where(r => r.HiddenAt == null);
        var storeStats = await ReviewStats.ForAsync(visible.Where(r => r.Target == ReviewTarget.Store), ct).ConfigureAwait(false);
        var productStats = await ReviewStats.ForAsync(visible.Where(r => r.Target == ReviewTarget.Product), ct).ConfigureAwait(false);
        var unanswered = await visible.CountAsync(r => r.Reply == null, ct).ConfigureAwait(false);

        var rows = all;
        if (query.Target is { } target) rows = rows.Where(r => r.Target == target);
        if (query.Stars is >= 1 and <= 5) rows = rows.Where(r => r.Stars == query.Stars);
        if (query.Replied is { } replied) rows = rows.Where(r => (r.Reply != null) == replied);

        var total = await rows.CountAsync(ct).ConfigureAwait(false);
        var items = await OwnerRows(rows).Skip(paging.Skip).Take(paging.PageSize).ToListAsync(ct).ConfigureAwait(false);
        return new StoreReviewsDto(storeStats, productStats, unanswered, new PagedResult<StoreReviewDto>(items, paging.Page, paging.PageSize, total));
    }

    public Task<StoreReviewDto?> GetForOwnerAsync(Guid storeId, Guid reviewId, CancellationToken ct = default) =>
        OwnerRows(db.Reviews.AsNoTracking().Where(r => r.Id == reviewId && r.StoreId == storeId)).FirstOrDefaultAsync(ct);

    private IQueryable<StoreReviewDto> OwnerRows(IQueryable<Review> reviews) =>
        from r in reviews
        join o in db.Orders on r.OrderId equals o.Id
        orderby r.CreatedAt descending, r.Id
        select new StoreReviewDto(
            r.Id, r.Target, r.AuthorName, o.Reference, r.StoreOrderId,
            db.Set<OrderLine>().Where(l => l.Id == r.OrderLineId).Select(l => l.ProductName).FirstOrDefault(),
            db.Products.Where(p => p.Id == r.ProductId && p.DeletedAt == null).Select(p => p.Slug).FirstOrDefault(),
            r.Stars, r.Comment, r.CreatedAt, r.EditedAt, r.Reply, r.RepliedAt, r.HiddenAt != null, r.HiddenReason);

    // -- Admin --------------------------------------------------------------------

    public async Task<PagedResult<AdminReviewDto>> AdminListAsync(AdminReviewQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var rows = db.Reviews.AsNoTracking();

        if (query.Target is { } target) rows = rows.Where(r => r.Target == target);
        if (query.Hidden is { } hidden) rows = rows.Where(r => (r.HiddenAt != null) == hidden);
        if (query.Stars is >= 1 and <= 5) rows = rows.Where(r => r.Stars == query.Stars);
        if (query.StoreId is { } storeId) rows = rows.Where(r => r.StoreId == storeId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{EscapeLike(query.Search.Trim().TrimStart('#'))}%";
            rows = rows.Where(r => EF.Functions.ILike(r.Comment ?? "", term, "\\")
                || db.Users.Any(u => u.Id == r.AuthorId && (EF.Functions.ILike(u.FullName, term, "\\") || EF.Functions.ILike(u.Email, term, "\\")))
                || db.Stores.Any(s => s.Id == r.StoreId && EF.Functions.ILike(s.Name, term, "\\"))
                || db.Set<OrderLine>().Any(l => l.Id == r.OrderLineId && EF.Functions.ILike(l.ProductName, term, "\\"))
                || db.Orders.Any(o => o.Id == r.OrderId && EF.Functions.ILike(o.Reference, term, "\\")));
        }

        var total = await rows.CountAsync(ct).ConfigureAwait(false);
        var page = rows.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip(paging.Skip).Take(paging.PageSize);
        var items = await AdminRows(page).ToListAsync(ct).ConfigureAwait(false);
        return new PagedResult<AdminReviewDto>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<AdminReviewCounts> AdminCountsAsync(CancellationToken ct = default)
    {
        var reviews = db.Reviews.AsNoTracking();
        return new AdminReviewCounts(
            await reviews.CountAsync(ct).ConfigureAwait(false),
            await reviews.CountAsync(r => r.HiddenAt != null, ct).ConfigureAwait(false),
            await reviews.CountAsync(r => r.HiddenAt == null && r.Stars <= 2, ct).ConfigureAwait(false));
    }

    public Task<AdminReviewDto?> AdminGetAsync(Guid reviewId, CancellationToken ct = default) =>
        AdminRows(db.Reviews.AsNoTracking().Where(r => r.Id == reviewId)).FirstOrDefaultAsync(ct);

    private IQueryable<AdminReviewDto> AdminRows(IQueryable<Review> reviews) =>
        from r in reviews
        join u in db.Users on r.AuthorId equals u.Id
        join st in db.Stores on r.StoreId equals st.Id
        join o in db.Orders on r.OrderId equals o.Id
        orderby r.CreatedAt descending, r.Id
        select new AdminReviewDto(
            r.Id, r.Target, r.AuthorId, u.FullName, u.Email, r.StoreId, st.Name, st.Slug, r.ProductId,
            db.Set<OrderLine>().Where(l => l.Id == r.OrderLineId).Select(l => l.ProductName).FirstOrDefault(),
            db.Products.Where(p => p.Id == r.ProductId).Select(p => p.Slug).FirstOrDefault(),
            o.Reference, r.Stars, r.Comment, r.CreatedAt, r.EditedAt, r.Reply, r.RepliedAt, r.HiddenAt, r.HiddenReason);

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);
}
