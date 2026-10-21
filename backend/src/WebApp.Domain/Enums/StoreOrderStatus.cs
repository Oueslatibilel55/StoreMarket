namespace WebApp.Domain.Enums;

/// <summary>
/// Status of a store's part of an order (016). Stored as int — append only, never renumber.
/// <c>Placed → Confirmed → Shipped → Delivered</c>, plus <c>Cancelled</c> (before shipping) and
/// <c>NotDelivered</c> (refused or returned parcel). <c>Delivered</c> is final: coins are credited then (019).
/// </summary>
public enum StoreOrderStatus
{
    Placed = 1,
    Confirmed = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
    NotDelivered = 6,
}

/// <summary>Why a parcel was not delivered — kept for the fraud statistics (024). Append only.</summary>
public enum NotDeliveredReason
{
    Refused = 1,
    Unreachable = 2,
    WrongAddress = 3,
    Other = 4,
}

/// <summary>Who changed a store order's status. Append only.</summary>
public enum OrderActorRole
{
    Store = 1,
    Client = 2,
    Admin = 3,
}

/// <summary>How the client pays (015). v1: cash on delivery only. Append only.</summary>
public enum PaymentMethod
{
    CashOnDelivery = 1,
}

/// <summary>Append only.</summary>
public enum PaymentStatus
{
    Unpaid = 1,
    /// <summary>The store confirmed the cash was received (delivery).</summary>
    Collected = 2,
}
