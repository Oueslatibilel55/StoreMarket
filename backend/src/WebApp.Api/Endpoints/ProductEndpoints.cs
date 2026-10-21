using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Api.Endpoints;

/// <summary>Products: the owner's catalogue (StoreOwner, their own store only) and the public product by slug.</summary>
internal static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{slug}", GetPublic).WithTags("Catalog").AllowAnonymous()
            .Produces<PublicProductDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("A published product (labels resolved, variants, store) of an active or closed store");

        var mine = app.MapGroup("/api/me/store/products").WithTags("My store").RequireAuthorization(AuthPolicies.StoreOwner);
        mine.MapGet("/", List).Produces<PagedResult<ProductListItem>>().WithSummary("My products: search name / SKU, filter by status, paged");
        mine.MapGet("/{id:guid}", Get).Produces<ProductDto>().ProducesProblem(StatusCodes.Status404NotFound);
        mine.MapPost("/", Create).Produces<ProductDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Add a product (values checked against the category fields, own photos only)");
        mine.MapPut("/{id:guid}", Update).Produces<ProductDto>().WithSummary("Edit a product (variants keep their id when they still exist)");
        mine.MapDelete("/{id:guid}", Delete).Produces(StatusCodes.Status204NoContent).WithSummary("Delete (kept for past orders)");

        return app;
    }

    private static async Task<IResult> GetPublic(string slug, ProductService service, CancellationToken ct) =>
        Respond(await service.GetPublicAsync(slug, ct));

    private static async Task<IResult> List(string? search, ProductStatus? status, int? page, int? pageSize, ProductService service,
        HttpContext http, CancellationToken ct) =>
        Respond(await service.ListMineAsync(http.User.GetUserId()!.Value, new ProductListQuery(search, status, page, pageSize), ct));

    private static async Task<IResult> Get(Guid id, ProductService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.GetMineAsync(http.User.GetUserId()!.Value, id, ct));

    private static async Task<IResult> Create(SaveProductRequest request, ProductService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.CreateAsync(http.User.GetUserId()!.Value, request, ct);
        return result.IsSuccess ? TypedResults.Created($"/api/me/store/products/{result.Value.Id}", result.Value) : result.ToProblem();
    }

    private static async Task<IResult> Update(Guid id, SaveProductRequest request, ProductService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.UpdateAsync(http.User.GetUserId()!.Value, id, request, ct));

    private static async Task<IResult> Delete(Guid id, ProductService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.DeleteAsync(http.User.GetUserId()!.Value, id, ct);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblem();
    }

    private static IResult Respond<T>(Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
