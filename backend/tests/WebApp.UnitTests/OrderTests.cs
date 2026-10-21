using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Domain.Events;

namespace WebApp.UnitTests;

public sealed class OrderTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid StoreA = Guid.CreateVersion7();
    private static readonly Guid StoreB = Guid.CreateVersion7();

    private static readonly OrderContact Contact = new("  Leila Gharbi ", "50 112 233", "tunis", " 12 rue de Marseille ", "  Sonner deux fois ");

    private static OrderLineDraft Line(Guid store, decimal price, int quantity, OrderAffiliate? affiliate = null, decimal fee = 7m) =>
        new(store, store == StoreA ? "Parfumerie" : "Cosmétiques", fee, Guid.CreateVersion7(), null, "Musc", "musc", [], price, quantity, affiliate);

    private static Result<Order> Place(IReadOnlyList<OrderLineDraft> lines, OrderContact? contact = null, decimal coinValue = 1m) =>
        Order.Place(123, Now, Guid.CreateVersion7(), null, contact ?? Contact, lines, coinValue);

    [Fact]
    public void Place_SplitsPerStore_WithDeliveryPerStore_AndTotals()
    {
        var order = Place([Line(StoreA, 50m, 2), Line(StoreA, 80m, 1), Line(StoreB, 30m, 1, fee: 8m)]).Value;

        Assert.Equal("CMD-2026-000123", order.Reference);
        Assert.Equal(2, order.StoreOrders.Count);
        var a = order.StoreOrders.Single(s => s.StoreId == StoreA);
        Assert.Equal(180m, a.Subtotal);
        Assert.Equal(187m, a.AmountToCollect);
        Assert.Equal(StoreOrderStatus.Placed, a.Status);
        Assert.Equal(210m, order.ItemsTotal);
        Assert.Equal(15m, order.DeliveryTotal);
        Assert.Equal(225m, order.Total);
        Assert.IsType<OrderPlacedEvent>(Assert.Single(order.DomainEvents));
    }

    [Fact]
    public void Place_NormalisesTheContact()
    {
        var order = Place([Line(StoreA, 10m, 1)]).Value;

        Assert.Equal("Leila Gharbi", order.ClientName);
        Assert.Equal("+21650112233", order.ClientPhone);
        Assert.Equal("12 rue de Marseille", order.Address);
        Assert.Equal("Sonner deux fois", order.Note);
    }

    [Fact]
    public void Place_FreezesTheAffiliateAndExpectedCoins()
    {
        var affiliate = new OrderAffiliate(Guid.CreateVersion7(), Guid.CreateVersion7(), "DRF-45", 10);
        var order = Place([Line(StoreA, 200m, 1, affiliate), Line(StoreA, 50m, 1)], coinValue: 0.5m).Value;

        var lines = order.StoreOrders.Single().Lines;
        var paid = lines.Single(l => l.AffiliateId is not null);
        Assert.Equal("DRF-45", paid.AffiliateCode);
        Assert.Equal(10, paid.AffiliatePercent);
        Assert.Equal(40m, paid.ExpectedCoins); // 200 × 10 % = 20 TND = 40 coins at 0.5 TND
        Assert.Null(lines.Single(l => l.AffiliateId is null).ExpectedCoins);
        Assert.Equal(0.5m, order.CoinValueInTnd);
    }

    [Theory]
    [InlineData(100, 10, 1, 10)]
    [InlineData(33.333, 7, 1, 2.333)]
    [InlineData(50, 50, 1, 25)]
    public void ExpectedCoins_IsLineTotalTimesPercent(decimal total, int percent, decimal coinValue, decimal expected) =>
        Assert.Equal(expected, Order.ExpectedCoins(total, percent, coinValue));

    [Fact]
    public void Place_RefusesBadContactDetails()
    {
        OrderLineDraft[] lines = [Line(StoreA, 10m, 1)];
        Assert.Equal(DomainErrors.Order.NameRequired, Place(lines, Contact with { FullName = " " }).Error);
        Assert.Equal(DomainErrors.Order.PhoneInvalid, Place(lines, Contact with { Phone = "123" }).Error);
        Assert.Equal(DomainErrors.Order.CityInvalid, Place(lines, Contact with { City = "paris" }).Error);
        Assert.Equal(DomainErrors.Order.AddressRequired, Place(lines, Contact with { Address = "rue" }).Error);
        Assert.Equal(DomainErrors.Order.NoteTooLong, Place(lines, Contact with { Note = new string('a', Order.MaxNoteLength + 1) }).Error);
    }

    [Fact]
    public void Place_NeedsLines() =>
        Assert.Equal(DomainErrors.Order.Empty, Place([]).Error);
}
