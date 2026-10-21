using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;

namespace WebApp.Api.Endpoints;

/// <summary>Store categories: public list of active ones, admin management (audited).</summary>
internal static class StoreCategoryEndpoints
{
    public static IEndpointRouteBuilder MapStoreCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/store-categories", ListActive).WithTags("Catalog").AllowAnonymous()
            .Produces<IReadOnlyList<StoreCategoryDto>>().WithSummary("Active store categories in display order");

        var admin = app.MapGroup("/api/admin/store-categories").WithTags("Admin · Store categories")
            .RequireAuthorization(AuthPolicies.Admin);
        admin.MapGet("/", ListAll).Produces<IReadOnlyList<AdminStoreCategoryDto>>().WithSummary("Every category, active or not, in display order");
        admin.MapPost("/", Create).Produces<AdminStoreCategoryDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create a category (placed last; slug made from the French name when empty)");
        admin.MapPut("/{id:guid}", Update).Produces<AdminStoreCategoryDto>().WithSummary("Rename / change slug or icon");
        admin.MapPost("/{id:guid}/activate", Activate).Produces<AdminStoreCategoryDto>().WithSummary("Show the category in the shop");
        admin.MapPost("/{id:guid}/deactivate", Deactivate).Produces<AdminStoreCategoryDto>().WithSummary("Hide the category from the shop");
        admin.MapPost("/{id:guid}/move", Move).Produces<AdminStoreCategoryDto>().WithSummary("Move one place { direction: up | down }");
        admin.MapDelete("/{id:guid}", Delete).Produces(StatusCodes.Status204NoContent).WithSummary("Delete a category");

        return app;
    }

    private static Task<IReadOnlyList<StoreCategoryDto>> ListActive(StoreCategoryService service, CancellationToken ct) => service.ListActiveAsync(ct);

    private static Task<IReadOnlyList<AdminStoreCategoryDto>> ListAll(StoreCategoryService service, CancellationToken ct) => service.ListAllAsync(ct);

    private static async Task<IResult> Create(SaveStoreCategoryRequest request, StoreCategoryService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.CreateAsync(http.User.GetAdminActor(), request, ct);
        return result.IsSuccess ? TypedResults.Created($"/api/admin/store-categories/{result.Value.Id}", result.Value) : result.ToProblem();
    }

    private static async Task<IResult> Update(Guid id, SaveStoreCategoryRequest request, StoreCategoryService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.UpdateAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> Activate(Guid id, StoreCategoryService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.ActivateAsync(http.User.GetAdminActor(), id, ct));

    private static async Task<IResult> Deactivate(Guid id, StoreCategoryService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.DeactivateAsync(http.User.GetAdminActor(), id, ct));

    private static async Task<IResult> Move(Guid id, MoveStoreCategoryRequest request, StoreCategoryService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.MoveAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> Delete(Guid id, StoreCategoryService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.DeleteAsync(http.User.GetAdminActor(), id, ct);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblem();
    }

    private static IResult Respond(Result<AdminStoreCategoryDto> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
