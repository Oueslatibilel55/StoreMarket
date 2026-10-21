using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

public sealed record OrderContactRequest(string FullName, string Phone, string City, string Address, string? Note);

/// <summary>
/// Place the order. Signed in: the server cart is ordered (<c>Lines</c> ignored). Guest: <c>Lines</c> is the
/// browser cart. <c>ForClient</c>: an affiliate orders for a client — their own code goes on every product of a
/// store they work with, the other lines carry no code.
/// </summary>
public sealed record CheckoutRequest(OrderContactRequest Contact, bool ForClient = false, IReadOnlyList<GuestCartLine>? Lines = null);

public sealed record OrderLineDto(
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
    int? AffiliatePercent);

public sealed record StoreOrderDto(
    Guid Id,
    Guid StoreId,
    string StoreName,
    string? StoreSlug,
    StoreOrderStatus Status,
    PaymentStatus PaymentStatus,
    decimal DeliveryFee,
    decimal Subtotal,
    decimal AmountToCollect,
    IReadOnlyList<OrderLineDto> Lines,
    IReadOnlyList<StoreOrderEventDto> History,
    bool CanCancel);

/// <summary>An order as its client (or the affiliate who placed it) sees it.</summary>
public sealed record OrderDto(
    Guid Id,
    string Reference,
    DateTime CreatedAt,
    string ClientName,
    string ClientPhone,
    string City,
    string Address,
    string? Note,
    bool PlacedByAffiliate,
    decimal ItemsTotal,
    decimal DeliveryTotal,
    decimal Total,
    IReadOnlyList<StoreOrderDto> StoreOrders);

public sealed record OrderListItem(
    Guid Id,
    string Reference,
    DateTime CreatedAt,
    string ClientName,
    bool PlacedByAffiliate,
    int ItemCount,
    IReadOnlyList<string> StoreNames,
    IReadOnlyList<StoreOrderStatus> Statuses,
    decimal Total);

/// <summary>A product sold with the affiliate's code, as the affiliate sees it — no client contact unless they placed the order.</summary>
public sealed record AffiliateOrderLine(
    Guid LineId,
    string Reference,
    DateTime CreatedAt,
    string StoreName,
    string ProductName,
    string ClientName,
    int Quantity,
    decimal LineTotal,
    int AffiliatePercent,
    decimal ExpectedCoins,
    StoreOrderStatus Status,
    bool PlacedByMe);

/// <param name="ExpectedCoins">Lines whose parcel is still on its way.</param>
/// <param name="EarnedCoins">Lines delivered (credited to the ledger, 019).</param>
public sealed record AffiliateOrderSummary(int Lines, decimal Sales, decimal ExpectedCoins, decimal EarnedCoins);

public sealed record AffiliateOrdersDto(AffiliateOrderSummary Summary, PagedResult<AffiliateOrderLine> Lines);

public sealed record OrderListQuery(int? Page, int? PageSize);
