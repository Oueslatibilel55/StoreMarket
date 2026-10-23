using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Domain.Settings;

namespace WebApp.Application.Services;

/// <summary>
/// Ratings and reviews (017). Only the client of a <b>delivered</b> store order may rate it: each product line once,
/// and the store once. The store owner answers publicly; an admin hides abusive reviews (reason required, audited).
/// Every change recomputes the product's and the store's average, count and ranking score <b>in the same
/// transaction</b>, from the visible reviews only.
/// </summary>
public sealed class ReviewService(
    IReviewRepository reviews,
    IReviewQueries queries,
    ICatalogQueries catalog,
    IUserRepository users,
    IStoreRepository stores,
    IPlatformSettings settings,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    // -- Client -------------------------------------------------------------------

    public async Task<MyReviewsDto> MineAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct).ConfigureAwait(false);
        return user is null ? new MyReviewsDto([], []) : await queries.MineAsync(userId, user.Phone, ct).ConfigureAwait(false);
    }

    public async Task<Result<MyReviewDto>> CreateAsync(Guid userId, CreateReviewRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await users.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user is null) return Result.Fail<MyReviewDto>(ReviewErrors.OrderNotFound);

        var storeOrder = await reviews.FindReviewableAsync(userId, user.Phone, request.StoreOrderId, ct).ConfigureAwait(false);
        if (storeOrder is null) return Result.Fail<MyReviewDto>(ReviewErrors.OrderNotFound);
        ReviewableLine? line = null;
        if (request.OrderLineId is { } lineId)
        {
            line = storeOrder.Lines.FirstOrDefault(l => l.OrderLineId == lineId);
            if (line is null) return Result.Fail<MyReviewDto>(ReviewErrors.OrderNotFound);
        }
        if (storeOrder.StoreOwnerId == userId) return Result.Fail<MyReviewDto>(ReviewErrors.OwnStore);
        if (storeOrder.Status != StoreOrderStatus.Delivered) return Result.Fail<MyReviewDto>(ReviewErrors.NotDelivered);

        var target = line is null ? ReviewTarget.Store : ReviewTarget.Product;
        if (await reviews.ExistsAsync(target, storeOrder.StoreOrderId, line?.OrderLineId, ct).ConfigureAwait(false))
            return Result.Fail<MyReviewDto>(ReviewErrors.AlreadyReviewed);

        var now = clock.GetUtcNow().UtcDateTime;
        var window = await settings.GetIntAsync(SettingKeys.ReviewEditWindowDays, ct).ConfigureAwait(false);
        var created = line is null
            ? Review.ForStore(userId, user.FullName, storeOrder.OrderId, storeOrder.StoreOrderId, storeOrder.StoreId,
                request.Stars, request.Comment, now, window)
            : Review.ForProduct(userId, user.FullName, storeOrder.OrderId, storeOrder.StoreOrderId, line.OrderLineId, line.ProductId,
                storeOrder.StoreId, request.Stars, request.Comment, now, window);
        if (!created.IsSuccess) return Result.Fail<MyReviewDto>(created.Error!);

        var saved = await SaveAsync(created.Value, () => reviews.Add(created.Value), audited: null, ct).ConfigureAwait(false);
        return saved.IsSuccess ? await MyDtoAsync(userId, created.Value.Id, ct).ConfigureAwait(false) : Result.Fail<MyReviewDto>(saved.Error!);
    }

    public async Task<Result<MyReviewDto>> EditAsync(Guid userId, Guid id, EditReviewRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var review = await reviews.GetAsync(id, ct).ConfigureAwait(false);
        // Someone else's review is reported as missing.
        if (review is null || review.AuthorId != userId) return Result.Fail<MyReviewDto>(ReviewErrors.NotFound);

        var edited = review.Edit(request.Stars, request.Comment, clock.GetUtcNow().UtcDateTime);
        if (!edited.IsSuccess) return Result.Fail<MyReviewDto>(edited.Error!);
        var saved = await SaveAsync(review, null, audited: null, ct).ConfigureAwait(false);
        return saved.IsSuccess ? await MyDtoAsync(userId, id, ct).ConfigureAwait(false) : Result.Fail<MyReviewDto>(saved.Error!);
    }

    // -- Public -------------------------------------------------------------------

    public async Task<Result<PublicReviewsDto>> ForProductAsync(string slug, int? stars, int? page, int? pageSize, CancellationToken ct = default) =>
        await catalog.GetProductReviewsAsync(slug, stars, page, pageSize, ct).ConfigureAwait(false) is { } dto
            ? Result.Ok(dto) : Result.Fail<PublicReviewsDto>(ProductErrors.NotFound);

    public async Task<Result<PublicReviewsDto>> ForStoreAsync(string slug, int? stars, int? page, int? pageSize, CancellationToken ct = default) =>
        await catalog.GetStoreReviewsAsync(slug, stars, page, pageSize, ct).ConfigureAwait(false) is { } dto
            ? Result.Ok(dto) : Result.Fail<PublicReviewsDto>(StoreErrors.NotFound);

    // -- Store owner ----------------------------------------------------------------

    public async Task<Result<StoreReviewsDto>> ForMyStoreAsync(Guid ownerId, StoreReviewQuery query, CancellationToken ct = default) =>
        await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false) is { } store
            ? Result.Ok(await queries.ForOwnerAsync(store.Id, query, ct).ConfigureAwait(false))
            : Result.Fail<StoreReviewsDto>(StoreErrors.NotFound);

    public Task<Result<StoreReviewDto>> ReplyAsync(Guid ownerId, Guid id, ReplyReviewRequest request, CancellationToken ct = default) =>
        OwnerStepAsync(ownerId, id, r => r.SetReply(request?.Reply, clock.GetUtcNow().UtcDateTime), ct);

    public Task<Result<StoreReviewDto>> RemoveReplyAsync(Guid ownerId, Guid id, CancellationToken ct = default) =>
        OwnerStepAsync(ownerId, id, r =>
        {
            r.RemoveReply();
            return Result.Ok();
        }, ct);

    /// <summary>A reply changes no rating: saved without recomputing.</summary>
    private async Task<Result<StoreReviewDto>> OwnerStepAsync(Guid ownerId, Guid id, Func<Review, Result> step, CancellationToken ct)
    {
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<StoreReviewDto>(StoreErrors.NotFound);
        if (store.IsDeleted) return Result.Fail<StoreReviewDto>(DomainErrors.Store.Deleted);
        var review = await reviews.GetAsync(id, ct).ConfigureAwait(false);
        if (review is null || review.StoreId != store.Id) return Result.Fail<StoreReviewDto>(ReviewErrors.NotFound);

        var done = step(review);
        if (!done.IsSuccess) return Result.Fail<StoreReviewDto>(done.Error!);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return await queries.GetForOwnerAsync(store.Id, id, ct).ConfigureAwait(false) is { } dto
            ? Result.Ok(dto) : Result.Fail<StoreReviewDto>(ReviewErrors.NotFound);
    }

    // -- Admin --------------------------------------------------------------------

    public Task<PagedResult<AdminReviewDto>> ListAsync(AdminReviewQuery query, CancellationToken ct = default) => queries.AdminListAsync(query, ct);

    public Task<AdminReviewCounts> CountsAsync(CancellationToken ct = default) => queries.AdminCountsAsync(ct);

    public Task<Result<AdminReviewDto>> HideAsync(AdminActor actor, Guid id, ModerateReviewRequest request, CancellationToken ct = default) =>
        ModerateAsync(actor, id, AuditActions.ReviewHide, (r, now) => r.Hide(request?.Reason, now), request?.Reason, ct);

    public Task<Result<AdminReviewDto>> UnhideAsync(AdminActor actor, Guid id, ModerateReviewRequest request, CancellationToken ct = default) =>
        ModerateAsync(actor, id, AuditActions.ReviewUnhide, (r, _) => r.Unhide(request?.Reason), request?.Reason, ct);

    private async Task<Result<AdminReviewDto>> ModerateAsync(AdminActor actor, Guid id, string action, Func<Review, DateTime, Result> step,
        string? reason, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var review = await reviews.GetAsync(id, ct).ConfigureAwait(false);
        if (review is null) return Result.Fail<AdminReviewDto>(ReviewErrors.NotFound);

        var before = Snapshot(review);
        var done = step(review, clock.GetUtcNow().UtcDateTime);
        if (!done.IsSuccess) return Result.Fail<AdminReviewDto>(done.Error!);

        var label = await queries.AdminGetAsync(id, ct).ConfigureAwait(false) is { } current
            ? $"{current.ProductName ?? current.StoreName} · {current.Stars}★ · {current.AuthorFullName}"
            : null;
        var saved = await SaveAsync(review, null, (actor, action, reason?.Trim(), before, label), ct).ConfigureAwait(false);
        if (!saved.IsSuccess) return Result.Fail<AdminReviewDto>(saved.Error!);
        return await queries.AdminGetAsync(id, ct).ConfigureAwait(false) is { } dto ? Result.Ok(dto) : Result.Fail<AdminReviewDto>(ReviewErrors.NotFound);
    }

    // -- Shared -------------------------------------------------------------------

    /// <summary>Saves the review change (and its audit line), then recomputes the ratings — one transaction.</summary>
    private async Task<Result<bool>> SaveAsync(Review review, Action? add,
        (AdminActor Actor, string Action, string? Reason, object Before, string? Label)? audited, CancellationToken ct)
    {
        var prior = await settings.GetIntAsync(SettingKeys.RankingMinReviewsPrior, ct).ConfigureAwait(false);
        return await unitOfWork.InTransactionAsync(async () =>
        {
            add?.Invoke();
            if (audited is { } a)
                audit.Record(a.Actor.Email, a.Actor.Id, a.Action, AuditTargets.Review, review.Id, a.Label, a.Before, Snapshot(review), a.Reason);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            await reviews.RefreshRatingsAsync(review.ProductId, review.StoreId, prior, ct).ConfigureAwait(false);
            return Result.Ok(true);
        }, ct).ConfigureAwait(false);
    }

    private static object Snapshot(Review r) => new { r.Stars, Hidden = r.IsHidden, r.HiddenReason };

    private async Task<Result<MyReviewDto>> MyDtoAsync(Guid userId, Guid id, CancellationToken ct) =>
        await queries.GetMineAsync(userId, id, ct).ConfigureAwait(false) is { } dto ? Result.Ok(dto) : Result.Fail<MyReviewDto>(ReviewErrors.NotFound);
}
