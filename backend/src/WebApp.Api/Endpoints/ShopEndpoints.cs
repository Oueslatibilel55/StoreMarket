using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;

namespace WebApp.Api.Endpoints;

/// <summary>Public shop lists (no account needed). Single store / product pages are in Store- / ProductEndpoints.</summary>
internal static class ShopEndpoints
{
    public static IEndpointRouteBuilder MapShopEndpoints(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/api").WithTags("Catalog").AllowAnonymous();

        shop.MapGet("/products", Products).Produces<PagedResult<ProductCardDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Published products: ?category= (product category slug, sub-categories included) &store= &storeCategory= &search= (full text) " +
                "&minPrice= &maxPrice= &minRating= &attr=field:choice,choice (filterable fields of the category, repeatable) " +
                "&sort=Newest|PriceAsc|PriceDesc|TopRated (TopRated by default when searching) &page=");
        shop.MapGet("/stores", Stores).Produces<PagedResult<StoreCardDto>>()
            .WithSummary("Active and closed stores, open ones first, then ?sort=TopRated (default when searching): ?category= (store category slug) &city= &search= (full text) &page=");

        return app;
    }

    private static async Task<IResult> Products([AsParameters] ShopProductRequest request, ICatalogQueries catalog, CancellationToken ct) =>
        await catalog.ListShopProductsAsync(new ShopProductQuery(
                request.Category, request.Store, request.Search, request.Sort, request.Page, request.PageSize,
                request.StoreCategory, request.MinPrice, request.MaxPrice, request.MinRating, request.Attr), ct) is { } result
            ? TypedResults.Ok(result)
            : Result.Fail(ProductErrors.NotFound).ToProblem();

    /// <summary>Query string of <c>GET /api/products</c>; <c>attr</c> repeats once per field.</summary>
    internal sealed record ShopProductRequest(
        string? Category, string? Store, string? StoreCategory, string? Search, decimal? MinPrice, decimal? MaxPrice, int? MinRating,
        string[]? Attr, ProductSort? Sort, int? Page, int? PageSize);

    private static async Task<PagedResult<StoreCardDto>> Stores(string? category, string? city, string? search, StoreSort? sort, int? page, int? pageSize,
        ICatalogQueries catalog, CancellationToken ct) =>
        await catalog.ListShopStoresAsync(new ShopStoreQuery(category, city, search, sort, page, pageSize), ct);
}
