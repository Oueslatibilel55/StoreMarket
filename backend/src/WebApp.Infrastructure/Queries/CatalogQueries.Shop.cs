using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using WebApp.Application.Common;
using WebApp.Application.DTOs;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;

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

    private IQueryable<Store> VisibleStores() =>
        db.Stores.AsNoTracking().Where(s => s.Status == StoreStatus.Active || s.Status == StoreStatus.Closed);

    /// <summary>
    /// A filterable choice / yes-no field (018): the product holds one of the wanted values — in its own values
    /// (a multi-choice is stored <c>a,b</c>) or, for a variant axis, in one of its variants.
    /// </summary>
    private static IQueryable<Product> WhereAttribute(IQueryable<Product> products, AttributeDefinition field, AttributeFilter filter)
    {
        if (!field.Filterable) return products;
        var key = field.Key;
        var wanted = field.Type switch
        {
            AttributeType.SingleChoice or AttributeType.MultiChoice => filter.Values.Where(v => field.Options.Any(o => o.Key == v)).ToList(),
            AttributeType.Boolean => filter.Values.Where(v => v is "true" or "false").ToList(),
            _ => [],
        };
        if (wanted.Count == 0) return products; // nothing valid: the filter is ignored, like an unknown field

        return products.Where(p =>
            p.Values.Any(v => v.Key == key && wanted.Any(w => ("," + v.Value + ",").Contains("," + w + ",")))
            || p.Variants.Any(variant => variant.Selection.Any(s => s.Key == key && wanted.Contains(s.Value))));
    }

    public Task<ProductStoreInfo?> GetVisibleProductStoreAsync(Guid productId, CancellationToken ct = default) =>
        VisibleProducts()
            .Where(p => p.Id == productId)
            .Join(db.Stores, p => p.StoreId, s => s.Id, (p, s) =>
                new ProductStoreInfo(p.Id, s.Id, s.OwnerId, p.AffiliatePercent ?? s.DefaultAffiliatePercent))
            .FirstOrDefaultAsync(ct);

    public Task<bool> IsStoreVisibleAsync(Guid storeId, CancellationToken ct = default) =>
        db.Stores.AnyAsync(s => s.Id == storeId && (s.Status == StoreStatus.Active || s.Status == StoreStatus.Closed), ct);

    /// <summary>Stores matched by a search whose products are found too (by store name); more would make the query slow.</summary>
    private const int MaxStoresMatchedBySearch = 100;

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

            // Fields of the category and its ancestors are shared by every product below it.
            var fields = tree.PathTo(category).SelectMany(c => c.Attributes).ToDictionary(a => a.Key, StringComparer.Ordinal);
            foreach (var filter in AttributeFilter.Parse(query.Attributes))
                if (fields.TryGetValue(filter.Key, out var field)) products = WhereAttribute(products, field, filter);
        }
        if (!string.IsNullOrWhiteSpace(query.Store))
        {
            var storeId = await db.Stores.AsNoTracking().Where(s => s.Slug == query.Store).Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (storeId is null) return null;
            products = products.Where(p => p.StoreId == storeId);
        }
        if (!string.IsNullOrWhiteSpace(query.StoreCategory))
        {
            var categoryId = await db.StoreCategories.AsNoTracking().Where(c => c.Slug == query.StoreCategory && c.IsActive)
                .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (categoryId is null) return null;
            products = products.Where(p => db.Stores.Any(s => s.Id == p.StoreId && s.CategoryId == categoryId));
        }
        if (query.MinPrice is { } min) products = products.Where(p => p.FromPrice >= min);
        if (query.MaxPrice is { } max) products = products.Where(p => p.FromPrice <= max);
        if (query.MinRating is { } stars)
        {
            var atLeast = (decimal)Math.Clamp(stars, Ranking.MinStars, Ranking.MaxStars);
            products = products.Where(p => p.AverageRating >= atLeast);
        }

        var search = SearchText.ToTsQuery(query.Search);
        if (search is not null)
        {
            // Products of a store whose name matches are found too, with a relevance of 0 on their own text.
            var matchingStores = await VisibleStores().Where(FullTextSearch.Matches<Store>(search))
                .Select(s => s.Id).Take(MaxStoresMatchedBySearch).ToListAsync(ct).ConfigureAwait(false);
            products = products.Where(p => matchingStores.Contains(p.StoreId)
                || EF.Property<NpgsqlTsVector>(p, FullTextSearch.Vector).Matches(EF.Functions.ToTsQuery(FullTextSearch.Config, FullTextSearch.Unaccent(search))));
        }

        var total = await products.CountAsync(ct).ConfigureAwait(false);
        // Searching sorts by rating first unless another order is asked for; relevance then breaks the ties.
        var sort = query.Sort ?? (search is null ? ProductSort.Newest : ProductSort.TopRated);
        var ordered = sort switch
        {
            ProductSort.PriceAsc => products.OrderBy(p => p.FromPrice),
            ProductSort.PriceDesc => products.OrderByDescending(p => p.FromPrice),
            // Unrated products (null score) after every rated one.
            ProductSort.TopRated => products.OrderBy(p => p.RankingScore == null).ThenByDescending(p => p.RankingScore),
            _ => products.OrderByDescending(p => p.CreatedAt),
        };
        if (search is not null)
            ordered = ordered.ThenByDescending(FullTextSearch.Rank<Product>(search));
        var page = await ordered.ThenByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Include(p => p.Variants)
            .Skip(paging.Skip).Take(paging.PageSize)
            .ToListAsync(ct).ConfigureAwait(false);

        var storeIds = page.Select(p => p.StoreId).Distinct().ToList();
        var stores = await db.Stores.AsNoTracking().Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct).ConfigureAwait(false);
        var urls = await ImageUrlsAsync(page.Where(p => p.ImageIds.Count > 0).Select(p => p.ImageIds[0]), ct).ConfigureAwait(false);

        var items = page.Select(p => new ProductCardDto(
            p.Id, p.Slug, p.Name, p.ImageIds.Count > 0 ? urls.GetValueOrDefault(p.ImageIds[0]) : null, p.Price, ProductMapping.MinPrice(p),
            p.CompareAtPrice, p.HasVariants, p.TotalStock > 0, stores[p.StoreId].Name, stores[p.StoreId].Slug,
            stores[p.StoreId].Status == StoreStatus.Active, new RatingDto(p.AverageRating, p.RatingCount))).ToList();
        return new PagedResult<ProductCardDto>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<PagedResult<StoreCardDto>> ListShopStoresAsync(ShopStoreQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var stores = VisibleStores();

        if (!string.IsNullOrWhiteSpace(query.Category))
            stores = stores.Where(s => db.StoreCategories.Any(c => c.Id == s.CategoryId && c.Slug == query.Category));
        if (!string.IsNullOrWhiteSpace(query.City)) stores = stores.Where(s => s.City == query.City);
        var search = SearchText.ToTsQuery(query.Search);
        if (search is not null)
            stores = stores.Where(FullTextSearch.Matches<Store>(search));

        var total = await stores.CountAsync(ct).ConfigureAwait(false);
        // Open stores first; then the best rated when asked — the default when searching — then the most relevant.
        var topRated = (query.Sort ?? (search is null ? StoreSort.Name : StoreSort.TopRated)) == StoreSort.TopRated;
        var ordered = stores
            .OrderBy(s => s.Status == StoreStatus.Active ? 0 : 1)
            .ThenBy(s => topRated && s.RankingScore == null)
            .ThenByDescending(s => topRated ? s.RankingScore : null);
        if (search is not null)
            ordered = ordered.ThenByDescending(FullTextSearch.Rank<Store>(search));
        var page = await ordered
            .ThenBy(s => s.Name).ThenBy(s => s.Id)
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
            x.Store.City, x.Store.Status == StoreStatus.Active, x.Products, x.Store.DeliveryFee,
            new RatingDto(x.Store.AverageRating, x.Store.RatingCount))).ToList();
        return new PagedResult<StoreCardDto>(items, paging.Page, paging.PageSize, total);
    }
}
