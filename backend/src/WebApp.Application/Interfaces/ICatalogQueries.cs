using WebApp.Application.DTOs;

namespace WebApp.Application.Interfaces;

/// <summary>Read-only queries for the public shop (no tracking, projections only).</summary>
public interface ICatalogQueries
{
    Task<IReadOnlyList<StoreCategoryDto>> ListActiveStoreCategoriesAsync(CancellationToken ct = default);
    /// <summary>An active or closed store by slug (disabled / suspended ones are hidden), or null.</summary>
    Task<PublicStoreDto?> GetStoreBySlugAsync(string slug, CancellationToken ct = default);
    Task<MyStoreDto?> GetStoreOfOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductCategoryNode>> ListVisibleProductCategoriesAsync(CancellationToken ct = default);
    /// <summary>Schema of a visible category by slug (inherited attributes first), or null.</summary>
    Task<CategorySchemaDto?> GetProductCategorySchemaAsync(string slug, CancellationToken ct = default);
    Task<PagedResult<ProductListItem>> ListStoreProductsAsync(Guid storeId, ProductListQuery query, CancellationToken ct = default);
    Task<ProductDto?> GetStoreProductAsync(Guid storeId, Guid productId, CancellationToken ct = default);
    /// <summary>A published, not deleted product of an active or closed store, with labels resolved — or null.</summary>
    Task<PublicProductDto?> GetPublishedProductAsync(string slug, CancellationToken ct = default);
    /// <summary>Published products of active or closed stores; null when the category or store slug is unknown.</summary>
    Task<PagedResult<ProductCardDto>?> ListShopProductsAsync(ShopProductQuery query, CancellationToken ct = default);
    Task<PagedResult<StoreCardDto>> ListShopStoresAsync(ShopStoreQuery query, CancellationToken ct = default);
    /// <summary>The store of a product clients can order right now (same visibility as the shop), or null.</summary>
    Task<ProductStoreInfo?> GetVisibleProductStoreAsync(Guid productId, CancellationToken ct = default);
    /// <summary>Whether the store is shown in the shop (active or closed, not deleted).</summary>
    Task<bool> IsStoreVisibleAsync(Guid storeId, CancellationToken ct = default);
    /// <summary>The cart's products — also removed or hidden ones, flagged with <c>Visible = false</c> (013).</summary>
    Task<IReadOnlyList<CartProductInfo>> GetCartProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
}
