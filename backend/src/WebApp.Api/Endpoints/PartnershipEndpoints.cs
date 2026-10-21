using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Api.Endpoints;

/// <summary>
/// Affiliate ↔ store partnerships (012): the affiliate asks (<c>/api/me/partnerships</c>), only the store
/// owner decides (<c>/api/me/store/partnerships</c>), an admin can end one for fraud (audited). Plus the
/// affiliate code check used by the cart and checkout.
/// </summary>
internal static class PartnershipEndpoints
{
    public static IEndpointRouteBuilder MapPartnershipEndpoints(this IEndpointRouteBuilder app)
    {
        var mine = app.MapGroup("/api/me/partnerships").WithTags("Affiliate").RequireAuthorization(AuthPolicies.Affiliate);
        mine.MapGet("/", ListMine).Produces<IReadOnlyList<AffiliatePartnershipDto>>().WithSummary("My stores: every request and partnership, newest first");
        mine.MapPost("/", Request).Produces<AffiliatePartnershipDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Ask to work with a store (one open request or partnership per store)");
        mine.MapPost("/{id:guid}/cancel", Cancel).Produces<AffiliatePartnershipDto>().WithSummary("Withdraw my request before the owner decides");

        var store = app.MapGroup("/api/me/store/partnerships").WithTags("My store").RequireAuthorization(AuthPolicies.StoreOwner);
        store.MapGet("/", ListForStore).Produces<IReadOnlyList<StorePartnershipDto>>()
            .WithSummary("Affiliates of my store: waiting requests first, then partners and history");
        store.MapPost("/{id:guid}/accept", Accept).Produces<StorePartnershipDto>().WithSummary("Accept a request");
        store.MapPost("/{id:guid}/refuse", Refuse).Produces<StorePartnershipDto>().WithSummary("Refuse a request (optional reason shown to the affiliate)");
        store.MapPost("/{id:guid}/end", End).Produces<StorePartnershipDto>().WithSummary("Stop working with an affiliate (future orders only)");

        var admin = app.MapGroup("/api/admin/partnerships").WithTags("Admin · Partnerships").RequireAuthorization(AuthPolicies.Admin);
        admin.MapGet("/", AdminList).Produces<PagedResult<AdminPartnershipItem>>()
            .WithSummary("Every partnership, filter by status / store / affiliate, search, paged");
        admin.MapPost("/{id:guid}/end", AdminEnd).Produces<AdminPartnershipItem>().WithSummary("End a partnership (fraud) — reason required, audited");

        app.MapPost("/api/affiliate-codes/check", Check).WithTags("Catalog").AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.AffiliateCode)
            .Produces<AffiliateCodeCheckDto>().ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Is this affiliate code valid on this product? (cart and checkout)");

        return app;
    }

    private static async Task<IReadOnlyList<AffiliatePartnershipDto>> ListMine(PartnershipService service, HttpContext http, CancellationToken ct) =>
        await service.ListMineAsync(http.User.GetUserId()!.Value, ct);

    private static async Task<IResult> Request(CreatePartnershipRequest request, PartnershipService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.RequestAsync(http.User.GetUserId()!.Value, request, ct);
        return result.IsSuccess ? TypedResults.Created($"/api/me/partnerships/{result.Value.Id}", result.Value) : result.ToProblem();
    }

    private static async Task<IResult> Cancel(Guid id, PartnershipService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.CancelAsync(http.User.GetUserId()!.Value, id, ct));

    private static async Task<IResult> ListForStore(PartnershipStatus? status, PartnershipService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.ListForMyStoreAsync(http.User.GetUserId()!.Value, status, ct));

    private static async Task<IResult> Accept(Guid id, PartnershipService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.AcceptAsync(http.User.GetUserId()!.Value, id, ct));

    private static async Task<IResult> Refuse(Guid id, PartnershipReasonRequest? body, PartnershipService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.RefuseAsync(http.User.GetUserId()!.Value, id, body ?? new PartnershipReasonRequest(null), ct));

    private static async Task<IResult> End(Guid id, PartnershipReasonRequest? body, PartnershipService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.EndAsync(http.User.GetUserId()!.Value, id, body ?? new PartnershipReasonRequest(null), ct));

    private static Task<PagedResult<AdminPartnershipItem>> AdminList(
        PartnershipStatus? status, string? search, Guid? storeId, Guid? affiliateId, int? page, int? pageSize,
        PartnershipService service, CancellationToken ct) =>
        service.ListAsync(new AdminPartnershipQuery(status, search, storeId, affiliateId, page, pageSize), ct);

    private static async Task<IResult> AdminEnd(Guid id, PartnershipReasonRequest body, PartnershipService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.AdminEndAsync(http.User.GetAdminActor(), id, body, ct));

    private static async Task<IResult> Check(CheckAffiliateCodeRequest request, AffiliateCodeService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.ValidateAsync(request.Code, request.ProductId, http.User.GetUserId(), ct));

    private static IResult Respond<T>(Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
