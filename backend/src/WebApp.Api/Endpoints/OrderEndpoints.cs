using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;

namespace WebApp.Api.Endpoints;

/// <summary>
/// Checkout and orders (014): place a cash-on-delivery order (signed in or guest, or an affiliate for a client),
/// "My orders", a guest's lookup by reference + phone, and the affiliate's "Orders with my code".
/// </summary>
internal static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/orders", Place).WithTags("Orders").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Cart)
            .Produces<OrderDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Place a cash-on-delivery order from my cart (or a guest's browser cart); ForClient = affiliate mode");
        app.MapGet("/api/orders/lookup", Lookup).WithTags("Orders").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Cart)
            .Produces<OrderDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("A guest finds their order with its reference and the phone given at checkout");

        var mine = app.MapGroup("/api/me/orders").WithTags("Orders").RequireAuthorization();
        mine.MapGet("/", ListMine).Produces<PagedResult<OrderListItem>>().WithSummary("Orders I placed (as a client or for clients), newest first");
        mine.MapGet("/{reference}", GetMine).Produces<OrderDto>().ProducesProblem(StatusCodes.Status404NotFound).WithSummary("One of my orders");

        app.MapGet("/api/me/affiliate/orders", ListForAffiliate).WithTags("Affiliate").RequireAuthorization(AuthPolicies.Affiliate)
            .Produces<AffiliateOrdersDto>().WithSummary("Products sold with my code: sale, %, expected coins, status");

        return app;
    }

    private static async Task<IResult> Place(CheckoutRequest request, CheckoutService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.PlaceAsync(http.User.GetUserId(), request, ct);
        return result.IsSuccess ? TypedResults.Created($"/api/me/orders/{result.Value.Reference}", result.Value) : result.ToProblem();
    }

    private static async Task<IResult> Lookup(string? reference, string? phone, CheckoutService service, CancellationToken ct) =>
        Respond(await service.LookupAsync(reference, phone, ct));

    private static Task<PagedResult<OrderListItem>> ListMine(int? page, int? pageSize, CheckoutService service, HttpContext http, CancellationToken ct) =>
        service.ListMineAsync(http.User.GetUserId()!.Value, new OrderListQuery(page, pageSize), ct);

    private static async Task<IResult> GetMine(string reference, CheckoutService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.GetMineAsync(http.User.GetUserId()!.Value, reference.ToUpperInvariant(), ct));

    private static Task<AffiliateOrdersDto> ListForAffiliate(int? page, int? pageSize, CheckoutService service, HttpContext http, CancellationToken ct) =>
        service.ListForAffiliateAsync(http.User.GetUserId()!.Value, new OrderListQuery(page, pageSize), ct);

    private static IResult Respond(Result<OrderDto> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
