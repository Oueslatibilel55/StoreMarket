using System.Net;
using WebApp.Application.DTOs;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>Checkout and orders (014). Each test builds its own stores and products.</summary>
public sealed class CheckoutEndpointsTests(ApiFactory factory)
{
    private static readonly string[] Tunis = ["tunis"];
    private static readonly string[] TunisAndSfax = ["tunis", "sfax"];

    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString("N")[..10]}";

    private static object Contact(string city = "tunis", string phone = "50 112 233") =>
        new { fullName = "Leila Gharbi", phone, city, address = "12 rue de Marseille", note = "Sonner deux fois" };

    private sealed record Shop(MyStoreDto Store, HttpClient Owner);

    private sealed class World(HttpClient admin, Guid categoryId, Guid storeCategoryId) : IDisposable
    {
        public HttpClient Admin { get; } = admin;
        public Guid CategoryId { get; } = categoryId;
        public Guid StoreCategoryId { get; } = storeCategoryId;
        public void Dispose() => Admin.Dispose();
    }

    private async Task<World> Arrange()
    {
        var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);
        var category = await (await admin.Post("/api/admin/product-categories", new { nameFr = Unique("Parfums") })).Read<AdminProductCategoryDto>();
        var storeCategory = await (await admin.Post("/api/admin/store-categories", new { nameFr = Unique("Beauté") })).Read<AdminStoreCategoryDto>();
        return new World(admin, category.Id, storeCategory.Id);
    }

    private async Task<Shop> NewStore(World world, decimal deliveryFee, string[]? cities = null)
    {
        var owner = await factory.StoreOwnerClient();
        var store = await (await owner.Post("/api/me/store", new
        {
            name = Unique("Boutique"), categoryId = world.StoreCategoryId, phone = "22 145 678", city = "tunis",
            defaultAffiliatePercent = 10, deliveryFee, deliveryCities = cities ?? Tunis, deliveryMinDays = 1, deliveryMaxDays = 2,
        })).Read<MyStoreDto>();
        return new Shop(store, owner);
    }

    private static async Task<ProductDto> NewProduct(Shop shop, World world, decimal price, int stock = 5) =>
        await (await shop.Owner.Post("/api/me/store/products", new
        {
            name = Unique("Musc"), categoryId = world.CategoryId, price, stock, status = "Published",
        })).Read<ProductDto>();

    private async Task<HttpClient> Client()
    {
        var client = factory.CreateClient();
        client.WithToken((await client.Register()).AccessToken);
        return client;
    }

    private async Task<(HttpClient Client, string Code)> PartnerOf(Shop shop)
    {
        var client = factory.CreateClient();
        var auth = await client.RegisterAffiliate();
        client.WithToken(auth.AccessToken);
        var request = await (await client.Post("/api/me/partnerships", new { storeId = shop.Store.Id })).Read<AffiliatePartnershipDto>();
        (await shop.Owner.Post($"/api/me/store/partnerships/{request.Id}/accept")).EnsureSuccessStatusCode();
        return (client, auth.User.AffiliateCode!);
    }

    private static async Task Add(HttpClient client, Guid productId, int quantity = 1, string? code = null) =>
        (await client.Post("/api/me/cart/lines", new { productId, quantity, affiliateCode = code })).EnsureSuccessStatusCode();

    private static Task<HttpResponseMessage> Checkout(HttpClient client, object? contact = null, bool forClient = false, object[]? lines = null) =>
        client.Post("/api/orders", new { contact = contact ?? Contact(), forClient, lines });

    private static async Task<int> StockOf(Shop shop, Guid productId) =>
        (await (await shop.Owner.GetAsync(new Uri($"/api/me/store/products/{productId}", UriKind.Relative), Ct)).Read<ProductDto>()).Stock;

    [Fact]
    public async Task SignedIn_OrderSplitPerStore_StockTaken_CartEmptied_CodeFrozen()
    {
        using var world = await Arrange();
        var a = await NewStore(world, 7m);
        var b = await NewStore(world, 8m);
        using var _ = a.Owner;
        using var __ = b.Owner;
        var perfume = await NewProduct(a, world, 50m, stock: 5);
        var cream = await NewProduct(b, world, 30m, stock: 3);
        var (affiliate, code) = await PartnerOf(a);
        using var ___ = affiliate;
        using var client = await Client();
        await Add(client, perfume.Id, 2, code);
        await Add(client, cream.Id);

        var response = await Checkout(client);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Read<OrderDto>();
        Assert.Matches(@"^CMD-\d{4}-\d{6}$", order.Reference);
        Assert.Equal(2, order.StoreOrders.Count);
        Assert.Equal(130m, order.ItemsTotal);
        Assert.Equal(15m, order.DeliveryTotal);
        Assert.Equal(145m, order.Total);
        Assert.Equal("+21650112233", order.ClientPhone);
        var storeA = order.StoreOrders.Single(s => s.StoreId == a.Store.Id);
        Assert.Equal(StoreOrderStatus.Placed, storeA.Status);
        Assert.Equal(107m, storeA.AmountToCollect);
        Assert.Equal(code, storeA.Lines.Single().AffiliateCode);
        Assert.Equal(10, storeA.Lines.Single().AffiliatePercent);

        Assert.Equal(3, await StockOf(a, perfume.Id));
        Assert.Equal(2, await StockOf(b, cream.Id));
        Assert.Equal(0, (await (await client.GetAsync(new Uri("/api/me/cart", UriKind.Relative), Ct)).Read<CartDto>()).ItemCount);

        var mine = await (await client.GetAsync(new Uri("/api/me/orders", UriKind.Relative), Ct)).Read<PagedResult<OrderListItem>>();
        var listed = Assert.Single(mine.Items);
        Assert.Equal(3, listed.ItemCount);
        var detail = await (await client.GetAsync(new Uri($"/api/me/orders/{order.Reference}", UriKind.Relative), Ct)).Read<OrderDto>();
        Assert.Equal(order.Total, detail.Total);

        // The affiliate sees the sale and the coins it will bring — 100 TND × 10 % = 10 coins.
        var sales = await (await affiliate.GetAsync(new Uri("/api/me/affiliate/orders", UriKind.Relative), Ct)).Read<AffiliateOrdersDto>();
        var line = Assert.Single(sales.Lines.Items);
        Assert.Equal(100m, line.LineTotal);
        Assert.Equal(10m, line.ExpectedCoins);
        Assert.False(line.PlacedByMe);
        Assert.Equal(10m, sales.Summary.ExpectedCoins);

        // Someone else's order doesn't exist for them.
        using var other = await Client();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(new Uri($"/api/me/orders/{order.Reference}", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task Guest_Checkout_ThenLookupWithReferenceAndPhone()
    {
        using var world = await Arrange();
        var shop = await NewStore(world, 7m);
        using var _ = shop.Owner;
        var perfume = await NewProduct(shop, world, 50m);

        using var guest = factory.CreateClient();
        var response = await Checkout(guest, lines: [new { productId = perfume.Id, variantId = (Guid?)null, quantity = 2, affiliateCode = (string?)null }]);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Read<OrderDto>();
        Assert.Equal(107m, order.Total);

        var found = await guest.GetAsync(new Uri($"/api/orders/lookup?reference={order.Reference.ToLowerInvariant()}&phone=%2B216%2050%20112%20233", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await guest.GetAsync(new Uri($"/api/orders/lookup?reference={order.Reference}&phone=22000000", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task Refused_WhenTheStoreDoesNotDeliverTheCity_OrTheContactIsWrong_OrTheCartIsEmpty()
    {
        using var world = await Arrange();
        var shop = await NewStore(world, 7m);
        using var _ = shop.Owner;
        var perfume = await NewProduct(shop, world, 50m);
        using var client = await Client();

        Assert.Equal("checkout.cartEmpty", await (await Checkout(client)).ProblemCode());

        await Add(client, perfume.Id);
        var far = await Checkout(client, Contact(city: "sfax"));
        Assert.Equal(HttpStatusCode.Conflict, far.StatusCode);
        Assert.Equal("checkout.cityNotDelivered", await far.ProblemCode());
        Assert.Equal("order.phoneInvalid", await (await Checkout(client, Contact(phone: "123"))).ProblemCode());

        Assert.Equal(5, await StockOf(shop, perfume.Id)); // nothing was taken
    }

    [Fact]
    public async Task Refused_WhenACodeStoppedCounting_OrAProductWasDisabled_NothingIsTaken()
    {
        using var world = await Arrange();
        var shop = await NewStore(world, 7m);
        using var _ = shop.Owner;
        var perfume = await NewProduct(shop, world, 50m);
        var (affiliate, code) = await PartnerOf(shop);
        using var __ = affiliate;
        using var client = await Client();
        await Add(client, perfume.Id, 1, code);

        // The owner ends the partnership after the client filled the cart.
        var partnership = (await (await shop.Owner.GetAsync(new Uri("/api/me/store/partnerships", UriKind.Relative), Ct))
            .Read<IReadOnlyList<StorePartnershipDto>>()).Single();
        (await shop.Owner.Post($"/api/me/store/partnerships/{partnership.Id}/end")).EnsureSuccessStatusCode();
        Assert.Equal("checkout.codeRefused", await (await Checkout(client)).ProblemCode());

        (await world.Admin.Post($"/api/admin/products/{perfume.Id}/disable", new { reason = "Contrefaçon" })).EnsureSuccessStatusCode();
        Assert.Equal("checkout.cartNotReady", await (await Checkout(client)).ProblemCode());
        Assert.Equal(5, await StockOf(shop, perfume.Id));
        Assert.Empty((await (await client.GetAsync(new Uri("/api/me/orders", UriKind.Relative), Ct)).Read<PagedResult<OrderListItem>>()).Items);
    }

    [Fact]
    public async Task LastUnit_TwoClientsAtOnce_OnlyOneOrderIsPlaced()
    {
        using var world = await Arrange();
        var shop = await NewStore(world, 7m);
        using var _ = shop.Owner;
        var last = await NewProduct(shop, world, 50m, stock: 1);
        using var first = await Client();
        using var second = await Client();
        await Add(first, last.Id);
        await Add(second, last.Id);

        var results = await Task.WhenAll(Checkout(first), Checkout(second));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        var loser = results.Single(r => r.StatusCode != HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        Assert.Equal(0, await StockOf(shop, last.Id));
    }

    [Fact]
    public async Task AffiliateMode_OwnCodeOnPartnerStoresOnly()
    {
        using var world = await Arrange();
        var partner = await NewStore(world, 7m, TunisAndSfax);
        var other = await NewStore(world, 8m, TunisAndSfax);
        using var _ = partner.Owner;
        using var __ = other.Owner;
        var perfume = await NewProduct(partner, world, 40m);
        var cream = await NewProduct(other, world, 30m);
        var (affiliate, code) = await PartnerOf(partner);
        using var ___ = affiliate;
        await Add(affiliate, perfume.Id, 2);
        await Add(affiliate, cream.Id);

        var response = await Checkout(affiliate, Contact(city: "sfax", phone: "98 765 432"), forClient: true);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Read<OrderDto>();
        Assert.True(order.PlacedByAffiliate);
        Assert.Equal(code, order.StoreOrders.Single(s => s.StoreId == partner.Store.Id).Lines.Single().AffiliateCode);
        Assert.Null(order.StoreOrders.Single(s => s.StoreId == other.Store.Id).Lines.Single().AffiliateCode);

        Assert.Single((await (await affiliate.GetAsync(new Uri("/api/me/orders", UriKind.Relative), Ct)).Read<PagedResult<OrderListItem>>()).Items);
        var sales = await (await affiliate.GetAsync(new Uri("/api/me/affiliate/orders", UriKind.Relative), Ct)).Read<AffiliateOrdersDto>();
        Assert.True(Assert.Single(sales.Lines.Items).PlacedByMe);

        // Only an active affiliate can order for a client.
        using var client = await Client();
        await Add(client, cream.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await Checkout(client, forClient: true)).StatusCode);
    }
}
