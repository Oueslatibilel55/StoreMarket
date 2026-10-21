using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Domain.Enums;

namespace WebApp.Infrastructure.Queries;

/// <summary>What the cart (013) needs to know about its products, read fresh each time.</summary>
internal sealed partial class CatalogQueries
{
    public async Task<IReadOnlyList<CartProductInfo>> GetCartProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        if (productIds.Count == 0) return [];

        // "Can be ordered" is exactly what the shop shows: same rule, same place (VisibleProducts).
        var visible = (await VisibleProducts().Where(p => productIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct).ConfigureAwait(false))
            .ToHashSet();
        var products = await db.Products.AsNoTracking().Include(p => p.Variants)
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(ct).ConfigureAwait(false);
        if (products.Count == 0) return [];

        var storeIds = products.Select(p => p.StoreId).Distinct().ToList();
        var stores = await db.Stores.AsNoTracking().Where(s => storeIds.Contains(s.Id)).ToListAsync(ct).ConfigureAwait(false);
        var urls = await ImageUrlsAsync(
            [.. products.Where(p => p.ImageIds.Count > 0).Select(p => p.ImageIds[0]), .. stores.Where(s => s.LogoImageId is not null).Select(s => s.LogoImageId!.Value)],
            ct).ConfigureAwait(false);
        var tree = products.Any(p => p.HasVariants) ? await ProductTreeAsync(ct).ConfigureAwait(false) : null;

        var storeInfo = stores.ToDictionary(s => s.Id, s => new CartStoreInfo(
            s.Id, s.Name, s.Slug, s.LogoImageId is { } logo ? urls.GetValueOrDefault(logo) : null,
            s.Status == StoreStatus.Active, s.DeliveryFee, [.. s.DeliveryCities]));

        return [.. products.Select(p =>
        {
            // Variant option labels come from the fields of the category and its ancestors (by stable keys).
            var category = tree?.Find(p.CategoryId);
            var schema = category is null ? [] : tree!.PathTo(category).SelectMany(c => c.Attributes).ToList();
            var variants = p.Variants.Select(v => new CartVariantInfo(v.Id, v.Price ?? p.Price, v.Stock,
                [.. p.VariantAxes.Select(axis =>
                {
                    var def = schema.FirstOrDefault(a => a.Key == axis);
                    var key = v.Selection.FirstOrDefault(s => s.Key == axis)?.Value;
                    var option = def?.Options.FirstOrDefault(o => o.Key == key);
                    return new CartOptionLabel(
                        def is null ? new LocalizedName(axis, null, null) : new LocalizedName(def.NameFr, def.NameAr, def.NameEn),
                        option is null ? new LocalizedName(key ?? "?", null, null) : new LocalizedName(option.NameFr, option.NameAr, option.NameEn));
                })])).ToList();

            return new CartProductInfo(
                p.Id, p.Name, p.Slug, p.ImageIds.Count > 0 ? urls.GetValueOrDefault(p.ImageIds[0]) : null,
                p.Price, p.Stock, p.HasVariants, visible.Contains(p.Id), storeInfo[p.StoreId], variants);
        })];
    }
}
