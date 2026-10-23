using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

/// <summary>Average and number of visible reviews, shown on shop cards and pages (017). <c>Average</c> is null when none.</summary>
public sealed record RatingDto(decimal? Average, int Count);

/// <summary>Average, count and how many reviews gave 1★ … 5★ (<c>Stars[0]</c> = 1★).</summary>
public sealed record ReviewStatsDto(decimal? Average, int Count, IReadOnlyList<int> Stars);

/// <summary>A review as visitors see it: first name and initial only.</summary>
public sealed record PublicReviewDto(
    Guid Id, string AuthorName, int Stars, string? Comment, DateTime CreatedAt, DateTime? EditedAt, string? Reply, DateTime? RepliedAt);

public sealed record PublicReviewsDto(ReviewStatsDto Stats, PagedResult<PublicReviewDto> Reviews);

// -- Client ---------------------------------------------------------------------

/// <summary>Something the client received and has not rated yet: a product (order line) or a store (store order).</summary>
public sealed record ReviewToWriteDto(
    ReviewTarget Target,
    string OrderReference,
    Guid StoreOrderId,
    Guid? OrderLineId,
    string StoreName,
    string? StoreSlug,
    string? ProductName,
    string? ProductSlug,
    string? ImageUrl,
    DateTime DeliveredAt);

public sealed record MyReviewDto(
    Guid Id,
    ReviewTarget Target,
    string OrderReference,
    Guid StoreOrderId,
    Guid? OrderLineId,
    string StoreName,
    string? StoreSlug,
    string? ProductName,
    string? ProductSlug,
    string? ImageUrl,
    int Stars,
    string? Comment,
    DateTime CreatedAt,
    DateTime? EditedAt,
    DateTime EditableUntil,
    bool CanEdit,
    string? Reply,
    DateTime? RepliedAt,
    bool Hidden,
    string? HiddenReason);

public sealed record MyReviewsDto(IReadOnlyList<ReviewToWriteDto> ToWrite, IReadOnlyList<MyReviewDto> Written);

/// <summary>With <c>OrderLineId</c>: rates that product; without: rates the store of <c>StoreOrderId</c>.</summary>
public sealed record CreateReviewRequest(Guid StoreOrderId, Guid? OrderLineId, int Stars, string? Comment);

public sealed record EditReviewRequest(int Stars, string? Comment);

// -- Store owner ------------------------------------------------------------------

public sealed record StoreReviewQuery(ReviewTarget? Target, int? Stars, bool? Replied, int? Page, int? PageSize);

public sealed record StoreReviewDto(
    Guid Id,
    ReviewTarget Target,
    string AuthorName,
    string OrderReference,
    Guid StoreOrderId,
    string? ProductName,
    string? ProductSlug,
    int Stars,
    string? Comment,
    DateTime CreatedAt,
    DateTime? EditedAt,
    string? Reply,
    DateTime? RepliedAt,
    bool Hidden,
    string? HiddenReason);

/// <summary>The owner's reviews screen: store and product stats, then the reviews (hidden ones included, flagged).</summary>
public sealed record StoreReviewsDto(ReviewStatsDto Store, ReviewStatsDto Products, int Unanswered, PagedResult<StoreReviewDto> Reviews);

public sealed record ReplyReviewRequest(string? Reply);

// -- Admin ------------------------------------------------------------------------

public sealed record AdminReviewQuery(ReviewTarget? Target, bool? Hidden, int? Stars, Guid? StoreId, string? Search, int? Page, int? PageSize);

public sealed record AdminReviewDto(
    Guid Id,
    ReviewTarget Target,
    Guid AuthorId,
    string AuthorFullName,
    string AuthorEmail,
    Guid StoreId,
    string StoreName,
    string StoreSlug,
    Guid? ProductId,
    string? ProductName,
    string? ProductSlug,
    string OrderReference,
    int Stars,
    string? Comment,
    DateTime CreatedAt,
    DateTime? EditedAt,
    string? Reply,
    DateTime? RepliedAt,
    DateTime? HiddenAt,
    string? HiddenReason);

public sealed record AdminReviewCounts(int Total, int Hidden, int LowStars);

public sealed record ModerateReviewRequest(string? Reason);
