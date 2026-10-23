using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Infrastructure.Queries;

/// <summary>Product reads. Pages are small, so products are loaded with their variants and shaped in memory.</summary>
internal sealed partial class CatalogQueries
{
    public async Task<PagedResult<ProductListItem>> ListStoreProductsAsync(Guid storeId, ProductListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var products = db.Products.AsNoTracking().Where(p => p.StoreId == storeId && p.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{EscapeLike(query.Search.Trim())}%";
            products = products.Where(p => EF.Functions.ILike(p.Name, term, "\\") || (p.Sku != null && EF.Functions.ILike(p.Sku, term, "\\")));
        }
        if (query.Status is { } status) products = products.Where(p => p.Status == status);

        var total = await products.CountAsync(ct).ConfigureAwait(false);
        var page = await products.Include(p => p.Variants)
            .OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id)
            .Skip(paging.Skip).Take(paging.PageSize)
            .ToListAsync(ct).ConfigureAwait(false);

        var urls = await ImageUrlsAsync(page.Where(p => p.ImageIds.Count > 0).Select(p => p.ImageIds[0]), ct).ConfigureAwait(false);
        var categoryIds = page.Select(p => p.CategoryId).Distinct().ToList();
        var names = await db.ProductCategories.AsNoTracking().Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new LocalizedName(c.NameFr, c.NameAr, c.NameEn), ct).ConfigureAwait(false);

        var items = page.Select(p => new ProductListItem(
            p.Id, p.Slug, p.Name, p.ImageIds.Count > 0 ? urls.GetValueOrDefault(p.ImageIds[0]) : null, p.Price, ProductMapping.MinPrice(p),
            p.TotalStock, p.Variants.Count, p.Status, names[p.CategoryId], p.UpdatedAt, p.IsDisabled, p.ModerationReason)).ToList();
        return new PagedResult<ProductListItem>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<ProductDto?> GetStoreProductAsync(Guid storeId, Guid productId, CancellationToken ct = default)
    {
        var product = await db.Products.AsNoTracking().Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == productId && p.StoreId == storeId && p.DeletedAt == null, ct).ConfigureAwait(false);
        if (product is null) return null;

        var store = await db.Stores.AsNoTracking().FirstAsync(s => s.Id == storeId, ct).ConfigureAwait(false);
        var category = await db.ProductCategories.AsNoTracking().FirstAsync(c => c.Id == product.CategoryId, ct).ConfigureAwait(false);
        var urls = await ImageUrlsAsync(product.ImageIds, ct).ConfigureAwait(false);

        return new ProductDto(
            product.Id, product.Slug, product.Name,
            new ProductCategoryRef(category.Id, category.Slug, new LocalizedName(category.NameFr, category.NameAr, category.NameEn)),
            product.Description, product.Price, product.CompareAtPrice, product.Sku, product.Stock, product.TotalStock,
            product.AffiliatePercent, product.AffiliatePercent ?? store.DefaultAffiliatePercent, product.Status,
            [.. product.ImageIds.Where(urls.ContainsKey).Select(id => new ProductImageDto(id, urls[id]))],
            [.. product.Values.Select(v => new ProductValueDto(v.Key, v.Value))],
            product.VariantAxes,
            [.. product.Variants.Select(v => new ProductVariantDto(v.Id, ProductMapping.Options(v), v.Stock, v.Price, v.Sku))],
            product.CreatedAt, product.UpdatedAt, product.IsDisabled, product.ModerationReason);
    }

    public async Task<PublicProductDto?> GetPublishedProductAsync(string slug, CancellationToken ct = default)
    {
        var product = await VisibleProducts().Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Slug == slug, ct).ConfigureAwait(false);
        if (product is null) return null;

        var store = await db.Stores.AsNoTracking().FirstAsync(s => s.Id == product.StoreId, ct).ConfigureAwait(false);

        var tree = await ProductTreeAsync(ct).ConfigureAwait(false);
        var category = tree.Find(product.CategoryId);
        if (category is null) return null;

        var path = tree.PathTo(category);
        var schema = path.SelectMany(c => c.Attributes.OrderBy(a => a.SortOrder)).ToList();
        var urls = await ImageUrlsAsync([.. product.ImageIds, .. store.LogoImageId is { } logo ? new[] { logo } : []], ct).ConfigureAwait(false);

        // Fields in schema order, labels resolved; a value whose field was renamed keeps working (same key).
        var attributes = schema
            .Select(def => (def, value: product.Values.FirstOrDefault(v => v.Key == def.Key)))
            .Where(x => x.value is not null)
            .Select(x => Resolve(x.def, x.value!))
            .ToList();
        var axes = product.VariantAxes
            .Select(key => schema.FirstOrDefault(a => a.Key == key))
            .Where(a => a is not null)
            .Select(a => new PublicVariantAxisDto(a!.Key, new LocalizedName(a.NameFr, a.NameAr, a.NameEn), [.. ProductCategoryMapping.ToDto(a).Options]))
            .ToList();

        return new PublicProductDto(
            product.Id, product.Slug, product.Name, product.Description, product.Price, ProductMapping.MinPrice(product), product.CompareAtPrice,
            product.AffiliatePercent ?? store.DefaultAffiliatePercent, product.TotalStock > 0,
            [.. product.ImageIds.Where(urls.ContainsKey).Select(id => urls[id])],
            [.. path.Select(tree.Node)],
            attributes,
            axes,
            [.. product.Variants.Select(v => new PublicVariantDto(v.Id, ProductMapping.Options(v), v.Price ?? product.Price, v.Stock > 0))],
            new ProductStoreRef(store.Id, store.Name, store.Slug, store.LogoImageId is { } l ? urls.GetValueOrDefault(l) : null,
                store.Status == StoreStatus.Active),
            new RatingDto(product.AverageRating, product.RatingCount));
    }

    private static PublicAttributeDto Resolve(AttributeDefinition def, AttributeValue value)
    {
        var name = new LocalizedName(def.NameFr, def.NameAr, def.NameEn);
        var options = def.Options.Where(o => value.OptionKeys.Contains(o.Key))
            .Select(o => new AttributeOptionDto(o.Key, new LocalizedName(o.NameFr, o.NameAr, o.NameEn))).ToList();
        return def.Type switch
        {
            AttributeType.Number => new(def.Key, name, def.Type, def.Unit, null,
                decimal.Parse(value.Value, CultureInfo.InvariantCulture), null, []),
            AttributeType.Boolean => new(def.Key, name, def.Type, null, null, null, value.Value == "true", []),
            AttributeType.SingleChoice or AttributeType.MultiChoice => new(def.Key, name, def.Type, null, null, null, null, options),
            _ => new(def.Key, name, def.Type, null, value.Value, null, null, []),
        };
    }

    private async Task<Dictionary<Guid, string>> ImageUrlsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Where(id => id != Guid.Empty).Distinct().ToList();
        if (wanted.Count == 0) return [];
        return await db.Images.AsNoTracking().Where(i => wanted.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => ImageUrls.For(i.Path), ct).ConfigureAwait(false);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);
}
