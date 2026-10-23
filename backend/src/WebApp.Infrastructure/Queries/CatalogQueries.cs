using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Application.Interfaces;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Queries;

internal sealed partial class CatalogQueries(AppDbContext db) : ICatalogQueries
{
    public async Task<IReadOnlyList<StoreCategoryDto>> ListActiveStoreCategoriesAsync(CancellationToken ct = default) =>
        await db.StoreCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
            .Select(c => new StoreCategoryDto(c.Id, c.Slug, new LocalizedName(c.NameFr, c.NameAr, c.NameEn), c.Icon,
                db.Images.Where(i => i.Id == c.ImageId).Select(i => ImageUrls.RequestPath + "/" + i.Path).FirstOrDefault()))
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<ProductCategoryNode>> ListVisibleProductCategoriesAsync(CancellationToken ct = default)
    {
        var tree = await ProductTreeAsync(ct).ConfigureAwait(false);
        // Depth-first, sisters in order: the client can render the list as an indented tree.
        return [.. Walk(tree, parentId: null).Where(tree.IsVisible).Select(tree.Node)];
    }

    public async Task<CategorySchemaDto?> GetProductCategorySchemaAsync(string slug, CancellationToken ct = default)
    {
        var tree = await ProductTreeAsync(ct).ConfigureAwait(false);
        var category = tree.All.FirstOrDefault(c => c.Slug == slug);
        if (category is null || !tree.IsVisible(category)) return null;

        var path = tree.PathTo(category);
        var attributes = path
            .SelectMany(c => c.Attributes.OrderBy(a => a.SortOrder)
                .Select(a => new SchemaAttributeDto(ProductCategoryMapping.ToDto(a), c.Id, c.Slug, ProductCategoryMapping.Name(c))))
            .ToList();
        return new CategorySchemaDto(tree.Node(category), [.. path.Select(tree.Node)], attributes);
    }

    private async Task<CategoryTree> ProductTreeAsync(CancellationToken ct) =>
        new(await db.ProductCategories.AsNoTracking().Include(c => c.Attributes).ToListAsync(ct).ConfigureAwait(false));

    internal static IEnumerable<WebApp.Domain.Entities.ProductCategory> Walk(CategoryTree tree, Guid? parentId)
    {
        foreach (var child in tree.Children(parentId))
        {
            yield return child;
            foreach (var d in Walk(tree, child.Id)) yield return d;
        }
    }

    public Task<PublicStoreDto?> GetStoreBySlugAsync(string slug, CancellationToken ct = default) =>
        db.Stores.AsNoTracking()
            .Where(s => s.Slug == slug && (s.Status == StoreStatus.Active || s.Status == StoreStatus.Closed))
            .Join(db.StoreCategories, s => s.CategoryId, c => c.Id, (s, c) => new PublicStoreDto(
                s.Id, s.Name, s.Slug, new StoreCategoryRef(c.Id, c.Slug, new LocalizedName(c.NameFr, c.NameAr, c.NameEn)),
                s.Description, s.Phone, s.City, s.Address,
                db.Images.Where(i => i.Id == s.LogoImageId).Select(i => ImageUrls.RequestPath + "/" + i.Path).FirstOrDefault(),
                db.Images.Where(i => i.Id == s.BannerImageId).Select(i => ImageUrls.RequestPath + "/" + i.Path).FirstOrDefault(),
                s.Status == StoreStatus.Active, s.DefaultAffiliatePercent, s.DeliveryFee, s.DeliveryCities,
                s.DeliveryMinDays, s.DeliveryMaxDays, s.FacebookUrl, s.InstagramUrl, s.WebsiteUrl, new RatingDto(s.AverageRating, s.RatingCount)))
            .FirstOrDefaultAsync(ct);

    public Task<MyStoreDto?> GetStoreOfOwnerAsync(Guid ownerId, CancellationToken ct = default) =>
        db.Stores.AsNoTracking()
            .Where(s => s.OwnerId == ownerId)
            .Join(db.StoreCategories, s => s.CategoryId, c => c.Id, (s, c) => new MyStoreDto(
                s.Id, s.Name, s.Slug, new StoreCategoryRef(c.Id, c.Slug, new LocalizedName(c.NameFr, c.NameAr, c.NameEn)),
                s.Description, s.Phone, s.City, s.Address,
                s.LogoImageId,
                db.Images.Where(i => i.Id == s.LogoImageId).Select(i => ImageUrls.RequestPath + "/" + i.Path).FirstOrDefault(),
                s.BannerImageId,
                db.Images.Where(i => i.Id == s.BannerImageId).Select(i => ImageUrls.RequestPath + "/" + i.Path).FirstOrDefault(),
                s.Status, s.DefaultAffiliatePercent, s.DeliveryFee, s.DeliveryCities, s.DeliveryMinDays, s.DeliveryMaxDays,
                s.FacebookUrl, s.InstagramUrl, s.WebsiteUrl, s.SubscriptionMonthlyPrice, s.CreatedAt,
                s.ModerationReason, s.DeletedAt != null))
            .FirstOrDefaultAsync(ct);
}
