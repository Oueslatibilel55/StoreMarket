using System.Net;
using WebApp.Application.DTOs;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>
/// Dashboard numbers (023), checked against a known set of orders. Tests share one database: per store / affiliate /
/// product numbers are exact, platform-wide ones only grow by at least what the test added.
/// </summary>
public sealed class StatsEndpointsTests(ApiFactory factory)
{
    private static readonly string[] Tunis = ["tunis"];

    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString("N")[..10]}";

    private sealed record Shop(HttpClient Owner, MyStoreDto Store, ProductDto Product);

    private sealed record World(HttpClient Admin, HttpClient Affiliate, UserDto AffiliateUser, Shop A, Shop B, Guid ClientId) : IDisposable
    {
        public void Dispose()
        {
            Admin.Dispose();
            Affiliate.Dispose();
            A.Owner.Dispose();
            B.Owner.Dispose();
        }
    }

    /// <summary>
    /// Store A sells a 50 TND product at 10 %, store B an 80 TND one at 5 %; one affiliate works with both. Then:
    /// order 1 (code): 2 × A delivered, 1 × B cancelled; order 2 (no code): 1 × A delivered; order 3 (code): 1 × A not delivered.
    /// </summary>
    private async Task<World> Arrange()
    {
        var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);
        var category = await (await admin.Post("/api/admin/product-categories", new { nameFr = Unique("Parfums") })).Read<AdminProductCategoryDto>();
        var storeCategory = await (await admin.Post("/api/admin/store-categories", new { nameFr = Unique("Beauté") })).Read<AdminStoreCategoryDto>();

        async Task<Shop> NewShop(decimal price, int percent)
        {
            var owner = await factory.StoreOwnerClient();
            var store = await (await owner.Post("/api/me/store", new
            {
                name = Unique("Boutique"), categoryId = storeCategory.Id, phone = "22 145 678", city = "tunis",
                defaultAffiliatePercent = percent, deliveryFee = 7m, deliveryCities = Tunis, deliveryMinDays = 1, deliveryMaxDays = 2,
            })).Read<MyStoreDto>();
            var product = await (await owner.Post("/api/me/store/products", new
            {
                name = Unique("Musc"), categoryId = category.Id, price, stock = 20, status = "Published",
            })).Read<ProductDto>();
            return new Shop(owner, store, product);
        }

        var a = await NewShop(50m, 10);
        var b = await NewShop(80m, 5);
        var affiliate = factory.CreateClient();
        var auth = await affiliate.RegisterAffiliate();
        affiliate.WithToken(auth.AccessToken);
        foreach (var shop in new[] { a, b })
        {
            var request = await (await affiliate.Post("/api/me/partnerships", new { storeId = shop.Store.Id })).Read<AffiliatePartnershipDto>();
            (await shop.Owner.Post($"/api/me/store/partnerships/{request.Id}/accept")).EnsureSuccessStatusCode();
        }

        using var client = factory.CreateClient();
        var clientAuth = await client.Register();
        client.WithToken(clientAuth.AccessToken);
        async Task<Dictionary<Guid, Guid>> Order(string? code, params (Shop Shop, int Quantity)[] lines)
        {
            foreach (var (shop, quantity) in lines)
                (await client.Post("/api/me/cart/lines", new { productId = shop.Product.Id, quantity, affiliateCode = code })).EnsureSuccessStatusCode();
            var order = await (await client.Post("/api/orders", new
            {
                contact = new { fullName = "Leila Gharbi", phone = "50 112 233", city = "tunis", address = "12 rue de Marseille" },
            })).Read<OrderDto>();
            return order.StoreOrders.ToDictionary(s => s.StoreId, s => s.Id);
        }
        async Task Move(Shop shop, Guid id, string status, string? reason = null, string? notDeliveredReason = null) =>
            (await shop.Owner.Post($"/api/me/store/orders/{id}/status", new { status, reason, notDeliveredReason })).EnsureSuccessStatusCode();

        var code = auth.User.AffiliateCode;
        var first = await Order(code, (a, 2), (b, 1));
        await Move(a, first[a.Store.Id], "Shipped");
        await Move(a, first[a.Store.Id], "Delivered");
        await Move(b, first[b.Store.Id], "Cancelled", "Rupture");
        var second = await Order(null, (a, 1));
        await Move(a, second[a.Store.Id], "Shipped");
        await Move(a, second[a.Store.Id], "Delivered");
        var third = await Order(code, (a, 1));
        await Move(a, third[a.Store.Id], "Shipped");
        await Move(a, third[a.Store.Id], "NotDelivered", "Absent", "Unreachable");

        return new World(admin, affiliate, auth.User, a, b, clientAuth.User.Id);
    }

    private static async Task<T> Get<T>(HttpClient client, string url) =>
        await (await client.GetAsync(new Uri(url, UriKind.Relative), Ct)).Read<T>();

    [Fact]
    public async Task Store_OrdersSalesCoinsAndTopLists()
    {
        using var w = await Arrange();

        var a = await Get<StoreStatsDto>(w.Admin, $"/api/admin/stats/stores/{w.A.Store.Id}");
        Assert.Equal(new ConversionStats(Placed: 3, Open: 0, Delivered: 2, Cancelled: 0, NotDelivered: 1), a.Orders);
        Assert.Equal(new SalesStats(Orders: 2, Amount: 150m, Quantity: 3, OrdersViaAffiliates: 1, AmountViaAffiliates: 100m), a.Sales);
        Assert.Equal(10m, a.CoinsGenerated);
        Assert.Equal(1, a.Partnerships.Accepted);
        var product = Assert.Single(a.TopProducts);
        Assert.Equal((w.A.Product.Id, 2, 3, 150m, 10m), (product.Id, product.Count, product.Quantity, product.Amount, product.Coins));
        var affiliate = Assert.Single(a.TopAffiliates);
        Assert.Equal((w.AffiliateUser.Id, w.AffiliateUser.AffiliateCode, 10m, 100m), (affiliate.Id, affiliate.Detail, affiliate.Coins, affiliate.Amount));

        var b = await Get<StoreStatsDto>(w.Admin, $"/api/admin/stats/stores/{w.B.Store.Id}");
        Assert.Equal(new ConversionStats(1, 0, 0, 1, 0), b.Orders);
        Assert.Equal(0m, b.Sales.Amount);
        Assert.Empty(b.TopProducts);

        // The owner's dashboard shows the same numbers for their own store only.
        var mine = await Get<StoreStatsDto>(w.A.Owner, "/api/me/store/stats");
        Assert.Equal(a.Sales, mine.Sales);
        Assert.Equal(a.CoinsGenerated, mine.CoinsGenerated);
    }

    [Fact]
    public async Task Affiliate_ConversionOfTheirCode_CoinsPerStoreAndProduct()
    {
        using var w = await Arrange();

        var s = await Get<AffiliateStatsDto>(w.Admin, $"/api/admin/stats/affiliates/{w.AffiliateUser.Id}");

        Assert.Equal(new ConversionStats(Placed: 3, Open: 0, Delivered: 1, Cancelled: 1, NotDelivered: 1), s.Orders);
        Assert.Equal(new SalesStats(1, 100m, 2, 1, 100m), s.Sales);
        Assert.Equal((10m, 0m), (s.CoinsEarned, s.CoinsExpected));
        Assert.Equal(2, s.Partnerships.Accepted);
        var store = Assert.Single(s.ByStore);
        Assert.Equal((w.A.Store.Id, 10m), (store.Id, store.Coins));
        var product = Assert.Single(s.ByProduct);
        Assert.Equal((w.A.Product.Id, 2, 100m), (product.Id, product.Quantity, product.Amount));
    }

    [Fact]
    public async Task Product_SalesAndCoins_AndPercent()
    {
        using var w = await Arrange();

        var p = await Get<ProductStatsDto>(w.Admin, $"/api/admin/stats/products/{w.A.Product.Id}");

        Assert.Equal((w.A.Store.Id, 10, true), (p.StoreId, p.AffiliatePercent, p.IsVisible));
        Assert.Equal(new ConversionStats(3, 0, 2, 0, 1), p.Orders);
        Assert.Equal(new SalesStats(2, 150m, 3, 1, 100m), p.Sales);
        Assert.Equal(10m, p.CoinsEarned);
        Assert.Equal(w.AffiliateUser.Id, Assert.Single(p.TopAffiliates).Id);
    }

    [Fact]
    public async Task Period_OutsideTheOrders_IsEmpty()
    {
        using var w = await Arrange();

        var old = await Get<StoreStatsDto>(w.Admin, $"/api/admin/stats/stores/{w.A.Store.Id}?from=2020-01-01&to=2020-01-31");

        Assert.Equal(new StatsPeriod(new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 31)), old.Period);
        Assert.Equal(new ConversionStats(0, 0, 0, 0, 0), old.Orders);
        Assert.Equal(new SalesStats(0, 0m, 0, 0, 0m), old.Sales);
        Assert.Equal(0m, old.CoinsGenerated);
    }

    [Fact]
    public async Task Overview_CountsAtLeastThisTestsOrders_WithADailySeries()
    {
        using var w = await Arrange();

        var o = await Get<AdminOverviewDto>(w.Admin, "/api/admin/stats/overview");

        Assert.Equal(StatsPeriod.DefaultDays, o.Daily.Count);
        Assert.True(o.Orders.Placed >= 4 && o.Orders.Delivered >= 2 && o.Orders.Cancelled >= 1 && o.Orders.NotDelivered >= 1);
        Assert.True(o.Sales.Amount >= 150m && o.Sales.AmountViaAffiliates >= 100m);
        Assert.True(o.Coins.Earned >= 10m && o.ActiveAffiliates >= 1 && o.Stores.Active >= 2);
        Assert.True(o.Daily.Sum(d => d.Placed) >= 4 && o.Daily.Sum(d => d.Delivered) >= 2); // other tests add orders meanwhile: no exact sums
        Assert.InRange(o.TopStores.Count, 1, 5);
        Assert.InRange(o.TopAffiliates.Count, 1, 5);
    }

    [Fact]
    public async Task OrderList_FiltersBehindTheNumbers()
    {
        using var w = await Arrange();

        async Task<int> Count(string query) =>
            (await Get<PagedResult<AdminOrderListItem>>(w.Admin, $"/api/admin/orders?{query}")).Total;

        Assert.Equal(2, await Count($"affiliateId={w.AffiliateUser.Id}"));
        Assert.Equal(3, await Count($"productId={w.A.Product.Id}"));
        Assert.Equal(3, await Count($"clientId={w.ClientId}"));
        Assert.Equal(1, await Count($"affiliateId={w.AffiliateUser.Id}&status=NotDelivered"));
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(2, await Count($"storeId={w.A.Store.Id}&dateField=Delivered&from={today}&to={today}"));
        Assert.Equal(0, await Count($"clientId={w.ClientId}&from=2020-01-01&to=2020-12-31"));
    }

    [Fact]
    public async Task Unknown_404_NonAdmin_403()
    {
        using var w = await Arrange();

        Assert.Equal(HttpStatusCode.NotFound, (await w.Admin.GetAsync(new Uri($"/api/admin/stats/stores/{Guid.NewGuid()}", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await w.Admin.GetAsync(new Uri($"/api/admin/stats/products/{Guid.NewGuid()}", UriKind.Relative), Ct)).StatusCode);
        // A client is not an affiliate: no affiliate stats.
        Assert.Equal(HttpStatusCode.NotFound, (await w.Admin.GetAsync(new Uri($"/api/admin/stats/affiliates/{w.ClientId}", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.A.Owner.GetAsync(new Uri("/api/admin/stats/overview", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Affiliate.GetAsync(new Uri("/api/me/store/stats", UriKind.Relative), Ct)).StatusCode);
    }
}
