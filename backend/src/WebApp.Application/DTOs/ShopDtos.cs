namespace WebApp.Application.DTOs;

public enum ProductSort
{
    Newest = 1,
    PriceAsc = 2,
    PriceDesc = 3,
    /// <summary>Ranking score (Bayesian average, 017) first; unrated products last, newest first. The default when searching (018).</summary>
    TopRated = 4,
}

/// <summary>
/// Public product list, paged: by product category (sub-categories included), store, store category, full-text
/// search (018), "from" price range, minimum average rating and filterable fields of the category
/// (<c>Attributes</c> = <c>field:choice,choice</c>, see <see cref="Common.AttributeFilter"/>).
/// </summary>
public sealed record ShopProductQuery(
    string? Category, string? Store, string? Search, ProductSort? Sort, int? Page, int? PageSize,
    string? StoreCategory = null, decimal? MinPrice = null, decimal? MaxPrice = null, int? MinRating = null,
    IReadOnlyList<string>? Attributes = null);

public enum StoreSort
{
    /// <summary>Open stores first, then by name.</summary>
    Name = 1,
    /// <summary>Ranking score (Bayesian average, 017) first; unrated stores last.</summary>
    TopRated = 2,
}

public sealed record ShopStoreQuery(string? Category, string? City, string? Search, StoreSort? Sort, int? Page, int? PageSize);

/// <summary>A product tile in the shop. <c>MinPrice</c> &lt; <c>Price</c> when a variant is cheaper ("from …").</summary>
public sealed record ProductCardDto(
    Guid Id, string Slug, string Name, string? ImageUrl, decimal Price, decimal MinPrice, decimal? CompareAtPrice, bool HasVariants,
    bool InStock, string StoreName, string StoreSlug, bool StoreOpen, RatingDto Rating);

/// <summary>A store tile in the shop.</summary>
public sealed record StoreCardDto(
    Guid Id, string Slug, string Name, string? LogoUrl, string? BannerUrl, StoreCategoryRef Category, string City, bool IsOpen,
    int ProductCount, decimal DeliveryFee, RatingDto Rating);
