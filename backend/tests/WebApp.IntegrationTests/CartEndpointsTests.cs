using System.Net;
using System.Net.Http.Json;
using WebApp.Application.DTOs;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>The cart (013). Each test builds its own stores and products.</summary>
public sealed class CartEndpointsTests(ApiFactory factory)
{
    private static readonly string[] Tunis = ["tunis"];
    private static readonly string[] SizeAxis = ["pointure"];

    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString("N")[..10]}";

    private sealed record Shop(MyStoreDto Store, HttpClient Owner);

    private sealed record Catalog(HttpClient Admin, AdminProductCategoryDto Plain, AdminProductCategoryDto Sized, AdminStoreCategoryDto StoreCategory) : IDisposable
    {
        public void Dispose() => Admin.Dispose();
    }

    private async Task<Catalog> ArrangeCatalog()
    {
        var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);
        var plain = await (await admin.Post("/api/admin/product-categories", new { nameFr = Unique("Parfums") })).Read<AdminProductCategoryDto>();
        var sized = await (await admin.Post("/api/admin/product-categories", new { nameFr = Unique("Chaussures") })).Read<AdminProductCategoryDto>();
        sized = await (await admin.Post($"/api/admin/product-categories/{sized.Id}/attributes", new
        {
            nameFr = "Pointure", type = "SingleChoice", options = new[] { new { name = new { fr = "38" } }, new { name = new { fr = "39" } } },
        })).Read<AdminProductCategoryDto>();
        var storeCategory = await (await admin.Post("/api/admin/store-categories", new { nameFr = Unique("Mode") })).Read<AdminStoreCategoryDto>();
        return new Catalog(admin, plain, sized, storeCategory);
    }

    private async Task<Shop> NewStore(Catalog catalog, decimal deliveryFee)
    {
        var owner = await factory.StoreOwnerClient();
        var store = await (await owner.Post("/api/me/store", new
        {
            name = Unique("Boutique"), categoryId = catalog.StoreCategory.Id, phone = "22 145 678", city = "tunis",
            defaultAffiliatePercent = 10, deliveryFee, deliveryCities = Tunis, deliveryMinDays = 1, deliveryMaxDays = 2,
        })).Read<MyStoreDto>();
        return new Shop(store, owner);
    }

    private static async Task<ProductDto> NewProduct(Shop shop, Catalog catalog, decimal price, int stock = 5) =>
        await (await shop.Owner.Post("/api/me/store/products", new
        {
            name = Unique("Musc"), categoryId = catalog.Plain.Id, price, stock, status = "Published",
        })).Read<ProductDto>();

    private static async Task<ProductDto> NewSizedProduct(Shop shop, Catalog catalog) =>
        await (await shop.Owner.Post("/api/me/store/products", new
        {
            name = Unique("Babouches"), categoryId = catalog.Sized.Id, price = 49m, stock = 0, status = "Published",
            variantAxes = SizeAxis,
            variants = new[]
            {
                new { options = new Dictionary<string, string> { ["pointure"] = "38" }, stock = 3, price = (decimal?)45m },
                new { options = new Dictionary<string, string> { ["pointure"] = "39" }, stock = 0, price = (decimal?)null },
            },
        })).Read<ProductDto>();

    private async Task<HttpClient> Client()
    {
        var client = factory.CreateClient();
        client.WithToken((await client.Register()).AccessToken);
        return client;
    }

    private static async Task<CartDto> AddOk(HttpClient client, Guid productId, int quantity = 1, Guid? variantId = null, string? code = null)
    {
        var response = await client.Post("/api/me/cart/lines", new { productId, variantId, quantity, affiliateCode = code });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Read<CartDto>();
    }

    private static async Task<CartDto> Cart(HttpClient client) =>
        await (await client.GetAsync(new Uri("/api/me/cart", UriKind.Relative), Ct)).Read<CartDto>();

    [Fact]
    public async Task Lines_AreGroupedByStore_WithSubtotalDeliveryAndTotal()
    {
        using var catalog = await ArrangeCatalog();
        var a = await NewStore(catalog, deliveryFee: 7m);
        var b = await NewStore(catalog, deliveryFee: 8m);
        using var _ = a.Owner;
        using var __ = b.Owner;
        var perfume = await NewProduct(a, catalog, 50m);
        var other = await NewProduct(a, catalog, 80m);
        var cream = await NewProduct(b, catalog, 30m);
        using var client = await Client();

        await AddOk(client, perfume.Id, 2);
        await AddOk(client, other.Id);
        var cart = await AddOk(client, cream.Id);

        Assert.Equal([a.Store.Id, b.Store.Id], cart.Stores.Select(s => s.StoreId));
        var storeA = cart.Stores[0];
        Assert.Equal(180m, storeA.Subtotal);
        Assert.Equal(7m, storeA.DeliveryFee);
        Assert.Equal(2, storeA.Lines.Count);
        Assert.Equal(100m, storeA.Lines[0].LineTotal);
        Assert.Equal(4, cart.ItemCount);
        Assert.Equal(210m, cart.ItemsTotal);
        Assert.Equal(15m, cart.DeliveryTotal);
        Assert.Equal(225m, cart.Total);
        Assert.True(cart.CanCheckout);

        // The cart is kept on the server.
        Assert.Equal(225m, (await Cart(client)).Total);
    }

    [Fact]
    public async Task Quantity_AddsUp_StockIsChecked_RemoveAndClear()
    {
        using var catalog = await ArrangeCatalog();
        var shop = await NewStore(catalog, 7m);
        using var _ = shop.Owner;
        var product = await NewProduct(shop, catalog, 20m, stock: 5);
        using var client = await Client();

        await AddOk(client, product.Id, 2);
        var cart = await AddOk(client, product.Id, 3);
        var line = Assert.Single(cart.Stores.Single().Lines);
        Assert.Equal(5, line.Quantity);
        Assert.Equal(5, line.Available);

        var tooMany = await client.Post("/api/me/cart/lines", new { productId = product.Id, quantity = 1 });
        Assert.Equal(HttpStatusCode.Conflict, tooMany.StatusCode);
        Assert.Equal("cart.notEnoughStock", await tooMany.ProblemCode());

        var up = await client.PatchAsJsonAsync($"/api/me/cart/lines/{line.Id}", new { quantity = 6 }, Ct);
        Assert.Equal("cart.notEnoughStock", await up.ProblemCode());
        var down = await (await client.PatchAsJsonAsync($"/api/me/cart/lines/{line.Id}", new { quantity = 1 }, Ct)).Read<CartDto>();
        Assert.Equal(1, down.ItemCount);

        var removed = await (await client.DeleteAsync(new Uri($"/api/me/cart/lines/{line.Id}", UriKind.Relative), Ct)).Read<CartDto>();
        Assert.Empty(removed.Stores);
        Assert.False(removed.CanCheckout);

        await AddOk(client, product.Id);
        var cleared = await (await client.DeleteAsync(new Uri("/api/me/cart", UriKind.Relative), Ct)).Read<CartDto>();
        Assert.Equal(0, cleared.ItemCount);
    }

    [Fact]
    public async Task Variants_AreRequired_PricedAndLabelled()
    {
        using var catalog = await ArrangeCatalog();
        var shop = await NewStore(catalog, 7m);
        using var _ = shop.Owner;
        var shoes = await NewSizedProduct(shop, catalog);
        var size38 = shoes.Variants.Single(v => v.Options["pointure"] == "38");
        var size39 = shoes.Variants.Single(v => v.Options["pointure"] == "39");
        using var client = await Client();

        Assert.Equal("cart.variantRequired", await (await client.Post("/api/me/cart/lines", new { productId = shoes.Id, quantity = 1 })).ProblemCode());
        Assert.Equal("cart.notEnoughStock", await (await client.Post("/api/me/cart/lines",
            new { productId = shoes.Id, variantId = size39.Id, quantity = 1 })).ProblemCode());

        var line = (await AddOk(client, shoes.Id, 2, size38.Id)).Stores.Single().Lines.Single();
        Assert.Equal(45m, line.UnitPrice); // the variant's own price
        var option = Assert.Single(line.Options);
        Assert.Equal("Pointure", option.Axis.Fr);
        Assert.Equal("38", option.Value.Fr);
    }

    [Fact]
    public async Task AffiliateCode_PerLine_ValidOnPartnerStore_RefusedElsewhere()
    {
        using var catalog = await ArrangeCatalog();
        var partner = await NewStore(catalog, 7m);
        var other = await NewStore(catalog, 8m);
        using var _ = partner.Owner;
        using var __ = other.Owner;
        var perfume = await NewProduct(partner, catalog, 50m);
        var cream = await NewProduct(other, catalog, 30m);

        // An affiliate who works with the first store only.
        using var affiliate = factory.CreateClient();
        var auth = await affiliate.RegisterAffiliate();
        affiliate.WithToken(auth.AccessToken);
        var request = await (await affiliate.Post("/api/me/partnerships", new { storeId = partner.Store.Id })).Read<AffiliatePartnershipDto>();
        (await partner.Owner.Post($"/api/me/store/partnerships/{request.Id}/accept")).EnsureSuccessStatusCode();
        var code = auth.User.AffiliateCode!;

        using var client = await Client();
        await AddOk(client, perfume.Id, code: $"#{code.ToLowerInvariant()}");
        var cart = await AddOk(client, cream.Id, code: code);

        var ok = cart.Stores.Single(s => s.StoreId == partner.Store.Id).Lines.Single();
        Assert.Equal(code, ok.AffiliateCode);
        Assert.True(ok.Code!.Valid);
        Assert.Equal("Sana T.", ok.Code.AffiliateName);
        var refused = cart.Stores.Single(s => s.StoreId == other.Store.Id).Lines.Single();
        Assert.False(refused.Code!.Valid);
        Assert.Equal("affiliateCode.notPartner", refused.Code.Error);
        Assert.False(cart.CanCheckout); // a refused code must be fixed or removed first

        var withoutCode = await (await client.DeleteAsync(new Uri($"/api/me/cart/lines/{refused.Id}/code", UriKind.Relative), Ct)).Read<CartDto>();
        Assert.True(withoutCode.CanCheckout);

        var malformed = await client.PutAsJsonAsync($"/api/me/cart/lines/{refused.Id}/code", new { code = "n'importe quoi" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("cart.codeInvalid", await malformed.ProblemCode());

        // The affiliate's own code on their own purchase never counts.
        await AddOk(affiliate, perfume.Id, code: code);
        var own = (await Cart(affiliate)).Stores.Single().Lines.Single();
        Assert.Equal("affiliateCode.ownCode", own.Code!.Error);
    }

    [Fact]
    public async Task UnavailableLines_AreFlagged_LeftOutOfTotals_AndBlockCheckout()
    {
        using var catalog = await ArrangeCatalog();
        var shop = await NewStore(catalog, 7m);
        using var _ = shop.Owner;
        var kept = await NewProduct(shop, catalog, 10m);
        var fake = await NewProduct(shop, catalog, 999m);
        using var client = await Client();
        await AddOk(client, kept.Id);
        await AddOk(client, fake.Id);

        (await catalog.Admin.Post($"/api/admin/products/{fake.Id}/disable", new { reason = "Prix absurde" })).EnsureSuccessStatusCode();
        var cart = await Cart(client);
        var line = cart.Stores.Single().Lines.Single(l => l.ProductId == fake.Id);
        Assert.Equal("productUnavailable", line.Issue);
        Assert.Equal(10m, cart.ItemsTotal);
        Assert.False(cart.CanCheckout);

        // A closed store: everything is flagged, nothing to deliver.
        (await shop.Owner.Post("/api/me/store/close")).EnsureSuccessStatusCode();
        cart = await Cart(client);
        Assert.Equal("storeClosed", cart.Stores.Single().Lines.Single(l => l.ProductId == kept.Id).Issue);
        Assert.Equal(0m, cart.Total);
        Assert.Equal("cart.storeClosed", await (await client.Post("/api/me/cart/lines", new { productId = kept.Id, quantity = 1 })).ProblemCode());
    }

    [Fact]
    public async Task GuestCart_Preview_ThenMergedAtSignIn()
    {
        using var catalog = await ArrangeCatalog();
        var shop = await NewStore(catalog, 7m);
        using var _ = shop.Owner;
        var perfume = await NewProduct(shop, catalog, 50m);
        var lines = new[]
        {
            new { productId = perfume.Id, variantId = (Guid?)null, quantity = 2, affiliateCode = (string?)null },
            new { productId = Guid.NewGuid(), variantId = (Guid?)null, quantity = 1, affiliateCode = (string?)null }, // unknown: dropped
        };

        using var guest = factory.CreateClient();
        var preview = await (await guest.Post("/api/cart/preview", new { lines })).Read<CartDto>();
        var line = preview.Stores.Single().Lines.Single();
        Assert.Null(line.Id);
        Assert.Equal(100m, preview.ItemsTotal);
        Assert.Equal(107m, preview.Total);

        using var client = await Client();
        await AddOk(client, perfume.Id);
        var merged = await (await client.Post("/api/me/cart/merge", new { lines })).Read<CartDto>();
        Assert.Equal(3, merged.Stores.Single().Lines.Single().Quantity);
    }

    [Fact]
    public async Task Cart_IsPrivate()
    {
        using var catalog = await ArrangeCatalog();
        var shop = await NewStore(catalog, 7m);
        using var _ = shop.Owner;
        var product = await NewProduct(shop, catalog, 10m);
        using var client = await Client();
        using var other = await Client();
        var line = (await AddOk(client, product.Id)).Stores.Single().Lines.Single();

        var patch = await other.PatchAsJsonAsync($"/api/me/cart/lines/{line.Id}", new { quantity = 2 }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
        Assert.Equal(0, (await Cart(other)).ItemCount);

        using var visitor = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync(new Uri("/api/me/cart", UriKind.Relative), Ct)).StatusCode);
    }
}
