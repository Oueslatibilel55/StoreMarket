using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;

namespace WebApp.Api.Endpoints;

/// <summary>Product category tree and attribute schemas: public reads, admin management (audited).</summary>
internal static class ProductCategoryEndpoints
{
    public static IEndpointRouteBuilder MapProductCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/api/product-categories").WithTags("Catalog").AllowAnonymous();
        shop.MapGet("/", ListVisible).Produces<IReadOnlyList<ProductCategoryNode>>()
            .WithSummary("Visible tree, depth-first (a hidden category hides its branch)");
        shop.MapGet("/{slug}/schema", Schema).Produces<CategorySchemaDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Attributes a product of this category fills: inherited ones first, then its own");

        var admin = app.MapGroup("/api/admin/product-categories").WithTags("Admin · Product categories")
            .RequireAuthorization(AuthPolicies.Admin);
        admin.MapGet("/", ListAll).Produces<IReadOnlyList<AdminProductCategoryDto>>().WithSummary("Whole tree with own attributes");
        admin.MapPost("/", Create).Produces<AdminProductCategoryDto>(StatusCodes.Status201Created).WithSummary("Create (parentId null = top level)");
        admin.MapPut("/{id:guid}", Update).Produces<AdminProductCategoryDto>().WithSummary("Rename / move under another parent");
        admin.MapPost("/{id:guid}/activate", Activate).Produces<AdminProductCategoryDto>();
        admin.MapPost("/{id:guid}/deactivate", Deactivate).Produces<AdminProductCategoryDto>().WithSummary("Hide (with its branch)");
        admin.MapPost("/{id:guid}/move", Move).Produces<AdminProductCategoryDto>().WithSummary("Move among sisters { direction: up | down }");
        admin.MapDelete("/{id:guid}", Delete).Produces(StatusCodes.Status204NoContent).WithSummary("Delete a category without sub-categories");

        admin.MapPost("/{id:guid}/attributes", AddAttribute).Produces<AdminProductCategoryDto>(StatusCodes.Status201Created);
        admin.MapPut("/{id:guid}/attributes/{attributeId:guid}", UpdateAttribute).Produces<AdminProductCategoryDto>()
            .WithSummary("Edit names, unit, required, filterable, options (the type can't change)");
        admin.MapPost("/{id:guid}/attributes/{attributeId:guid}/move", MoveAttribute).Produces<AdminProductCategoryDto>();
        admin.MapDelete("/{id:guid}/attributes/{attributeId:guid}", DeleteAttribute).Produces<AdminProductCategoryDto>();

        return app;
    }

    private static Task<IReadOnlyList<ProductCategoryNode>> ListVisible(ProductCategoryService service, CancellationToken ct) =>
        service.ListVisibleAsync(ct);

    private static async Task<IResult> Schema(string slug, ProductCategoryService service, CancellationToken ct) =>
        Respond(await service.GetSchemaAsync(slug, ct));

    private static Task<IReadOnlyList<AdminProductCategoryDto>> ListAll(ProductCategoryService service, CancellationToken ct) =>
        service.ListAllAsync(ct);

    private static async Task<IResult> Create(SaveProductCategoryRequest request, ProductCategoryService service, HttpContext http, CancellationToken ct) =>
        Created(await service.CreateAsync(http.User.GetAdminActor(), request, ct));

    private static async Task<IResult> Update(Guid id, SaveProductCategoryRequest request, ProductCategoryService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.UpdateAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> Activate(Guid id, ProductCategoryService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.ActivateAsync(http.User.GetAdminActor(), id, ct));

    private static async Task<IResult> Deactivate(Guid id, ProductCategoryService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.DeactivateAsync(http.User.GetAdminActor(), id, ct));

    private static async Task<IResult> Move(Guid id, MoveRequest request, ProductCategoryService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.MoveAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> Delete(Guid id, ProductCategoryService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.DeleteAsync(http.User.GetAdminActor(), id, ct);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblem();
    }

    private static async Task<IResult> AddAttribute(Guid id, SaveAttributeRequest request, ProductCategoryService service, HttpContext http, CancellationToken ct) =>
        Created(await service.AddAttributeAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> UpdateAttribute(Guid id, Guid attributeId, SaveAttributeRequest request, ProductCategoryService service,
        HttpContext http, CancellationToken ct) =>
        Respond(await service.UpdateAttributeAsync(http.User.GetAdminActor(), id, attributeId, request, ct));

    private static async Task<IResult> MoveAttribute(Guid id, Guid attributeId, MoveRequest request, ProductCategoryService service,
        HttpContext http, CancellationToken ct) =>
        Respond(await service.MoveAttributeAsync(http.User.GetAdminActor(), id, attributeId, request, ct));

    private static async Task<IResult> DeleteAttribute(Guid id, Guid attributeId, ProductCategoryService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.DeleteAttributeAsync(http.User.GetAdminActor(), id, attributeId, ct));

    private static IResult Created(Result<AdminProductCategoryDto> result) =>
        result.IsSuccess ? TypedResults.Created($"/api/admin/product-categories/{result.Value.Id}", result.Value) : result.ToProblem();

    private static IResult Respond<T>(Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
