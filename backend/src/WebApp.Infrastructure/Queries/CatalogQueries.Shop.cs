using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Infrastructure.Queries;

/// <summary>Public shop lists. Filtering, sorting and paging run in SQL; the page is then shaped in memory.</summary>
internal sealed partial class CatalogQueries
{
    /// <summary>
    /// What a visitor may see: published, not deleted, not disabled by an admin, in an active or (temporarily)
    /// closed store — a disabled or deleted store (010) is neither.
    /// </summary>
    private IQueryable<Product> VisibleProducts() =>
        db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Published && p.DeletedAt == null && p.DisabledAt == null)
            .Where(p => db.Stores.Any(s => s.Id == p.StoreId && (s.Status == StoreStatus.Active || s.Status == StoreStatus.Closed)));

    public Task<ProductStoreInfo?> GetVisibleProductStoreAsync(Guid productId, CancellationToken ct = default) =>
        VisibleProducts()
            .Where(p => p.Id == productId)
            .Join(db.Stores, p => p.StoreId, s => s.Id, (p, s) =>
                new ProductStoreInfo(p.Id, s.Id, s.OwnerId, p.AffiliatePercent ?? s.DefaultAffiliatePercent))
            .FirstOrDefaultAsync(ct);

    public Task<bool> IsStoreVisibleAsync(Guid storeId, CancellationToken ct = default) =>
        db.Stores.AnyAsync(s => s.Id == storeId && (s.Status == StoreStatus.Active || s.Status == StoreStatus.Closed), ct);

    public async Task<PagedResult<ProductCardDto>?> ListShopProductsAsync(ShopProductQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var products = VisibleProducts();

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            // A category shows its own products and those of its sub-categories; a hidden branch shows nothing.
            var tree = await ProductTreeAsync(ct).ConfigureAwait(false);
            var category = tree.All.FirstOrDefault(c => c.Slug == query.Category);
            if (category is null || !tree.IsVisible(category)) return null;
            var ids = tree.Descendants(category).Prepend(category).Select(c => c.Id).ToList();
            products = products.Where(p => ids.Contains(p.CategoryId));
        }
        if (!string.IsNullOrWhiteSpace(query.Store))
        {
            var storeId = await db.Stores.AsNoTracking().Where(s => s.Slug == query.Store).Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (storeId is null) return null;
            products = products.Where(p => p.StoreId == storeId);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{EscapeLike(query.Search.Trim())}%";
            products = products.Where(p => EF.Functions.ILike(p.Name, term, "\\"));
        }

        var total = await products.CountAsync(ct).ConfigureAwait(false);
        // "From" price: the cheapest variant when there are variants.
        var ordered = query.Sort switch
        {
            ProductSort.PriceAsc => products.OrderBy(p => p.Variants.Any() ? p.Variants.Min(v => v.Price ?? p.Price) : p.Price),
            ProductSort.PriceDesc => products.OrderByDescending(p => p.Variants.Any() ? p.Variants.Min(v => v.Price ?? p.Price) : p.Price),
            _ => products.OrderByDescending(p => p.CreatedAt),
        };
        var page = await ordered.ThenBy(p => p.Id).Include(p => p.Variants)
            .Skip(paging.Skip).Take(paging.PageSize)
            .ToListAsync(ct).ConfigureAwait(false);

        var storeIds = page.Select(p => p.StoreId).Distinct().ToList();
        var stores = await db.Stores.AsNoTracking().Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct).ConfigureAwait(false);
        var urls = await ImageUrlsAsync(page.Where(p => p.ImageIds.Count > 0).Select(p => p.ImageIds[0]), ct).ConfigureAwait(false);

        var items = page.Select(p => new ProductCardDto(
            p.Id, p.Slug, p.Name, p.ImageIds.Count > 0 ? urls.GetValueOrDefault(p.ImageIds[0]) : null, p.Price, ProductMapping.MinPrice(p),
            p.CompareAtPrice, p.HasVariants, p.TotalStock > 0, stores[p.StoreId].Name, stores[p.StoreId].Slug,
            stores[p.StoreId].Status == StoreStatus.Active)).ToList();
        return new PagedResult<ProductCardDto>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<PagedResult<StoreCardDto>> ListShopStoresAsync(ShopStoreQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var stores = db.Stores.AsNoTracking().Where(s => s.Status == StoreStatus.Active || s.Status == StoreStatus.Closed);

        if (!string.IsNullOrWhiteSpace(query.Category))
            stores = stores.Where(s => db.StoreCategories.Any(c => c.Id == s.CategoryId && c.Slug == query.Category));
        if (!string.IsNullOrWhiteSpace(query.City)) stores = stores.Where(s => s.City == query.City);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{EscapeLike(query.Search.Trim())}%";
            stores = stores.Where(s => EF.Functions.ILike(s.Name, term, "\\"));
        }

        var total = await stores.CountAsync(ct).ConfigureAwait(false);
        var page = await stores
            .OrderBy(s => s.Status == StoreStatus.Active ? 0 : 1).ThenBy(s => s.Name).ThenBy(s => s.Id) // open stores first
            .Skip(paging.Skip).Take(paging.PageSize)
            .Select(s => new
            {
                Store = s,
                Category = db.StoreCategories.First(c => c.Id == s.CategoryId),
                Products = db.Products.Count(p => p.StoreId == s.Id && p.Status == ProductStatus.Published && p.DeletedAt == null && p.DisabledAt == null),
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var urls = await ImageUrlsAsync(page.SelectMany(x => new[] { x.Store.LogoImageId, x.Store.BannerImageId })
            .Where(id => id is not null).Select(id => id!.Value), ct).ConfigureAwait(false);

        var items = page.Select(x => new StoreCardDto(
            x.Store.Id, x.Store.Slug, x.Store.Name,
            x.Store.LogoImageId is { } logo ? urls.GetValueOrDefault(logo) : null,
            x.Store.BannerImageId is { } banner ? urls.GetValueOrDefault(banner) : null,
            new StoreCategoryRef(x.Category.Id, x.Category.Slug, new LocalizedName(x.Category.NameFr, x.Category.NameAr, x.Category.NameEn)),
            x.Store.City, x.Store.Status == StoreStatus.Active, x.Products, x.Store.DeliveryFee)).ToList();
        return new PagedResult<StoreCardDto>(items, paging.Page, paging.PageSize, total);
    }
}
