using System.Net;
using WebApp.Application.DTOs;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>Tests share one database: each builds its own categories / store and filters on them.</summary>
public sealed class ShopEndpointsTests(ApiFactory factory)
{
    private static readonly string[] Tunis = ["tunis"];

    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString("N")[..10]}";

    private sealed record Shop(string StoreSlug, string StoreCategorySlug, string ParentSlug, string ChildSlug, HttpClient Owner);

    /// <summary>A store with: a cheap product in the parent category, a variant product in the child, a draft.</summary>
    private async Task<Shop> Arrange()
    {
        using var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);
        var parent = await (await admin.Post("/api/admin/product-categories", new { nameFr = Unique("Maison") })).Read<AdminProductCategoryDto>();
        var child = await (await admin.Post("/api/admin/product-categories", new { parentId = parent.Id, nameFr = Unique("Cuisine") })).Read<AdminProductCategoryDto>();
        child = await (await admin.Post($"/api/admin/product-categories/{child.Id}/attributes", new
        {
            nameFr = "Taille", type = "SingleChoice", options = new[] { new { name = new { fr = "Petit" } }, new { name = new { fr = "Grand" } } },
        })).Read<AdminProductCategoryDto>();
        var storeCategory = await (await admin.Post("/api/admin/store-categories", new { nameFr = Unique("Déco") })).Read<AdminStoreCategoryDto>();

        var owner = await factory.StoreOwnerClient();
        var store = await (await owner.Post("/api/me/store", new
        {
            name = Unique("Boutique"), categoryId = storeCategory.Id, phone = "22 145 678", city = "sfax",
            defaultAffiliatePercent = 10, deliveryFee = 7m, deliveryCities = Tunis, deliveryMinDays = 1, deliveryMaxDays = 2,
        })).Read<MyStoreDto>();

        object P(string name, Guid category, decimal price, string status = "Published", object[]? variants = null) => new
        {
            name, categoryId = category, price, stock = 5, status,
            variantAxes = variants is null ? Array.Empty<string>() : ["taille"], variants = variants ?? [],
        };
        (await owner.Post("/api/me/store/products", P("Bol en terre", parent.Id, 15m))).EnsureSuccessStatusCode();
        (await owner.Post("/api/me/store/products", P("Plat à tajine", child.Id, 60m, variants:
        [
            new { options = new Dictionary<string, string> { ["taille"] = "petit" }, stock = 2, price = 45m },
            new { options = new Dictionary<string, string> { ["taille"] = "grand" }, stock = 0 },
        ]))).EnsureSuccessStatusCode();
        (await owner.Post("/api/me/store/products", P("Brouillon", parent.Id, 99m, status: "Draft"))).EnsureSuccessStatusCode();

        return new Shop(store.Slug, storeCategory.Slug, parent.Slug, child.Slug, owner);
    }

    private async Task<PagedResult<ProductCardDto>> Products(string query)
    {
        using var visitor = factory.CreateClient();
        return await (await visitor.GetAsync(new Uri($"/api/products?{query}", UriKind.Relative), Ct)).Read<PagedResult<ProductCardDto>>();
    }

    [Fact]
    public async Task Category_IncludesItsSubCategories_DraftsHidden()
    {
        var shop = await Arrange();
        using var _ = shop.Owner;

        var parent = await Products($"category={shop.ParentSlug}");
        Assert.Equal(["Plat à tajine", "Bol en terre"], parent.Items.Select(p => p.Name)); // newest first, draft hidden

        var child = await Products($"category={shop.ChildSlug}");
        var dish = Assert.Single(child.Items);
        Assert.True(dish.HasVariants);
        Assert.Equal(45m, dish.MinPrice); // the cheapest variant
        Assert.True(dish.InStock);
    }

    [Fact]
    public async Task Sort_ByFromPrice_AndFilterByStore()
    {
        var shop = await Arrange();
        using var _ = shop.Owner;

        var cheapFirst = await Products($"store={shop.StoreSlug}&sort=PriceAsc");
        Assert.Equal(["Bol en terre", "Plat à tajine"], cheapFirst.Items.Select(p => p.Name));
        var dearFirst = await Products($"store={shop.StoreSlug}&sort=PriceDesc");
        Assert.Equal(["Plat à tajine", "Bol en terre"], dearFirst.Items.Select(p => p.Name));
        Assert.All(dearFirst.Items, p => Assert.Equal(shop.StoreSlug, p.StoreSlug));
    }

    [Fact]
    public async Task ClosedStore_ProductsStayVisible_MarkedClosed()
    {
        var shop = await Arrange();
        using var _ = shop.Owner;
        await shop.Owner.Post("/api/me/store/close");

        var list = await Products($"store={shop.StoreSlug}");

        Assert.Equal(2, list.Total);
        Assert.All(list.Items, p => Assert.False(p.StoreOpen));
    }

    [Fact]
    public async Task UnknownCategoryOrStore_Returns404()
    {
        using var visitor = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri("/api/products?category=nope", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri("/api/products?store=nope", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task Stores_FilterByCategoryAndCity_WithProductCount()
    {
        var shop = await Arrange();
        using var _ = shop.Owner;
        using var visitor = factory.CreateClient();

        var byCategory = await (await visitor.GetAsync(new Uri($"/api/stores?category={shop.StoreCategorySlug}", UriKind.Relative), Ct))
            .Read<PagedResult<StoreCardDto>>();
        var store = Assert.Single(byCategory.Items);
        Assert.Equal(shop.StoreSlug, store.Slug);
        Assert.Equal(2, store.ProductCount); // drafts not counted
        Assert.Equal("sfax", store.City);

        var otherCity = await (await visitor.GetAsync(new Uri($"/api/stores?category={shop.StoreCategorySlug}&city=tunis", UriKind.Relative), Ct))
            .Read<PagedResult<StoreCardDto>>();
        Assert.Empty(otherCity.Items);
    }
}
