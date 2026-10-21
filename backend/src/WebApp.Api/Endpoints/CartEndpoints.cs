using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;

namespace WebApp.Api.Endpoints;

/// <summary>
/// The cart (013): <c>/api/me/cart</c> for a signed-in user (kept on the server), <c>/api/cart/preview</c> for a
/// guest's browser cart. Calls that check affiliate codes are rate limited (no brute-forcing codes).
/// </summary>
internal static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var mine = app.MapGroup("/api/me/cart").WithTags("Cart").RequireAuthorization();
        mine.MapGet("/", Get).Produces<CartDto>().WithSummary("My cart, grouped by store, prices / stock / codes checked now");
        mine.MapDelete("/", Clear).Produces<CartDto>().WithSummary("Empty my cart");
        mine.MapPost("/lines", Add).RequireRateLimiting(RateLimitPolicies.Cart)
            .Produces<CartDto>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Add a product (variant required when it has variants), optionally with an affiliate code");
        mine.MapPatch("/lines/{id:guid}", Update).Produces<CartDto>().WithSummary("Change a line's quantity");
        mine.MapDelete("/lines/{id:guid}", Remove).Produces<CartDto>().WithSummary("Remove a line");
        mine.MapPut("/lines/{id:guid}/code", SetCode).RequireRateLimiting(RateLimitPolicies.Cart)
            .Produces<CartDto>().WithSummary("Set or change a line's affiliate code (checked on that product's store)");
        mine.MapDelete("/lines/{id:guid}/code", RemoveCode).Produces<CartDto>().WithSummary("Remove a line's affiliate code");
        mine.MapPost("/merge", Merge).RequireRateLimiting(RateLimitPolicies.Cart)
            .Produces<CartDto>().WithSummary("Bring in the cart built as a guest (at sign-in)");

        app.MapPost("/api/cart/preview", Preview).WithTags("Cart").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Cart)
            .Produces<CartDto>().WithSummary("Show a guest's browser cart like a server one");

        return app;
    }

    private static Guid UserId(HttpContext http) => http.User.GetUserId()!.Value;

    private static Task<CartDto> Get(CartService service, HttpContext http, CancellationToken ct) =>
        service.GetAsync(UserId(http), ct);

    private static Task<CartDto> Clear(CartService service, HttpContext http, CancellationToken ct) =>
        service.ClearAsync(UserId(http), ct);

    private static async Task<IResult> Add(AddCartLineRequest request, CartService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.AddAsync(UserId(http), request, ct));

    private static async Task<IResult> Update(Guid id, UpdateCartLineRequest request, CartService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.UpdateQuantityAsync(UserId(http), id, request, ct));

    private static async Task<IResult> Remove(Guid id, CartService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.RemoveAsync(UserId(http), id, ct));

    private static async Task<IResult> SetCode(Guid id, SetCartCodeRequest request, CartService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.SetCodeAsync(UserId(http), id, request, ct));

    private static async Task<IResult> RemoveCode(Guid id, CartService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.SetCodeAsync(UserId(http), id, new SetCartCodeRequest(null), ct));

    private static Task<CartDto> Merge(GuestCartRequest request, CartService service, HttpContext http, CancellationToken ct) =>
        service.MergeAsync(UserId(http), request, ct);

    private static Task<CartDto> Preview(GuestCartRequest request, CartService service, HttpContext http, CancellationToken ct) =>
        service.PreviewAsync(request, http.User.GetUserId(), ct);

    private static IResult Respond(Result<CartDto> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
