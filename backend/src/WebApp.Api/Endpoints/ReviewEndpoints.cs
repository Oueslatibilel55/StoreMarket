using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Api.Endpoints;

/// <summary>
/// Ratings and reviews (017): public reviews on product and store pages, the client rates what was delivered to them
/// (<c>/api/me/reviews</c>), the store owner answers (<c>/api/me/store/reviews</c>), the admin hides abusive ones
/// (<c>/api/admin/reviews</c>, audited).
/// </summary>
internal static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/api").WithTags("Catalog").AllowAnonymous();
        shop.MapGet("/products/{slug}/reviews", ProductReviews).Produces<PublicReviewsDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Visible reviews of a product, newest first, with average and 1★…5★ counts: ?stars= &page=");
        shop.MapGet("/stores/{slug}/reviews", StoreReviews).Produces<PublicReviewsDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Visible reviews of a store's service, newest first, with average and 1★…5★ counts: ?stars= &page=");

        var mine = app.MapGroup("/api/me/reviews").WithTags("Reviews").RequireAuthorization();
        mine.MapGet("/", Mine).Produces<MyReviewsDto>().WithSummary("Delivered products and stores I can still rate, and my reviews");
        mine.MapPost("/", Create).Produces<MyReviewDto>(StatusCodes.Status201Created)
            .WithSummary("Rate a delivered product (orderLineId) or its store (no orderLineId): 1–5 stars, optional comment");
        mine.MapPut("/{id:guid}", Edit).Produces<MyReviewDto>().WithSummary("Change my review within the edit window");

        var store = app.MapGroup("/api/me/store/reviews").WithTags("Store").RequireAuthorization(AuthPolicies.StoreOwner);
        store.MapGet("/", ForMyStore).Produces<StoreReviewsDto>()
            .WithSummary("Reviews of my store and products (hidden ones flagged): ?target= &stars= &replied= &page=");
        store.MapPut("/{id:guid}/reply", Reply).Produces<StoreReviewDto>().WithSummary("Answer a review publicly (or change the answer)");
        store.MapDelete("/{id:guid}/reply", RemoveReply).Produces<StoreReviewDto>().WithSummary("Remove my answer");

        var admin = app.MapGroup("/api/admin/reviews").WithTags("Admin · Reviews").RequireAuthorization(AuthPolicies.Admin);
        admin.MapGet("/", List).Produces<PagedResult<AdminReviewDto>>()
            .WithSummary("Every review: ?target= &hidden= &stars= &storeId= &search= (comment, author, store, product, order) &page=");
        admin.MapGet("/counts", Counts).Produces<AdminReviewCounts>().WithSummary("Total, hidden and visible 1–2★ reviews");
        admin.MapPost("/{id:guid}/hide", Hide).Produces<AdminReviewDto>().WithSummary("Hide an abusive review (reason required); left out of averages");
        admin.MapPost("/{id:guid}/unhide", Unhide).Produces<AdminReviewDto>().WithSummary("Show a hidden review again (reason required)");

        return app;
    }

    private static async Task<IResult> ProductReviews(string slug, int? stars, int? page, int? pageSize, ReviewService service, CancellationToken ct) =>
        Ok(await service.ForProductAsync(slug, stars, page, pageSize, ct));

    private static async Task<IResult> StoreReviews(string slug, int? stars, int? page, int? pageSize, ReviewService service, CancellationToken ct) =>
        Ok(await service.ForStoreAsync(slug, stars, page, pageSize, ct));

    private static Task<MyReviewsDto> Mine(ReviewService service, HttpContext http, CancellationToken ct) =>
        service.MineAsync(http.User.GetUserId()!.Value, ct);

    private static async Task<IResult> Create(CreateReviewRequest request, ReviewService service, HttpContext http, CancellationToken ct)
    {
        Result<MyReviewDto> result = await service.CreateAsync(http.User.GetUserId()!.Value, request, ct);
        return result.IsSuccess ? TypedResults.Created($"/api/me/reviews/{result.Value.Id}", result.Value) : result.ToProblem();
    }

    private static async Task<IResult> Edit(Guid id, EditReviewRequest request, ReviewService service, HttpContext http, CancellationToken ct) =>
        Ok(await service.EditAsync(http.User.GetUserId()!.Value, id, request, ct));

    private static async Task<IResult> ForMyStore(ReviewTarget? target, int? stars, bool? replied, int? page, int? pageSize,
        ReviewService service, HttpContext http, CancellationToken ct) =>
        Ok(await service.ForMyStoreAsync(http.User.GetUserId()!.Value, new StoreReviewQuery(target, stars, replied, page, pageSize), ct));

    private static async Task<IResult> Reply(Guid id, ReplyReviewRequest request, ReviewService service, HttpContext http, CancellationToken ct) =>
        Ok(await service.ReplyAsync(http.User.GetUserId()!.Value, id, request, ct));

    private static async Task<IResult> RemoveReply(Guid id, ReviewService service, HttpContext http, CancellationToken ct) =>
        Ok(await service.RemoveReplyAsync(http.User.GetUserId()!.Value, id, ct));

    private static Task<PagedResult<AdminReviewDto>> List(ReviewTarget? target, bool? hidden, int? stars, Guid? storeId, string? search,
        int? page, int? pageSize, ReviewService service, CancellationToken ct) =>
        service.ListAsync(new AdminReviewQuery(target, hidden, stars, storeId, search, page, pageSize), ct);

    private static Task<AdminReviewCounts> Counts(ReviewService service, CancellationToken ct) => service.CountsAsync(ct);

    private static async Task<IResult> Hide(Guid id, ModerateReviewRequest request, ReviewService service, HttpContext http, CancellationToken ct) =>
        Ok(await service.HideAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> Unhide(Guid id, ModerateReviewRequest request, ReviewService service, HttpContext http, CancellationToken ct) =>
        Ok(await service.UnhideAsync(http.User.GetAdminActor(), id, request, ct));

    private static IResult Ok<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
