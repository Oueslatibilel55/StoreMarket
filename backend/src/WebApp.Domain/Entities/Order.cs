using WebApp.Domain.Common;
using WebApp.Domain.Enums;
using WebApp.Domain.Events;

namespace WebApp.Domain.Entities;

/// <summary>Where and to whom the parcel goes. The client may have no account (guest checkout).</summary>
public sealed record OrderContact(string FullName, string Phone, string City, string Address, string? Note);

/// <summary>The affiliate an order line pays, frozen at checkout: who, through which partnership, at what %.</summary>
public sealed record OrderAffiliate(Guid AffiliateId, Guid PartnershipId, string Code, int Percent);

/// <summary>One option of the ordered variant (e.g. Pointure: 38), names frozen in the three languages.</summary>
public sealed record OrderOption(string AxisFr, string? AxisAr, string? AxisEn, string ValueFr, string? ValueAr, string? ValueEn);

/// <summary>A product line as checkout hands it to <see cref="Order.Place"/> — everything already checked against the shop.</summary>
public sealed record OrderLineDraft(
    Guid StoreId,
    string StoreName,
    decimal DeliveryFee,
    Guid ProductId,
    Guid? VariantId,
    string ProductName,
    string ProductSlug,
    IReadOnlyList<OrderOption> Options,
    decimal UnitPrice,
    int Quantity,
    OrderAffiliate? Affiliate);

/// <summary>
/// A cash-on-delivery order (014), split into one <see cref="StoreOrder"/> per store — each store
/// prepares, ships and delivers its own part (016). Every line freezes what it was sold at: product,
/// price, quantity and, when it carries an affiliate code, the affiliate, partnership, % and expected
/// coins. Nothing here changes when the catalogue, a % or a setting changes later.
/// </summary>
public sealed class Order : AggregateRoot
{
    public const int MinNameLength = 2;
    public const int MaxNameLength = 120;
    public const int MinAddressLength = 5;
    public const int MaxAddressLength = 300;
    public const int MaxNoteLength = 500;

    private readonly List<StoreOrder> _storeOrders = [];

    /// <summary>Sequence number behind the reference.</summary>
    public long Number { get; private set; }
    /// <summary>Shown to the client: <c>CMD-2026-000123</c>.</summary>
    public string Reference { get; private set; } = null!;
    /// <summary>The signed-in client who ordered; null for a guest, or when an affiliate ordered for a client.</summary>
    public Guid? ClientId { get; private set; }
    /// <summary>Set when an affiliate placed the order for a client ("affiliate mode").</summary>
    public Guid? PlacedByAffiliateId { get; private set; }
    public bool PlacedByAffiliate => PlacedByAffiliateId is not null;

    public string ClientName { get; private set; } = null!;
    /// <summary>Normalised Tunisian number, <c>+216XXXXXXXX</c>.</summary>
    public string ClientPhone { get; private set; } = null!;
    /// <summary>Governorate code.</summary>
    public string City { get; private set; } = null!;
    public string Address { get; private set; } = null!;
    public string? Note { get; private set; }

    /// <summary>TND value of one coin when the order was placed (setting <c>coin.valueInTnd</c>, frozen).</summary>
    public decimal CoinValueInTnd { get; private set; }
    public decimal ItemsTotal { get; private set; }
    public decimal DeliveryTotal { get; private set; }
    public decimal Total { get; private set; }

    public IReadOnlyList<StoreOrder> StoreOrders => _storeOrders;

    private Order() { }

    public static string ReferenceFor(int year, long number) => $"CMD-{year}-{number:D6}";

    /// <summary>
    /// Places the order. The caller has checked availability, stock, store delivery zones and codes; the
    /// domain checks the client details and computes the totals and expected coins.
    /// </summary>
    public static Result<Order> Place(
        long number, DateTime utcNow, Guid? clientId, Guid? placedByAffiliateId, OrderContact contact,
        IReadOnlyList<OrderLineDraft> lines, decimal coinValueInTnd)
    {
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(coinValueInTnd);

        var name = contact.FullName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length is < MinNameLength or > MaxNameLength)
            return Result.Fail<Order>(DomainErrors.Order.NameRequired);
        var phone = User.NormalizePhone(contact.Phone);
        if (phone is null) return Result.Fail<Order>(DomainErrors.Order.PhoneInvalid);
        if (!Governorates.IsValid(contact.City)) return Result.Fail<Order>(DomainErrors.Order.CityInvalid);
        var address = contact.Address?.Trim();
        if (string.IsNullOrEmpty(address) || address.Length is < MinAddressLength or > MaxAddressLength)
            return Result.Fail<Order>(DomainErrors.Order.AddressRequired);
        var note = string.IsNullOrWhiteSpace(contact.Note) ? null : contact.Note.Trim();
        if (note?.Length > MaxNoteLength) return Result.Fail<Order>(DomainErrors.Order.NoteTooLong);

        if (lines.Count == 0) return Result.Fail<Order>(DomainErrors.Order.Empty);
        if (lines.Any(l => l.Quantity < 1 || l.UnitPrice < 0)) return Result.Fail<Order>(DomainErrors.Order.LineInvalid);

        var order = new Order
        {
            Number = number,
            Reference = ReferenceFor(utcNow.Year, number),
            ClientId = clientId,
            PlacedByAffiliateId = placedByAffiliateId,
            ClientName = name,
            ClientPhone = phone,
            City = contact.City,
            Address = address,
            Note = note,
            CoinValueInTnd = coinValueInTnd,
        };
        foreach (var store in lines.GroupBy(l => l.StoreId))
            order._storeOrders.Add(new StoreOrder(store.Key, store.First().StoreName, store.First().DeliveryFee,
                [.. store.Select(l => new OrderLine(l, coinValueInTnd))]));

        order.ItemsTotal = order._storeOrders.Sum(s => s.Subtotal);
        order.DeliveryTotal = order._storeOrders.Sum(s => s.DeliveryFee);
        order.Total = order.ItemsTotal + order.DeliveryTotal;
        order.RaiseDomainEvent(new OrderPlacedEvent(order.Id, order.Reference, [.. order._storeOrders.Select(s => s.StoreId)]));
        return Result.Ok(order);
    }

    /// <summary>
    /// Moves one store's part of the order (016). Who may do what:
    /// <list type="bullet">
    /// <item>the store: Placed → Confirmed → Shipped → Delivered, Placed → Shipped, Shipped → NotDelivered (reason
    /// required), Placed / Confirmed → Cancelled (reason required);</item>
    /// <item>the client: Placed / Confirmed → Cancelled (reason optional);</item>
    /// <item>an admin: any change but out of Delivered, always with a reason (audited by the caller).</item>
    /// </list>
    /// Delivered is final — coins are credited on it (019); a refused or returned parcel is NotDelivered.
    /// Payment collection and stock return are the caller's (015 payment method, stock repository).
    /// </summary>
    public Result<StoreOrder> ChangeStatus(Guid storeOrderId, StoreOrderStatus to, OrderActorRole role, Guid? actorId,
        string? reason, NotDeliveredReason? notDeliveredReason, DateTime utcNow)
    {
        var storeOrder = _storeOrders.FirstOrDefault(s => s.Id == storeOrderId);
        if (storeOrder is null) return Result.Fail<StoreOrder>(DomainErrors.Order.StoreOrderNotFound);
        var from = storeOrder.Status;
        if (from == StoreOrderStatus.Delivered) return Result.Fail<StoreOrder>(DomainErrors.Order.DeliveredIsFinal);
        if (from == to || !IsAllowed(role, from, to)) return Result.Fail<StoreOrder>(DomainErrors.Order.TransitionNotAllowed);

        var note = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (note?.Length > StoreOrder.MaxReasonLength) return Result.Fail<StoreOrder>(DomainErrors.Order.ReasonTooLong);
        var reasonRequired = role == OrderActorRole.Admin
            || (role == OrderActorRole.Store && to == StoreOrderStatus.Cancelled);
        if (reasonRequired && note is null) return Result.Fail<StoreOrder>(DomainErrors.Order.ReasonRequired);
        if (to == StoreOrderStatus.NotDelivered && (notDeliveredReason is null || !Enum.IsDefined(notDeliveredReason.Value)))
            return Result.Fail<StoreOrder>(DomainErrors.Order.ReasonRequired);

        storeOrder.Move(to, role, actorId, note, to == StoreOrderStatus.NotDelivered ? notDeliveredReason : null, utcNow);
        MarkUpdated();
        RaiseDomainEvent(new StoreOrderStatusChangedEvent(Id, storeOrder.Id, storeOrder.StoreId, from, to));
        if (to == StoreOrderStatus.Delivered) RaiseDomainEvent(new StoreOrderDeliveredEvent(Id, storeOrder.Id, storeOrder.StoreId));
        return Result.Ok(storeOrder);
    }

    /// <summary>The changes the store and the client may make; an admin may make any (except out of Delivered).</summary>
    public static bool IsAllowed(OrderActorRole role, StoreOrderStatus from, StoreOrderStatus to) => role switch
    {
        OrderActorRole.Admin => from != StoreOrderStatus.Delivered && from != to,
        OrderActorRole.Client => to == StoreOrderStatus.Cancelled && from is StoreOrderStatus.Placed or StoreOrderStatus.Confirmed,
        OrderActorRole.Store => (from, to) switch
        {
            (StoreOrderStatus.Placed, StoreOrderStatus.Confirmed) => true,
            (StoreOrderStatus.Placed or StoreOrderStatus.Confirmed, StoreOrderStatus.Shipped) => true,
            (StoreOrderStatus.Shipped, StoreOrderStatus.Delivered or StoreOrderStatus.NotDelivered) => true,
            (StoreOrderStatus.Placed or StoreOrderStatus.Confirmed, StoreOrderStatus.Cancelled) => true,
            _ => false,
        },
        _ => false,
    };

    /// <summary><c>line total × % ÷ coin value</c>, rounded to the millime-coin (3 decimals).</summary>
    public static decimal ExpectedCoins(decimal lineTotal, int percent, decimal coinValueInTnd) =>
        Math.Round(lineTotal * percent / 100m / coinValueInTnd, 3, MidpointRounding.AwayFromZero);
}

/// <summary>The part of an order one store prepares and delivers (its lifecycle comes with 016).</summary>
public sealed class StoreOrder : BaseEntity
{
    public const int MaxReasonLength = 500;

    private readonly List<OrderLine> _lines = [];
    private readonly List<StoreOrderEvent> _history = [];

    public Guid StoreId { get; private set; }
    /// <summary>Store name at checkout.</summary>
    public string StoreName { get; private set; } = null!;
    public StoreOrderStatus Status { get; private set; }
    /// <summary>Delivery fee of the store at checkout (frozen).</summary>
    public decimal DeliveryFee { get; private set; }
    public decimal Subtotal { get; private set; }
    /// <summary>Cash the delivery collects for this store: lines + delivery (015).</summary>
    public decimal AmountToCollect { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    /// <summary>When the store confirmed delivery (final; coins are credited from it — 019).</summary>
    public DateTime? DeliveredAt { get; private set; }
    /// <summary>Set when the parcel came back (refused, unreachable…) — kept for the fraud statistics (024).</summary>
    public NotDeliveredReason? NotDeliveredReason { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines;
    /// <summary>Every status change: who, when, from, to, why.</summary>
    public IReadOnlyList<StoreOrderEvent> History => _history;

    private StoreOrder() { }

    internal StoreOrder(Guid storeId, string storeName, decimal deliveryFee, List<OrderLine> lines)
    {
        StoreId = storeId;
        StoreName = storeName;
        Status = StoreOrderStatus.Placed;
        DeliveryFee = deliveryFee;
        _lines.AddRange(lines);
        Subtotal = lines.Sum(l => l.LineTotal);
        AmountToCollect = Subtotal + deliveryFee;
        PaymentMethod = PaymentMethod.CashOnDelivery;
        PaymentStatus = PaymentStatus.Unpaid;
    }

    internal void Move(StoreOrderStatus to, OrderActorRole role, Guid? actorId, string? reason, NotDeliveredReason? notDelivered, DateTime utcNow)
    {
        _history.Add(new StoreOrderEvent(Status, to, role, actorId, reason, notDelivered, utcNow));
        Status = to;
        if (to == StoreOrderStatus.Shipped) ShippedAt = utcNow;
        if (to == StoreOrderStatus.Delivered) DeliveredAt = utcNow;
        NotDeliveredReason = notDelivered;
        MarkUpdated();
    }

    /// <summary>The payment method confirms the money is in (015) — for cash on delivery, at delivery.</summary>
    public void MarkPaid(DateTime utcNow)
    {
        if (PaymentStatus == PaymentStatus.Collected) return;
        PaymentStatus = PaymentStatus.Collected;
        PaidAt = utcNow;
        MarkUpdated();
    }
}

/// <summary>One status change of a store order (016).</summary>
public sealed class StoreOrderEvent : BaseEntity
{
    public StoreOrderStatus From { get; private set; }
    public StoreOrderStatus To { get; private set; }
    public OrderActorRole ActorRole { get; private set; }
    /// <summary>The store owner, client or admin — null for a guest client.</summary>
    public Guid? ActorId { get; private set; }
    public string? Reason { get; private set; }
    public NotDeliveredReason? NotDeliveredReason { get; private set; }
    public DateTime At { get; private set; }

    private StoreOrderEvent() { }

    internal StoreOrderEvent(StoreOrderStatus from, StoreOrderStatus to, OrderActorRole role, Guid? actorId, string? reason,
        NotDeliveredReason? notDelivered, DateTime at)
    {
        From = from;
        To = to;
        ActorRole = role;
        ActorId = actorId;
        Reason = reason;
        NotDeliveredReason = notDelivered;
        At = at;
    }
}

/// <summary>One ordered product, frozen at checkout — including the affiliate it pays, if any.</summary>
public sealed class OrderLine : BaseEntity
{
    private readonly List<OrderOption> _options = [];

    public Guid ProductId { get; private set; }
    public Guid? VariantId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public string ProductSlug { get; private set; } = null!;
    public IReadOnlyList<OrderOption> Options => _options;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal LineTotal { get; private set; }

    public Guid? AffiliateId { get; private set; }
    public Guid? PartnershipId { get; private set; }
    public string? AffiliateCode { get; private set; }
    /// <summary>Commission % at checkout (frozen).</summary>
    public int? AffiliatePercent { get; private set; }
    /// <summary>Coins the affiliate will get if this line is delivered (019 credits them).</summary>
    public decimal? ExpectedCoins { get; private set; }

    private OrderLine() { }

    internal OrderLine(OrderLineDraft draft, decimal coinValueInTnd)
    {
        ProductId = draft.ProductId;
        VariantId = draft.VariantId;
        ProductName = draft.ProductName;
        ProductSlug = draft.ProductSlug;
        _options.AddRange(draft.Options);
        UnitPrice = draft.UnitPrice;
        Quantity = draft.Quantity;
        LineTotal = draft.UnitPrice * draft.Quantity;
        if (draft.Affiliate is { } a)
        {
            AffiliateId = a.AffiliateId;
            PartnershipId = a.PartnershipId;
            AffiliateCode = a.Code;
            AffiliatePercent = a.Percent;
            ExpectedCoins = Order.ExpectedCoins(LineTotal, a.Percent, coinValueInTnd);
        }
    }
}
