using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Infrastructure.Queries;

/// <summary>Store and product moderation lists (010): every store and product, whatever its state.</summary>
internal sealed partial class AdminQueries
{
    public async Task<PagedResult<AdminStoreListItem>> ListStoresAsync(AdminStoreQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var stores = db.Stores.AsNoTracking();

        stores = query.Deleted == true ? stores.Where(s => s.DeletedAt != null) : stores.Where(s => s.DeletedAt == null);
        if (query.Status is { } status) stores = stores.Where(s => s.Status == status);
        if (!string.IsNullOrWhiteSpace(query.Category))
            stores = stores.Where(s => db.StoreCategories.Any(c => c.Id == s.CategoryId && c.Slug == query.Category));
        if (!string.IsNullOrWhiteSpace(query.City)) stores = stores.Where(s => s.City == query.City);
        // Dates from the query string are days (no time zone): Npgsql needs UTC values for timestamptz.
        if (query.CreatedFrom is { } from)
        {
            var start = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
            stores = stores.Where(s => s.CreatedAt >= start);
        }
        if (query.CreatedTo is { } to)
        {
            var end = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);
            stores = stores.Where(s => s.CreatedAt < end);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{EscapeLike(query.Search.Trim())}%";
            stores = stores.Where(s => EF.Functions.ILike(s.Name, term, "\\") || EF.Functions.ILike(s.Slug, term, "\\")
                || db.Users.Any(u => u.Id == s.OwnerId && (EF.Functions.ILike(u.Email, term, "\\") || EF.Functions.ILike(u.FullName, term, "\\"))));
        }

        var total = await stores.CountAsync(ct).ConfigureAwait(false);
        var items = await stores
            .OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Id)
            .Skip(paging.Skip).Take(paging.PageSize)
            .Join(db.Users, s => s.OwnerId, u => u.Id, (s, u) => new { s, u })
            .Join(db.StoreCategories, x => x.s.CategoryId, c => c.Id, (x, c) => new AdminStoreListItem(
                x.s.Id, x.s.Name, x.s.Slug,
                db.Images.Where(i => i.Id == x.s.LogoImageId).Select(i => ImageUrls.RequestPath + "/" + i.Path).FirstOrDefault(),
                new LocalizedName(c.NameFr, c.NameAr, c.NameEn), x.s.City, x.s.Status, x.s.DeletedAt != null, x.s.ModerationReason,
                x.u.Id, x.u.FullName, x.u.Email,
                db.Products.Count(p => p.StoreId == x.s.Id && p.DeletedAt == null),
                x.s.CreatedAt))
            .ToListAsync(ct).ConfigureAwait(false);

        return new PagedResult<AdminStoreListItem>(items, paging.Page, paging.PageSize, total);
    }

    public Task<AdminStoreDto?> GetStoreAsync(Guid id, CancellationToken ct = default) =>
        db.Stores.AsNoTracking()
            .Where(s => s.Id == id)
            .Join(db.Users, s => s.OwnerId, u => u.Id, (s, u) => new { s, u })
            .Join(db.StoreCategories, x => x.s.CategoryId, c => c.Id, (x, c) => new AdminStoreDto(
                x.s.Id, x.s.Name, x.s.Slug, new StoreCategoryRef(c.Id, c.Slug, new LocalizedName(c.NameFr, c.NameAr, c.NameEn)),
                x.s.Description, x.s.Phone, x.s.City, x.s.Address,
                db.Images.Where(i => i.Id == x.s.LogoImageId).Select(i => ImageUrls.RequestPath + "/" + i.Path).FirstOrDefault(),
                db.Images.Where(i => i.Id == x.s.BannerImageId).Select(i => ImageUrls.RequestPath + "/" + i.Path).FirstOrDefault(),
                x.s.Status, x.s.DeletedAt != null, x.s.ModerationReason, x.s.DisabledAt, x.s.DeletedAt,
                x.s.DefaultAffiliatePercent, x.s.DeliveryFee, x.s.DeliveryCities, x.s.DeliveryMinDays, x.s.DeliveryMaxDays,
                x.s.FacebookUrl, x.s.InstagramUrl, x.s.WebsiteUrl, x.s.SubscriptionMonthlyPrice, x.s.CreatedAt, x.s.UpdatedAt,
                new AdminStoreOwner(x.u.Id, x.u.FullName, x.u.Email, x.u.Phone, x.u.Status, x.u.Roles.Contains(Role.StoreOwner)),
                db.Products.Count(p => p.StoreId == x.s.Id && p.DeletedAt == null),
                db.Products.Count(p => p.StoreId == x.s.Id && p.DeletedAt == null && p.DisabledAt == null && p.Status == ProductStatus.Published),
                db.Products.Count(p => p.StoreId == x.s.Id && p.DeletedAt == null && p.DisabledAt != null)))
            .FirstOrDefaultAsync(ct);

    public async Task<PagedResult<AdminProductListItem>> ListProductsAsync(AdminProductQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var products = db.Products.AsNoTracking();

        products = query.State switch
        {
            AdminProductState.Live => products.Where(p => p.DeletedAt == null && p.DisabledAt == null && p.Status == ProductStatus.Published),
            AdminProductState.Draft => products.Where(p => p.DeletedAt == null && p.DisabledAt == null && p.Status == ProductStatus.Draft),
            AdminProductState.Archived => products.Where(p => p.DeletedAt == null && p.DisabledAt == null && p.Status == ProductStatus.Archived),
            AdminProductState.Disabled => products.Where(p => p.DeletedAt == null && p.DisabledAt != null),
            AdminProductState.Deleted => products.Where(p => p.DeletedAt != null),
            _ => products.Where(p => p.DeletedAt == null),
        };
        if (query.StoreId is { } storeId) products = products.Where(p => p.StoreId == storeId);
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            // A category shows its own products and those of its sub-categories.
            var tree = new CategoryTree(await db.ProductCategories.AsNoTracking().ToListAsync(ct).ConfigureAwait(false));
            var category = tree.All.FirstOrDefault(c => c.Slug == query.Category);
            var ids = category is null ? [] : tree.Descendants(category).Prepend(category).Select(c => c.Id).ToList();
            products = products.Where(p => ids.Contains(p.CategoryId));
        }
        if (query.MinPrice is { } min) products = products.Where(p => p.Price >= min);
        if (query.MaxPrice is { } max) products = products.Where(p => p.Price <= max);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{EscapeLike(query.Search.Trim())}%";
            products = products.Where(p => EF.Functions.ILike(p.Name, term, "\\") || EF.Functions.ILike(p.Slug, term, "\\")
                || (p.Sku != null && EF.Functions.ILike(p.Sku, term, "\\")));
        }

        var total = await products.CountAsync(ct).ConfigureAwait(false);
        var ordered = query.Sort switch
        {
            ProductSort.PriceAsc => products.OrderBy(p => p.Price),
            ProductSort.PriceDesc => products.OrderByDescending(p => p.Price),
            _ => products.OrderByDescending(p => p.CreatedAt),
        };
        var page = await ordered.ThenBy(p => p.Id).Include(p => p.Variants)
            .Skip(paging.Skip).Take(paging.PageSize)
            .ToListAsync(ct).ConfigureAwait(false);

        var storeIds = page.Select(p => p.StoreId).Distinct().ToList();
        var stores = await db.Stores.AsNoTracking().Where(s => storeIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Name, s.Slug, s.DefaultAffiliatePercent }, ct).ConfigureAwait(false);
        var categoryIds = page.Select(p => p.CategoryId).Distinct().ToList();
        var names = await db.ProductCategories.AsNoTracking().Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new LocalizedName(c.NameFr, c.NameAr, c.NameEn), ct).ConfigureAwait(false);
        var imageIds = page.Where(p => p.ImageIds.Count > 0).Select(p => p.ImageIds[0]).Distinct().ToList();
        var urls = await db.Images.AsNoTracking().Where(i => imageIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => ImageUrls.For(i.Path), ct).ConfigureAwait(false);

        var items = page.Select(p => new AdminProductListItem(
            p.Id, p.Slug, p.Name, p.ImageIds.Count > 0 ? urls.GetValueOrDefault(p.ImageIds[0]) : null,
            p.Price, ProductMapping.MinPrice(p), p.ImageIds.Count, p.Status, p.IsDisabled, p.IsDeleted, p.ModerationReason,
            names[p.CategoryId], p.StoreId, stores[p.StoreId].Name, stores[p.StoreId].Slug, p.CreatedAt,
            p.AffiliatePercent ?? stores[p.StoreId].DefaultAffiliatePercent)).ToList();
        return new PagedResult<AdminProductListItem>(items, paging.Page, paging.PageSize, total);
    }
}
