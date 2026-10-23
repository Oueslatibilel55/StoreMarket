using WebApp.Application.DTOs;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.Interfaces;

/// <summary>A store order the user may rate, with its lines — what <c>ReviewService</c> checks a new review against.</summary>
public sealed record ReviewableStoreOrder(
    Guid OrderId, Guid StoreOrderId, Guid StoreId, Guid StoreOwnerId, StoreOrderStatus Status, IReadOnlyList<ReviewableLine> Lines);

public sealed record ReviewableLine(Guid OrderLineId, Guid ProductId);

public interface IReviewRepository
{
    /// <summary>
    /// The store order if it belongs to the user: an order they placed signed in, or — guest or affiliate-placed
    /// orders — one whose client phone is the account's phone (and that they did not place as an affiliate).
    /// </summary>
    Task<ReviewableStoreOrder?> FindReviewableAsync(Guid userId, string userPhone, Guid storeOrderId, CancellationToken ct = default);
    Task<bool> ExistsAsync(ReviewTarget target, Guid storeOrderId, Guid? orderLineId, CancellationToken ct = default);
    Task<Review?> GetAsync(Guid id, CancellationToken ct = default);
    void Add(Review review);

    /// <summary>
    /// Recomputes average, count and ranking score of the product (when given) and the store from their visible
    /// reviews. Locks those rows first, so concurrent reviews of the same item are counted one after the other.
    /// Call it inside <see cref="IUnitOfWork.InTransactionAsync{T}"/>, after the review change is saved.
    /// </summary>
    Task RefreshRatingsAsync(Guid? productId, Guid storeId, int minReviewsPrior, CancellationToken ct = default);
}

/// <summary>Read-only review views (no tracking, projections).</summary>
public interface IReviewQueries
{
    Task<MyReviewsDto> MineAsync(Guid userId, string userPhone, CancellationToken ct = default);
    Task<MyReviewDto?> GetMineAsync(Guid userId, Guid reviewId, CancellationToken ct = default);

    Task<StoreReviewsDto> ForOwnerAsync(Guid storeId, StoreReviewQuery query, CancellationToken ct = default);
    Task<StoreReviewDto?> GetForOwnerAsync(Guid storeId, Guid reviewId, CancellationToken ct = default);

    Task<PagedResult<AdminReviewDto>> AdminListAsync(AdminReviewQuery query, CancellationToken ct = default);
    Task<AdminReviewCounts> AdminCountsAsync(CancellationToken ct = default);
    Task<AdminReviewDto?> AdminGetAsync(Guid reviewId, CancellationToken ct = default);
}
