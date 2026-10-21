using System.Net;
using WebApp.Application.DTOs;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>Order lifecycle and cash on delivery (015 / 016). Each test places its own order.</summary>
public sealed class OrderLifecycleEndpointsTests(ApiFactory factory)
{
    private static readonly string[] Tunis = ["tunis"];

    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString("N")[..10]}";

    private sealed record Placed(HttpClient Owner, HttpClient Client, HttpClient Admin, ProductDto Product, OrderDto Order, Guid StoreOrderId) : IDisposable
    {
        public void Dispose()
        {
            Owner.Dispose();
            Client.Dispose();
            Admin.Dispose();
        }
    }

    /// <summary>A store with one product (stock 5) and a client order of 2 units, still Placed.</summary>
    private async Task<Placed> Arrange()
    {
        var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);
        var category = await (await admin.Post("/api/admin/product-categories", new { nameFr = Unique("Parfums") })).Read<AdminProductCategoryDto>();
        var storeCategory = await (await admin.Post("/api/admin/store-categories", new { nameFr = Unique("Beauté") })).Read<AdminStoreCategoryDto>();

        var owner = await factory.StoreOwnerClient();
        await (await owner.Post("/api/me/store", new
        {
            name = Unique("Boutique"), categoryId = storeCategory.Id, phone = "22 145 678", city = "tunis",
            defaultAffiliatePercent = 10, deliveryFee = 7m, deliveryCities = Tunis, deliveryMinDays = 1, deliveryMaxDays = 2,
        })).Read<MyStoreDto>();
        var product = await (await owner.Post("/api/me/store/products", new
        {
            name = Unique("Musc"), categoryId = category.Id, price = 50m, stock = 5, status = "Published",
        })).Read<ProductDto>();

        var client = factory.CreateClient();
        client.WithToken((await client.Register()).AccessToken);
        (await client.Post("/api/me/cart/lines", new { productId = product.Id, quantity = 2 })).EnsureSuccessStatusCode();
        var order = await (await client.Post("/api/orders", new
        {
            contact = new { fullName = "Leila Gharbi", phone = "50 112 233", city = "tunis", address = "12 rue de Marseille" },
        })).Read<OrderDto>();
        return new Placed(owner, client, admin, product, order, order.StoreOrders.Single().Id);
    }

    private static Task<HttpResponseMessage> Move(HttpClient owner, Guid id, string status, string? reason = null, string? notDeliveredReason = null) =>
        owner.Post($"/api/me/store/orders/{id}/status", new { status, reason, notDeliveredReason });

    private static async Task<int> StockOf(HttpClient owner, Guid productId) =>
        (await (await owner.GetAsync(new Uri($"/api/me/store/products/{productId}", UriKind.Relative), Ct)).Read<ProductDto>()).Stock;

    private static async Task<OrderDto> ClientView(Placed p) =>
        await (await p.Client.GetAsync(new Uri($"/api/me/orders/{p.Order.Reference}", UriKind.Relative), Ct)).Read<OrderDto>();

    [Fact]
    public async Task Store_ConfirmsShipsDelivers_CashCollected_DeliveredIsFinal()
    {
        using var p = await Arrange();

        var list = await (await p.Owner.GetAsync(new Uri("/api/me/store/orders?status=Placed", UriKind.Relative), Ct)).Read<PagedResult<StoreOrderListItem>>();
        var row = Assert.Single(list.Items);
        Assert.Equal(p.Order.Reference, row.Reference);
        Assert.Equal(107m, row.AmountToCollect);
        Assert.Equal(PaymentStatus.Unpaid, row.PaymentStatus);

        var counts = await (await p.Owner.GetAsync(new Uri("/api/me/store/orders/counts", UriKind.Relative), Ct)).Read<StoreOrderCounts>();
        Assert.Equal(1, counts.ByStatus[StoreOrderStatus.Placed]);

        var detail = await (await p.Owner.GetAsync(new Uri($"/api/me/store/orders/{p.StoreOrderId}", UriKind.Relative), Ct)).Read<StoreOrderDetailDto>();
        Assert.Equal("+21650112233", detail.ClientPhone);
        Assert.Equal("12 rue de Marseille", detail.Address);
        Assert.Equal([StoreOrderStatus.Confirmed, StoreOrderStatus.Shipped, StoreOrderStatus.Cancelled], detail.Next);

        (await Move(p.Owner, p.StoreOrderId, "Confirmed")).EnsureSuccessStatusCode();
        (await Move(p.Owner, p.StoreOrderId, "Shipped")).EnsureSuccessStatusCode();
        var delivered = await (await Move(p.Owner, p.StoreOrderId, "Delivered")).Read<StoreOrderDetailDto>();
        Assert.Equal(StoreOrderStatus.Delivered, delivered.Status);
        Assert.Equal(PaymentStatus.Collected, delivered.PaymentStatus);
        Assert.Empty(delivered.Next);
        Assert.Equal(3, delivered.History.Count);

        var mine = (await ClientView(p)).StoreOrders.Single();
        Assert.Equal(StoreOrderStatus.Delivered, mine.Status);
        Assert.False(mine.CanCancel);

        var again = await Move(p.Owner, p.StoreOrderId, "NotDelivered", notDeliveredReason: "Refused");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("order.deliveredIsFinal", await again.ProblemCode());
        Assert.Equal(3, await StockOf(p.Owner, p.Product.Id)); // delivered parcels keep their stock taken
    }

    [Fact]
    public async Task Client_CancelsBeforeShipping_StockComesBack_NotAfter()
    {
        using var p = await Arrange();
        Assert.Equal(3, await StockOf(p.Owner, p.Product.Id));

        var cancelled = await (await p.Client.Post($"/api/me/orders/{p.Order.Reference}/store-orders/{p.StoreOrderId}/cancel", new { reason = "Erreur de taille" }))
            .Read<OrderDto>();
        var part = cancelled.StoreOrders.Single();
        Assert.Equal(StoreOrderStatus.Cancelled, part.Status);
        Assert.Equal(OrderActorRole.Client, part.History.Single().ActorRole);
        Assert.Equal(5, await StockOf(p.Owner, p.Product.Id));

        using var shipped = await Arrange();
        (await Move(shipped.Owner, shipped.StoreOrderId, "Shipped")).EnsureSuccessStatusCode();
        var late = await shipped.Client.Post($"/api/me/orders/{shipped.Order.Reference}/store-orders/{shipped.StoreOrderId}/cancel");
        Assert.Equal("order.transitionNotAllowed", await late.ProblemCode());

        // Someone else can't cancel it.
        using var stranger = factory.CreateClient();
        stranger.WithToken((await stranger.Register()).AccessToken);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.Post($"/api/me/orders/{shipped.Order.Reference}/store-orders/{shipped.StoreOrderId}/cancel")).StatusCode);
    }

    [Fact]
    public async Task Store_CancelAndNotDelivered_NeedAReason_AndReturnTheStock()
    {
        using var p = await Arrange();
        Assert.Equal("order.reasonRequired", await (await Move(p.Owner, p.StoreOrderId, "Cancelled")).ProblemCode());
        Assert.Equal("order.transitionNotAllowed", await (await Move(p.Owner, p.StoreOrderId, "Delivered")).ProblemCode());

        (await Move(p.Owner, p.StoreOrderId, "Shipped")).EnsureSuccessStatusCode();
        Assert.Equal("order.reasonRequired", await (await Move(p.Owner, p.StoreOrderId, "NotDelivered")).ProblemCode());
        var returned = await (await Move(p.Owner, p.StoreOrderId, "NotDelivered", "Absent deux fois", "Unreachable")).Read<StoreOrderDetailDto>();
        Assert.Equal(StoreOrderStatus.NotDelivered, returned.Status);
        Assert.Equal(NotDeliveredReason.Unreachable, returned.History[^1].NotDeliveredReason);
        Assert.Equal(PaymentStatus.Unpaid, returned.PaymentStatus);
        Assert.Equal(5, await StockOf(p.Owner, p.Product.Id));
    }

    [Fact]
    public async Task OnlyTheStore_SeesAndMovesItsOrders()
    {
        using var p = await Arrange();
        using var other = await Arrange();

        Assert.Equal(HttpStatusCode.NotFound, (await other.Owner.GetAsync(new Uri($"/api/me/store/orders/{p.StoreOrderId}", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Move(other.Owner, p.StoreOrderId, "Confirmed")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Move(p.Client, p.StoreOrderId, "Confirmed")).StatusCode);
    }

    [Fact]
    public async Task TwoDeliveriesAtOnce_OnlyOneCounts()
    {
        using var p = await Arrange();
        (await Move(p.Owner, p.StoreOrderId, "Shipped")).EnsureSuccessStatusCode();

        var results = await Task.WhenAll(Move(p.Owner, p.StoreOrderId, "Delivered"), Move(p.Owner, p.StoreOrderId, "Delivered"));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(HttpStatusCode.Conflict, results.Single(r => r.StatusCode != HttpStatusCode.OK).StatusCode);
        var history = (await ClientView(p)).StoreOrders.Single().History;
        Assert.Single(history, h => h.To == StoreOrderStatus.Delivered);
    }

    [Fact]
    public async Task Admin_SeesEveryOrder_ForcesWithReason_Audited_ReopeningTakesStockAgain()
    {
        using var p = await Arrange();

        var list = await (await p.Admin.GetAsync(new Uri($"/api/admin/orders?search={p.Order.Reference}", UriKind.Relative), Ct)).Read<PagedResult<AdminOrderListItem>>();
        Assert.Equal(p.StoreOrderId, Assert.Single(Assert.Single(list.Items).StoreOrders).Id);
        var detail = await (await p.Admin.GetAsync(new Uri($"/api/admin/orders/{p.Order.Reference}", UriKind.Relative), Ct)).Read<OrderDto>();
        Assert.Equal(p.Order.Total, detail.Total);

        var url = $"/api/admin/store-orders/{p.StoreOrderId}/status";
        Assert.Equal("order.reasonRequired", await (await p.Admin.Post(url, new { status = "Cancelled" })).ProblemCode());
        (await p.Admin.Post(url, new { status = "Cancelled", reason = "Commande en double" })).EnsureSuccessStatusCode();
        Assert.Equal(5, await StockOf(p.Owner, p.Product.Id));

        var reopened = await (await p.Admin.Post(url, new { status = "Confirmed", reason = "Le client confirme par téléphone" })).Read<OrderDto>();
        Assert.Equal(StoreOrderStatus.Confirmed, reopened.StoreOrders.Single().Status);
        Assert.Equal(3, await StockOf(p.Owner, p.Product.Id));

        var audit = await (await p.Admin.GetAsync(new Uri($"/api/admin/audit?targetId={p.Order.Id}", UriKind.Relative), Ct)).Read<PagedResult<AuditEntryDto>>();
        Assert.Equal(2, audit.Items.Count(e => e.Action == "order.forceStatus"));

        Assert.Equal(HttpStatusCode.Forbidden, (await p.Client.GetAsync(new Uri("/api/admin/orders", UriKind.Relative), Ct)).StatusCode);
    }
}
