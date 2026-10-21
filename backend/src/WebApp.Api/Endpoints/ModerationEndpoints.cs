using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Api.Endpoints;

/// <summary>Admin moderation of stores and products (010). Every change takes a reason (except enable) and is audited.</summary>
internal static class ModerationEndpoints
{
    public static IEndpointRouteBuilder MapModerationEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(AuthPolicies.Admin);

        var stores = admin.MapGroup("/stores").WithTags("Admin · Stores");
        stores.MapGet("/", ListStores).Produces<PagedResult<AdminStoreListItem>>()
            .WithSummary("Every store: search (name, owner), filter by status / category / city / creation date, deleted ones apart");
        stores.MapGet("/{id:guid}", GetStore).Produces<AdminStoreDto>().WithSummary("Store detail: configuration, owner, moderation state");
        stores.MapPost("/{id:guid}/disable", DisableStore).Produces<AdminStoreDto>()
            .WithSummary("Disable a store (reason shown to the owner): it and its products leave the shop");
        stores.MapPost("/{id:guid}/enable", EnableStore).Produces<AdminStoreDto>()
            .WithSummary("Re-enable a disabled store (its owner must still have the StoreOwner role)");
        stores.MapPost("/{id:guid}/delete", DeleteStore).Produces<AdminStoreDto>()
            .WithSummary("Soft-delete a store (final, kept for order history)");

        var products = admin.MapGroup("/products").WithTags("Admin · Products");
        products.MapGet("/", ListProducts).Produces<PagedResult<AdminProductListItem>>()
            .WithSummary("Products of every store: filter by store / category / price / state, sort by price or newest");
        products.MapPost("/disable", DisableProducts).Produces<BulkModerationResult>().WithSummary("Disable several products at once");
        products.MapPost("/{id:guid}/disable", DisableProduct).Produces(StatusCodes.Status204NoContent)
            .WithSummary("Disable a product (reason shown to the owner)");
        products.MapPost("/{id:guid}/enable", EnableProduct).Produces(StatusCodes.Status204NoContent).WithSummary("Re-enable a product");
        products.MapPost("/{id:guid}/delete", DeleteProduct).Produces(StatusCodes.Status204NoContent)
            .WithSummary("Soft-delete a product (final, kept for order history)");

        return app;
    }

    private static Task<PagedResult<AdminStoreListItem>> ListStores(
        string? search, StoreStatus? status, string? category, string? city, bool? deleted, DateTime? createdFrom, DateTime? createdTo,
        int? page, int? pageSize, ModerationService service, CancellationToken ct) =>
        service.ListStoresAsync(new AdminStoreQuery(search, status, category, city, deleted, createdFrom, createdTo, page, pageSize), ct);

    private static async Task<IResult> GetStore(Guid id, ModerationService service, CancellationToken ct) =>
        Respond(await service.GetStoreAsync(id, ct));

    private static async Task<IResult> DisableStore(Guid id, ModerationRequest request, ModerationService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.DisableStoreAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> EnableStore(Guid id, ModerationService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.EnableStoreAsync(http.User.GetAdminActor(), id, ct));

    private static async Task<IResult> DeleteStore(Guid id, ModerationRequest request, ModerationService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.DeleteStoreAsync(http.User.GetAdminActor(), id, request, ct));

    private static Task<PagedResult<AdminProductListItem>> ListProducts(
        string? search, Guid? storeId, string? category, decimal? minPrice, decimal? maxPrice, AdminProductState? state, ProductSort? sort,
        int? page, int? pageSize, ModerationService service, CancellationToken ct) =>
        service.ListProductsAsync(new AdminProductQuery(search, storeId, category, minPrice, maxPrice, state, sort, page, pageSize), ct);

    private static async Task<IResult> DisableProducts(BulkModerationRequest request, ModerationService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.DisableProductsAsync(http.User.GetAdminActor(), request, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }

    private static async Task<IResult> DisableProduct(Guid id, ModerationRequest request, ModerationService service, HttpContext http, CancellationToken ct) =>
        NoContent(await service.DisableProductAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> EnableProduct(Guid id, ModerationService service, HttpContext http, CancellationToken ct) =>
        NoContent(await service.EnableProductAsync(http.User.GetAdminActor(), id, ct));

    private static async Task<IResult> DeleteProduct(Guid id, ModerationRequest request, ModerationService service, HttpContext http, CancellationToken ct) =>
        NoContent(await service.DeleteProductAsync(http.User.GetAdminActor(), id, request, ct));

    private static IResult Respond(Result<AdminStoreDto> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();

    private static IResult NoContent(Result result) => result.IsSuccess ? TypedResults.NoContent() : result.ToProblem();
}
