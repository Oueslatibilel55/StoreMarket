using WebApp.Application.Payments;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Domain.Events;
using static WebApp.Domain.Enums.StoreOrderStatus;

namespace WebApp.UnitTests;

public sealed class OrderLifecycleTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Owner = Guid.CreateVersion7();

    private static (Order Order, Guid StoreOrderId) NewOrder()
    {
        var store = Guid.CreateVersion7();
        var order = Order.Place(1, Now, Guid.CreateVersion7(), null,
            new OrderContact("Leila Gharbi", "50112233", "tunis", "12 rue de Marseille", null),
            [new OrderLineDraft(store, "Parfumerie", 7m, Guid.CreateVersion7(), null, "Musc", "musc", [], 50m, 1, null)], 1m).Value;
        order.ClearDomainEvents();
        return (order, order.StoreOrders[0].Id);
    }

    private static Result<StoreOrder> Store(Order order, Guid id, StoreOrderStatus to, string? reason = null, NotDeliveredReason? why = null) =>
        order.ChangeStatus(id, to, OrderActorRole.Store, Owner, reason, why, Now);

    [Fact]
    public void Store_HappyPath_PlacedConfirmedShippedDelivered_WithHistory()
    {
        var (order, id) = NewOrder();

        Assert.True(Store(order, id, Confirmed).IsSuccess);
        Assert.True(Store(order, id, Shipped).IsSuccess);
        var delivered = Store(order, id, Delivered).Value;

        Assert.Equal(Delivered, delivered.Status);
        Assert.Equal(Now, delivered.DeliveredAt);
        Assert.Equal(Now, delivered.ShippedAt);
        Assert.Equal([Placed, Confirmed, Shipped], delivered.History.Select(h => h.From));
        Assert.Equal([Confirmed, Shipped, Delivered], delivered.History.Select(h => h.To));
        Assert.All(delivered.History, h => Assert.Equal(OrderActorRole.Store, h.ActorRole));
        Assert.Single(order.DomainEvents.OfType<StoreOrderDeliveredEvent>());
        Assert.Equal(3, order.DomainEvents.OfType<StoreOrderStatusChangedEvent>().Count());
    }

    [Fact]
    public void Delivered_IsFinal_EvenForAnAdmin()
    {
        var (order, id) = NewOrder();
        Store(order, id, Shipped);
        Store(order, id, Delivered);

        Assert.Equal(DomainErrors.Order.DeliveredIsFinal, Store(order, id, NotDelivered, why: NotDeliveredReason.Refused).Error);
        Assert.Equal(DomainErrors.Order.DeliveredIsFinal,
            order.ChangeStatus(id, Cancelled, OrderActorRole.Admin, Guid.CreateVersion7(), "Litige", null, Now).Error);
    }

    [Theory]
    [InlineData(Placed, Delivered)]
    [InlineData(Confirmed, Delivered)]
    [InlineData(Placed, NotDelivered)]
    public void Store_CannotSkipShipping(StoreOrderStatus from, StoreOrderStatus to)
    {
        var (order, id) = NewOrder();
        if (from == Confirmed) Store(order, id, Confirmed);
        Assert.Equal(DomainErrors.Order.TransitionNotAllowed, Store(order, id, to, "x", NotDeliveredReason.Refused).Error);
    }

    [Fact]
    public void NotDelivered_NeedsAReason_AndKeepsIt()
    {
        var (order, id) = NewOrder();
        Store(order, id, Shipped);

        Assert.Equal(DomainErrors.Order.ReasonRequired, Store(order, id, NotDelivered).Error);
        var refused = Store(order, id, NotDelivered, " Client absent ", NotDeliveredReason.Unreachable).Value;
        Assert.Equal(NotDeliveredReason.Unreachable, refused.NotDeliveredReason);
        Assert.Equal("Client absent", refused.History[^1].Reason);
    }

    [Fact]
    public void StoreCancel_NeedsAReason_ClientCancelDoesNot_NotAfterShipping()
    {
        var (order, id) = NewOrder();
        Assert.Equal(DomainErrors.Order.ReasonRequired, Store(order, id, Cancelled).Error);

        var client = order.ChangeStatus(id, Cancelled, OrderActorRole.Client, null, null, null, Now);
        Assert.True(client.IsSuccess);

        var (shipped, sid) = NewOrder();
        Store(shipped, sid, Shipped);
        Assert.Equal(DomainErrors.Order.TransitionNotAllowed,
            shipped.ChangeStatus(sid, Cancelled, OrderActorRole.Client, null, null, null, Now).Error);
    }

    [Fact]
    public void Client_CanOnlyCancel()
    {
        var (order, id) = NewOrder();
        Assert.Equal(DomainErrors.Order.TransitionNotAllowed,
            order.ChangeStatus(id, Confirmed, OrderActorRole.Client, null, null, null, Now).Error);
    }

    [Fact]
    public void Admin_CanForceAnyStatus_WithAReason()
    {
        var (order, id) = NewOrder();
        var admin = Guid.CreateVersion7();

        Assert.Equal(DomainErrors.Order.ReasonRequired, order.ChangeStatus(id, Cancelled, OrderActorRole.Admin, admin, null, null, Now).Error);
        Assert.True(order.ChangeStatus(id, Cancelled, OrderActorRole.Admin, admin, "Doublon", null, Now).IsSuccess);
        // Re-opened by the admin (the client called back).
        Assert.True(order.ChangeStatus(id, Confirmed, OrderActorRole.Admin, admin, "Le client confirme", null, Now).IsSuccess);
        Assert.Equal(OrderActorRole.Admin, order.StoreOrders[0].History[^1].ActorRole);
    }

    [Fact]
    public void CashOnDelivery_DeliveredMeansPaid()
    {
        var (order, id) = NewOrder();
        var storeOrder = order.StoreOrders[0];
        Assert.Equal(PaymentStatus.Unpaid, storeOrder.PaymentStatus);
        Assert.Equal(PaymentMethod.CashOnDelivery, storeOrder.PaymentMethod);

        new CashOnDelivery().OnDelivered(storeOrder, Now);

        Assert.Equal(PaymentStatus.Collected, storeOrder.PaymentStatus);
        Assert.Equal(Now, storeOrder.PaidAt);
    }

    [Fact]
    public void UnknownStoreOrder_IsNotFound()
    {
        var (order, _) = NewOrder();
        Assert.Equal(DomainErrors.Order.StoreOrderNotFound, Store(order, Guid.CreateVersion7(), Confirmed).Error);
    }
}
