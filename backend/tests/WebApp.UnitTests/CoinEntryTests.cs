using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.UnitTests;

public sealed class CoinEntryTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Affiliate = Guid.CreateVersion7();

    /// <summary>A delivered store order with one affiliate line (200 TND at 10 %) and one line without code.</summary>
    private static (Order Order, StoreOrder StoreOrder) Delivered(decimal coinValue = 1m)
    {
        var store = Guid.CreateVersion7();
        var order = Order.Place(7, Now, Guid.CreateVersion7(), null,
            new OrderContact("Leila Gharbi", "50112233", "tunis", "12 rue de Marseille", null),
            [
                new OrderLineDraft(store, "Parfumerie", 7m, Guid.CreateVersion7(), null, "Ambre", "ambre", [], 100m, 2,
                    new OrderAffiliate(Affiliate, Guid.CreateVersion7(), "DRF-45", 10)),
                new OrderLineDraft(store, "Parfumerie", 7m, Guid.CreateVersion7(), null, "Musc", "musc", [], 30m, 1, null),
            ], coinValue).Value;
        var id = order.StoreOrders[0].Id;
        order.ChangeStatus(id, StoreOrderStatus.Shipped, OrderActorRole.Store, null, null, null, Now);
        order.ChangeStatus(id, StoreOrderStatus.Delivered, OrderActorRole.Store, null, null, null, Now);
        return (order, order.StoreOrders[0]);
    }

    [Fact]
    public void Earned_CopiesEverythingTheCoinsComeFrom()
    {
        var (order, storeOrder) = Delivered();
        var line = storeOrder.Lines.Single(l => l.AffiliateId is not null);

        var entry = CoinEntry.Earned(order, storeOrder, line)!;

        Assert.Equal(CoinEntryType.Earned, entry.Type);
        Assert.Equal(Affiliate, entry.AffiliateId);
        Assert.Equal(20m, entry.Coins); // 200 TND × 10 % ÷ 1 TND
        Assert.Equal(order.Reference, entry.OrderReference);
        Assert.Equal(line.Id, entry.OrderLineId);
        Assert.Equal(storeOrder.Id, entry.StoreOrderId);
        Assert.Equal("Ambre", entry.ProductName);
        Assert.Equal("Parfumerie", entry.StoreName);
        Assert.Equal("Leila Gharbi", entry.ClientName);
        Assert.Equal(2, entry.Quantity);
        Assert.Equal(200m, entry.LineTotal);
        Assert.Equal(10, entry.AffiliatePercent);
        Assert.Equal(1m, entry.CoinValueInTnd);
    }

    [Fact]
    public void Earned_UsesTheCoinValueFrozenOnTheOrder()
    {
        var (order, storeOrder) = Delivered(coinValue: 0.5m);
        var entry = CoinEntry.Earned(order, storeOrder, storeOrder.Lines.Single(l => l.AffiliateId is not null))!;
        Assert.Equal(40m, entry.Coins); // 20 TND of commission at 0.5 TND a coin
    }

    [Fact]
    public void Earned_NothingForALineWithoutAffiliate()
    {
        var (order, storeOrder) = Delivered();
        Assert.Null(CoinEntry.Earned(order, storeOrder, storeOrder.Lines.Single(l => l.AffiliateId is null)));
    }

    [Fact]
    public void Earned_OnlyForDeliveredOrders()
    {
        var store = Guid.CreateVersion7();
        var order = Order.Place(8, Now, null, null, new OrderContact("Leila Gharbi", "50112233", "tunis", "12 rue de Marseille", null),
            [new OrderLineDraft(store, "P", 7m, Guid.CreateVersion7(), null, "A", "a", [], 100m, 1, new OrderAffiliate(Affiliate, Guid.CreateVersion7(), "DRF-45", 10))], 1m).Value;
        Assert.Throws<InvalidOperationException>(() => CoinEntry.Earned(order, order.StoreOrders[0], order.StoreOrders[0].Lines[0]));
    }

    [Theory]
    [InlineData(12.5)]
    [InlineData(-3)]
    public void Adjustment_IsSigned_WithAReason(decimal coins)
    {
        var entry = CoinEntry.Adjustment(Affiliate, coins, " Erreur de commission ", Guid.CreateVersion7(), null).Value;
        Assert.Equal(CoinEntryType.Adjustment, entry.Type);
        Assert.Equal(coins, entry.Coins);
        Assert.Equal("Erreur de commission", entry.Reason);
    }

    [Fact]
    public void Adjustment_Refused_WithoutReason_OrZero_OrTooPrecise()
    {
        var admin = Guid.CreateVersion7();
        Assert.Equal(DomainErrors.Coins.ReasonRequired, CoinEntry.Adjustment(Affiliate, 5m, " ", admin, null).Error);
        Assert.Equal(DomainErrors.Coins.AmountInvalid, CoinEntry.Adjustment(Affiliate, 0m, "x", admin, null).Error);
        Assert.Equal(DomainErrors.Coins.AmountInvalid, CoinEntry.Adjustment(Affiliate, 1.2345m, "x", admin, null).Error);
        Assert.Equal(DomainErrors.Coins.AmountInvalid, CoinEntry.Adjustment(Affiliate, CoinEntry.MaxAdjustment + 1, "x", admin, null).Error);
    }
}
