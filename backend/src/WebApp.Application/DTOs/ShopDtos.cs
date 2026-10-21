namespace WebApp.Application.DTOs;

public enum ProductSort
{
    Newest = 1,
    PriceAsc = 2,
    PriceDesc = 3,
}

/// <summary>Public product list: by product category (sub-categories included), store, text; paged.</summary>
public sealed record ShopProductQuery(string? Category, string? Store, string? Search, ProductSort? Sort, int? Page, int? PageSize);

public sealed record ShopStoreQuery(string? Category, string? City, string? Search, int? Page, int? PageSize);

/// <summary>A product tile in the shop. <c>MinPrice</c> &lt; <c>Price</c> when a variant is cheaper ("from …").</summary>
public sealed record ProductCardDto(
    Guid Id, string Slug, string Name, string? ImageUrl, decimal Price, decimal MinPrice, decimal? CompareAtPrice, bool HasVariants,
    bool InStock, string StoreName, string StoreSlug, bool StoreOpen);

/// <summary>A store tile in the shop.</summary>
public sealed record StoreCardDto(
    Guid Id, string Slug, string Name, string? LogoUrl, string? BannerUrl, StoreCategoryRef Category, string City, bool IsOpen,
    int ProductCount, decimal DeliveryFee);
