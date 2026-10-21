using System.Net;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Application.DTOs;
using WebApp.Application.Interfaces;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>Coin ledger (019): coins are credited on delivery, traceable, idempotent, corrected only by adjustments.</summary>
public sealed class CoinLedgerEndpointsTests(ApiFactory factory)
{
    private static readonly string[] Tunis = ["tunis"];

    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString("N")[..10]}";

    private sealed record Shop(HttpClient Owner, MyStoreDto Store, ProductDto Product);

    private sealed record World(HttpClient Admin, HttpClient Affiliate, UserDto AffiliateUser, Shop A, Shop B) : IDisposable
    {
        public void Dispose()
        {
            Admin.Dispose();
            Affiliate.Dispose();
            A.Owner.Dispose();
            B.Owner.Dispose();
        }
    }

    /// <summary>Two stores (product 50 TND at 10 % and 80 TND at 5 %), one affiliate partner of both.</summary>
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
        return new World(admin, affiliate, auth.User, a, b);
    }

    /// <summary>A new client orders with the affiliate's code; returns the store order id per store.</summary>
    private async Task<Dictionary<Guid, Guid>> Order(World w, params (Shop Shop, int Quantity)[] lines)
    {
        using var client = factory.CreateClient();
        client.WithToken((await client.Register()).AccessToken);
        foreach (var (shop, quantity) in lines)
            (await client.Post("/api/me/cart/lines", new { productId = shop.Product.Id, quantity, affiliateCode = w.AffiliateUser.AffiliateCode })).EnsureSuccessStatusCode();
        var order = await (await client.Post("/api/orders", new
        {
            contact = new { fullName = "Leila Gharbi", phone = "50 112 233", city = "tunis", address = "12 rue de Marseille" },
        })).Read<OrderDto>();
        return order.StoreOrders.ToDictionary(s => s.StoreId, s => s.Id);
    }

    private static async Task Deliver(Shop shop, Guid storeOrderId)
    {
        (await shop.Owner.Post($"/api/me/store/orders/{storeOrderId}/status", new { status = "Shipped" })).EnsureSuccessStatusCode();
        (await shop.Owner.Post($"/api/me/store/orders/{storeOrderId}/status", new { status = "Delivered" })).EnsureSuccessStatusCode();
    }

    private static async Task<CoinSummaryDto> Coins(World w) =>
        await (await w.Affiliate.GetAsync(new Uri("/api/me/coins", UriKind.Relative), Ct)).Read<CoinSummaryDto>();

    private async Task AssertBalanceMatchesLedger(Guid affiliateId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var (cached, sum) = await scope.ServiceProvider.GetRequiredService<ICoinQueries>().CheckBalanceAsync(affiliateId, Ct);
        Assert.Equal(sum, cached);
    }

    [Fact]
    public async Task Delivered_CreditsOneTraceableEntryPerLine_ExpectedBecomesEarned()
    {
        using var w = await Arrange();
        var storeOrders = await Order(w, (w.A, 2), (w.B, 1));

        var before = await Coins(w);
        Assert.Equal(0m, before.Balance);
        Assert.Equal(14m, before.Expected); // 100 × 10 % + 80 × 5 %

        await Deliver(w.A, storeOrders[w.A.Store.Id]);

        var after = await Coins(w);
        Assert.Equal(10m, after.Balance);
        Assert.Equal(10m, after.TotalEarned);
        Assert.Equal(4m, after.Expected);

        var entry = Assert.Single((await (await w.Affiliate.GetAsync(new Uri("/api/me/coins/entries", UriKind.Relative), Ct))
            .Read<PagedResult<CoinEntryDto>>()).Items);
        Assert.Equal(CoinEntryType.Earned, entry.Type);
        Assert.Equal(10m, entry.Coins);
        Assert.Equal(w.A.Store.Id, entry.StoreId);
        Assert.Equal(w.A.Product.Id, entry.ProductId);
        Assert.Equal(2, entry.Quantity);
        Assert.Equal(100m, entry.LineTotal);
        Assert.Equal(10, entry.AffiliatePercent);
        Assert.Equal("Leila Gharbi", entry.ClientName);
        Assert.Equal("+216 •• ••• 233", entry.ClientPhone); // masked for the affiliate
        Assert.Matches(@"^CMD-\d{4}-\d{6}$", entry.OrderReference!);

        // The admin sees the same entry with the full phone.
        var adminView = await (await w.Admin.GetAsync(new Uri($"/api/admin/coins/entries?affiliateId={w.AffiliateUser.Id}", UriKind.Relative), Ct))
            .Read<PagedResult<CoinEntryDto>>();
        Assert.Equal("+21650112233", Assert.Single(adminView.Items).ClientPhone);

        await AssertBalanceMatchesLedger(w.AffiliateUser.Id);
    }

    [Fact]
    public async Task CancelledOrNotDelivered_WriteNothing()
    {
        using var w = await Arrange();
        var storeOrders = await Order(w, (w.A, 1), (w.B, 1));

        (await w.A.Owner.Post($"/api/me/store/orders/{storeOrders[w.A.Store.Id]}/status", new { status = "Cancelled", reason = "Rupture" })).EnsureSuccessStatusCode();
        (await w.B.Owner.Post($"/api/me/store/orders/{storeOrders[w.B.Store.Id]}/status", new { status = "Shipped" })).EnsureSuccessStatusCode();
        (await w.B.Owner.Post($"/api/me/store/orders/{storeOrders[w.B.Store.Id]}/status",
            new { status = "NotDelivered", notDeliveredReason = "Refused" })).EnsureSuccessStatusCode();

        var coins = await Coins(w);
        Assert.Equal(0m, coins.Balance);
        Assert.Equal(0m, coins.Expected);
        Assert.Empty((await (await w.Affiliate.GetAsync(new Uri("/api/me/coins/entries", UriKind.Relative), Ct)).Read<PagedResult<CoinEntryDto>>()).Items);
    }

    [Fact]
    public async Task TwoStoresDeliveringAtOnce_BothCredited_BalanceMatchesLedger()
    {
        using var w = await Arrange();
        var storeOrders = await Order(w, (w.A, 3), (w.B, 2));
        foreach (var (shop, id) in new[] { (w.A, storeOrders[w.A.Store.Id]), (w.B, storeOrders[w.B.Store.Id]) })
            (await shop.Owner.Post($"/api/me/store/orders/{id}/status", new { status = "Shipped" })).EnsureSuccessStatusCode();

        var results = await Task.WhenAll(
            w.A.Owner.Post($"/api/me/store/orders/{storeOrders[w.A.Store.Id]}/status", new { status = "Delivered" }),
            w.B.Owner.Post($"/api/me/store/orders/{storeOrders[w.B.Store.Id]}/status", new { status = "Delivered" }));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(23m, (await Coins(w)).Balance); // 150 × 10 % + 160 × 5 %
        await AssertBalanceMatchesLedger(w.AffiliateUser.Id);
    }

    private static async Task<T> Get<T>(HttpClient client, string url) =>
        await (await client.GetAsync(new Uri(url, UriKind.Relative), Ct)).Read<T>();

    [Fact]
    public async Task Wallet_FiltersBreakdownAndExport_OnlyMyEntries()
    {
        using var w = await Arrange();
        var storeOrders = await Order(w, (w.A, 2), (w.B, 1));
        await Deliver(w.A, storeOrders[w.A.Store.Id]);
        await Deliver(w.B, storeOrders[w.B.Store.Id]);

        // Filters: store, product, type, period.
        var byStore = await Get<PagedResult<CoinEntryDto>>(w.Affiliate, $"/api/me/coins/entries?storeId={w.B.Store.Id}");
        Assert.Equal(w.B.Product.Id, Assert.Single(byStore.Items).ProductId);
        var byProduct = await Get<PagedResult<CoinEntryDto>>(w.Affiliate, $"/api/me/coins/entries?productId={w.A.Product.Id}");
        Assert.Equal(10m, Assert.Single(byProduct.Items).Coins);
        Assert.Empty((await Get<PagedResult<CoinEntryDto>>(w.Affiliate, "/api/me/coins/entries?type=Adjustment")).Items);
        var tomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Empty((await Get<PagedResult<CoinEntryDto>>(w.Affiliate, $"/api/me/coins/entries?from={tomorrow}")).Items);

        // Breakdown per product and per store, biggest first.
        var products = await Get<List<CoinBreakdownRow>>(w.Affiliate, "/api/me/coins/breakdown?by=Product");
        Assert.Equal([w.A.Product.Id, w.B.Product.Id], products.Select(r => r.Id));
        Assert.Equal((2, 100m, 10m), (products[0].Quantity, products[0].Sales, products[0].Coins));
        Assert.Equal(w.B.Store.Name, products[1].StoreName);
        var stores = await Get<List<CoinBreakdownRow>>(w.Affiliate, "/api/me/coins/breakdown?by=Store");
        Assert.Equal([10m, 4m], stores.Select(r => r.Coins));
        Assert.Empty(await Get<List<CoinBreakdownRow>>(w.Affiliate, $"/api/me/coins/breakdown?from={tomorrow}"));

        // An adjustment counts in the balance and the export, not in the breakdown.
        (await w.Admin.Post("/api/admin/coins/adjustments", new { affiliateId = w.AffiliateUser.Id, coins = 1m, reason = "Geste commercial" })).EnsureSuccessStatusCode();
        Assert.Equal(14m, (await Get<List<CoinBreakdownRow>>(w.Affiliate, "/api/me/coins/breakdown?by=Store")).Sum(r => r.Coins));
        var export = await Get<List<CoinEntryDto>>(w.Affiliate, "/api/me/coins/export");
        Assert.Equal(3, export.Count);
        Assert.All(export.Where(e => e.Type == CoinEntryType.Earned), e => Assert.Equal("+216 •• ••• 233", e.ClientPhone));

        // Another affiliate sees none of it.
        using var other = factory.CreateClient();
        other.WithToken((await other.RegisterAffiliate()).AccessToken);
        Assert.Empty(await Get<List<CoinEntryDto>>(other, "/api/me/coins/export"));
        Assert.Empty(await Get<List<CoinBreakdownRow>>(other, "/api/me/coins/breakdown"));
    }

    [Fact]
    public async Task Admin_Adjusts_WithReason_Audited_LedgerStaysAppendOnly()
    {
        using var w = await Arrange();
        var storeOrders = await Order(w, (w.A, 2));
        await Deliver(w.A, storeOrders[w.A.Store.Id]);
        var earned = Assert.Single((await (await w.Admin.GetAsync(new Uri($"/api/admin/coins/entries?affiliateId={w.AffiliateUser.Id}", UriKind.Relative), Ct))
            .Read<PagedResult<CoinEntryDto>>()).Items);

        var noReason = await w.Admin.Post("/api/admin/coins/adjustments", new { affiliateId = w.AffiliateUser.Id, coins = -2.5m, reason = " " });
        Assert.Equal("coins.reasonRequired", await noReason.ProblemCode());

        var adjusted = await (await w.Admin.Post("/api/admin/coins/adjustments",
            new { affiliateId = w.AffiliateUser.Id, coins = -2.5m, reason = "Commission comptée deux fois", relatedEntryId = earned.Id })).Read<CoinSummaryDto>();
        Assert.Equal(7.5m, adjusted.Balance);
        Assert.Equal(10m, adjusted.TotalEarned); // an adjustment is not earned

        var entries = (await (await w.Admin.GetAsync(new Uri($"/api/admin/coins/entries?affiliateId={w.AffiliateUser.Id}", UriKind.Relative), Ct))
            .Read<PagedResult<CoinEntryDto>>()).Items;
        Assert.Equal(2, entries.Count); // the earned entry is still there, untouched
        Assert.Contains(entries, e => e.Type == CoinEntryType.Adjustment && e.RelatedEntryId == earned.Id && e.Reason == "Commission comptée deux fois");

        var audit = await (await w.Admin.GetAsync(new Uri($"/api/admin/audit?targetId={w.AffiliateUser.Id}", UriKind.Relative), Ct)).Read<PagedResult<AuditEntryDto>>();
        Assert.Contains(audit.Items, e => e.Action == "coins.adjustment");
        await AssertBalanceMatchesLedger(w.AffiliateUser.Id);

        // Only affiliates have coins; only admins adjust.
        using var buyer = factory.CreateClient();
        var buyerAuth = await buyer.Register();
        buyer.WithToken(buyerAuth.AccessToken);
        Assert.Equal("coins.affiliateNotFound",
            await (await w.Admin.Post("/api/admin/coins/adjustments", new { affiliateId = buyerAuth.User.Id, coins = 1m, reason = "x" })).ProblemCode());
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync(new Uri("/api/me/coins", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await w.Affiliate.Post("/api/admin/coins/adjustments", new { affiliateId = w.AffiliateUser.Id, coins = 100m, reason = "moi" })).StatusCode);
    }
}
