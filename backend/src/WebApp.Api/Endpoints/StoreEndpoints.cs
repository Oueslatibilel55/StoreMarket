using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;

namespace WebApp.Api.Endpoints;

/// <summary>Stores: the owner's "My store" (StoreOwner role) and the public store by slug.</summary>
internal static class StoreEndpoints
{
    public static IEndpointRouteBuilder MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stores/{slug}", GetPublic).WithTags("Catalog").AllowAnonymous()
            .Produces<PublicStoreDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("A store by its URL name (active or temporarily closed)");

        app.MapGet("/api/cities", () => Governorates.All).WithTags("Catalog").AllowAnonymous()
            .WithSummary("Governorate codes for a store's city and delivery zones (labels are translated by the client)");

        var mine = app.MapGroup("/api/me/store").WithTags("My store").RequireAuthorization(AuthPolicies.StoreOwner);
        mine.MapGet("/", GetMine).Produces<MyStoreDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("My store (404 until I create it)");
        mine.MapPost("/", Create).Produces<MyStoreDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create my store — live at once, one per owner");
        mine.MapPut("/", Update).Produces<MyStoreDto>().WithSummary("Edit my store and its selling settings");
        mine.MapPost("/close", Close).Produces<MyStoreDto>().WithSummary("Close my store for a while");
        mine.MapPost("/reopen", Reopen).Produces<MyStoreDto>().WithSummary("Reopen my store");

        return app;
    }

    private static async Task<IResult> GetPublic(string slug, StoreService service, CancellationToken ct) =>
        Respond(await service.GetPublicAsync(slug, ct));

    private static async Task<IResult> GetMine(StoreService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.GetMineAsync(http.User.GetUserId()!.Value, ct));

    private static async Task<IResult> Create(SaveStoreRequest request, StoreService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.CreateAsync(http.User.GetUserId()!.Value, request, ct);
        return result.IsSuccess ? TypedResults.Created($"/api/stores/{result.Value.Slug}", result.Value) : result.ToProblem();
    }

    private static async Task<IResult> Update(SaveStoreRequest request, StoreService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.UpdateAsync(http.User.GetUserId()!.Value, request, ct));

    private static async Task<IResult> Close(StoreService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.CloseAsync(http.User.GetUserId()!.Value, ct));

    private static async Task<IResult> Reopen(StoreService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.ReopenAsync(http.User.GetUserId()!.Value, ct));

    private static IResult Respond<T>(Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
