using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Domain.Events;

/// <summary>An order was placed (014): each store has a new order to prepare (notifications come with 022).</summary>
public sealed record OrderPlacedEvent(Guid OrderId, string Reference, IReadOnlyList<Guid> StoreIds) : IDomainEvent;

/// <summary>A store order changed status (notifications: 022).</summary>
public sealed record StoreOrderStatusChangedEvent(Guid OrderId, Guid StoreOrderId, Guid StoreId, StoreOrderStatus From, StoreOrderStatus To) : IDomainEvent;

/// <summary>A store confirmed delivery: the affiliate coins of its lines can be credited (019).</summary>
public sealed record StoreOrderDeliveredEvent(Guid OrderId, Guid StoreOrderId, Guid StoreId) : IDomainEvent;
