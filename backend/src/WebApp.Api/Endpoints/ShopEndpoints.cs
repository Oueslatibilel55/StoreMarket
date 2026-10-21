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
            .WithSummary("Published products: ?category= (product category slug, sub-categories included) &store= &search= &sort=Newest|PriceAsc|PriceDesc &page=");
        shop.MapGet("/stores", Stores).Produces<PagedResult<StoreCardDto>>()
            .WithSummary("Active and closed stores (open ones first): ?category= (store category slug) &city= &search= &page=");

        return app;
    }

    private static async Task<IResult> Products(string? category, string? store, string? search, ProductSort? sort, int? page, int? pageSize,
        ICatalogQueries catalog, CancellationToken ct) =>
        await catalog.ListShopProductsAsync(new ShopProductQuery(category, store, search, sort, page, pageSize), ct) is { } result
            ? TypedResults.Ok(result)
            : Result.Fail(ProductErrors.NotFound).ToProblem();

    private static async Task<PagedResult<StoreCardDto>> Stores(string? category, string? city, string? search, int? page, int? pageSize,
        ICatalogQueries catalog, CancellationToken ct) =>
        await catalog.ListShopStoresAsync(new ShopStoreQuery(category, city, search, page, pageSize), ct);
}
