using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

/// <summary>Move a store order. <c>Reason</c>: required to cancel (store) and for every admin change; <c>NotDeliveredReason</c> for NotDelivered.</summary>
public sealed record ChangeStoreOrderStatusRequest(StoreOrderStatus Status, string? Reason = null, NotDeliveredReason? NotDeliveredReason = null);

/// <summary>The client cancels their part of an order with one store, before it ships.</summary>
public sealed record CancelStoreOrderRequest(string? Reason);

public sealed record StoreOrderEventDto(
    StoreOrderStatus From,
    StoreOrderStatus To,
    OrderActorRole ActorRole,
    string? Reason,
    NotDeliveredReason? NotDeliveredReason,
    DateTime At);

public sealed record StoreOrderQuery(StoreOrderStatus? Status, bool? WithAffiliate, string? Search, int? Page, int? PageSize);

/// <summary>A row of the store owner's order list.</summary>
public sealed record StoreOrderListItem(
    Guid Id,
    string Reference,
    DateTime CreatedAt,
    string ClientName,
    string City,
    int ItemCount,
    decimal AmountToCollect,
    StoreOrderStatus Status,
    PaymentStatus PaymentStatus,
    bool HasAffiliate,
    bool PlacedByAffiliate);

/// <summary>An order line as the store sees it: with the affiliate's commission it pays.</summary>
public sealed record StoreOrderLineDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSlug,
    string? ImageUrl,
    IReadOnlyList<CartOptionLabel> Options,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    string? AffiliateCode,
    int? AffiliatePercent,
    decimal? Commission);

/// <summary>One store order with everything the store needs to prepare, deliver and collect it.</summary>
/// <param name="Next">Statuses the store may move it to now.</param>
public sealed record StoreOrderDetailDto(
    Guid Id,
    string Reference,
    DateTime CreatedAt,
    StoreOrderStatus Status,
    PaymentStatus PaymentStatus,
    string ClientName,
    string ClientPhone,
    string City,
    string Address,
    string? Note,
    bool PlacedByAffiliate,
    decimal DeliveryFee,
    decimal Subtotal,
    decimal AmountToCollect,
    IReadOnlyList<StoreOrderLineDto> Lines,
    IReadOnlyList<StoreOrderEventDto> History,
    IReadOnlyList<StoreOrderStatus> Next);

/// <summary>Orders per status, for the tabs of the store's order list.</summary>
public sealed record StoreOrderCounts(IReadOnlyDictionary<StoreOrderStatus, int> ByStatus);

/// <summary>Which date the admin order list's <c>From</c> / <c>To</c> apply to (023 — so a dashboard number opens its orders).</summary>
public enum OrderDateField
{
    Placed = 1,
    /// <summary>The day a store's parcel was delivered.</summary>
    Delivered = 2,
}

/// <summary>
/// Admin order list filters. <c>AffiliateId</c> = an affiliate's code on a line, or the affiliate placed it for the client;
/// <c>From</c> / <c>To</c> are inclusive days (UTC) on the date chosen by <c>DateField</c> (placed by default).
/// </summary>
public sealed record AdminOrderQuery(
    StoreOrderStatus? Status, string? Search, Guid? StoreId, int? Page, int? PageSize,
    Guid? AffiliateId = null, Guid? ClientId = null, Guid? ProductId = null, DateOnly? From = null, DateOnly? To = null,
    OrderDateField? DateField = null);

public sealed record AdminStoreOrderRef(Guid Id, Guid StoreId, string StoreName, StoreOrderStatus Status, decimal AmountToCollect);

public sealed record AdminOrderListItem(
    Guid Id,
    string Reference,
    DateTime CreatedAt,
    string ClientName,
    string ClientPhone,
    string City,
    bool PlacedByAffiliate,
    decimal Total,
    IReadOnlyList<AdminStoreOrderRef> StoreOrders);
