using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Api.Endpoints;

/// <summary>
/// Order lifecycle (015 / 016): the store processes its orders (<c>/api/me/store/orders</c>), the client cancels
/// before shipping, an admin sees every order and can force a status (audited).
/// </summary>
internal static class OrderLifecycleEndpoints
{
    public static IEndpointRouteBuilder MapOrderLifecycleEndpoints(this IEndpointRouteBuilder app)
    {
        var store = app.MapGroup("/api/me/store/orders").WithTags("My store").RequireAuthorization(AuthPolicies.StoreOwner);
        store.MapGet("/", ListForStore).Produces<PagedResult<StoreOrderListItem>>()
            .WithSummary("My store's orders, newest first; filter by status, with / without affiliate code, search");
        store.MapGet("/counts", CountForStore).Produces<StoreOrderCounts>().WithSummary("My store's orders per status");
        store.MapGet("/{id:guid}", GetForStore).Produces<StoreOrderDetailDto>().WithSummary("One order: client, address, lines, history, next steps");
        store.MapPost("/{id:guid}/status", ChangeAsStore).Produces<StoreOrderDetailDto>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Confirm, ship, deliver (cash collected), not delivered (reason) or cancel (reason)");

        app.MapPost("/api/me/orders/{reference}/store-orders/{id:guid}/cancel", CancelAsClient).WithTags("Orders").RequireAuthorization()
            .Produces<OrderDto>().ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Cancel my order with one store, before it ships");

        var admin = app.MapGroup("/api/admin").WithTags("Admin · Orders").RequireAuthorization(AuthPolicies.Admin);
        admin.MapGet("/orders", AdminList).Produces<PagedResult<AdminOrderListItem>>()
            .WithSummary("Every order; filter by status, store, affiliate, client, product, from / to (placed or delivered), search");
        admin.MapGet("/orders/{reference}", AdminGet).Produces<OrderDto>().WithSummary("One order with every store's history");
        admin.MapPost("/store-orders/{id:guid}/status", AdminForce).Produces<OrderDto>()
            .WithSummary("Force a store order's status — reason required, audited; Delivered stays final");

        return app;
    }

    private static Guid UserId(HttpContext http) => http.User.GetUserId()!.Value;

    private static async Task<IResult> ListForStore(StoreOrderStatus? status, bool? withAffiliate, string? search, int? page, int? pageSize,
        OrderLifecycleService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.ListForMyStoreAsync(UserId(http), new StoreOrderQuery(status, withAffiliate, search, page, pageSize), ct));

    private static async Task<IResult> CountForStore(OrderLifecycleService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.CountForMyStoreAsync(UserId(http), ct));

    private static async Task<IResult> GetForStore(Guid id, OrderLifecycleService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.GetForMyStoreAsync(UserId(http), id, ct));

    private static async Task<IResult> ChangeAsStore(Guid id, ChangeStoreOrderStatusRequest request, OrderLifecycleService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.ChangeAsStoreAsync(UserId(http), id, request, ct));

    private static async Task<IResult> CancelAsClient(string reference, Guid id, CancelStoreOrderRequest? request, OrderLifecycleService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.CancelAsClientAsync(UserId(http), reference, id, request ?? new CancelStoreOrderRequest(null), ct));

    private static Task<PagedResult<AdminOrderListItem>> AdminList([AsParameters] AdminOrderRequest r, OrderLifecycleService service, CancellationToken ct) =>
        service.ListAsync(new AdminOrderQuery(r.Status, r.Search, r.StoreId, r.Page, r.PageSize,
            r.AffiliateId, r.ClientId, r.ProductId, r.From, r.To, r.DateField), ct);

    /// <summary>Query string of <c>GET /api/admin/orders</c>; <c>from</c> / <c>to</c> are inclusive days on <c>dateField</c> (Placed by default).</summary>
    internal sealed record AdminOrderRequest(
        StoreOrderStatus? Status, string? Search, Guid? StoreId, Guid? AffiliateId, Guid? ClientId, Guid? ProductId,
        DateOnly? From, DateOnly? To, OrderDateField? DateField, int? Page, int? PageSize);

    private static async Task<IResult> AdminGet(string reference, OrderLifecycleService service, CancellationToken ct) =>
        Respond(await service.GetAsync(reference, ct));

    private static async Task<IResult> AdminForce(Guid id, ChangeStoreOrderStatusRequest request, OrderLifecycleService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.ForceAsync(http.User.GetAdminActor(), id, request, ct));

    private static IResult Respond<T>(Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
