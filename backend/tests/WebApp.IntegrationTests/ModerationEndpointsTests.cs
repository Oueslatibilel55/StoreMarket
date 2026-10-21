using System.Net;
using WebApp.Application.DTOs;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>Admin moderation (010). Tests share one database: each builds its own store and filters on it.</summary>
public sealed class ModerationEndpointsTests(ApiFactory factory)
{
    private static readonly string[] Tunis = ["tunis"];

    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString("N")[..10]}";

    private sealed record Shop(MyStoreDto Store, ProductDto Cheap, ProductDto Dear, HttpClient Owner, HttpClient Admin) : IDisposable
    {
        public void Dispose()
        {
            Owner.Dispose();
            Admin.Dispose();
        }
    }

    /// <summary>A store with two published products (15 and 999 999 TND).</summary>
    private async Task<Shop> Arrange()
    {
        var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);
        var category = await (await admin.Post("/api/admin/product-categories", new { nameFr = Unique("Parfums") })).Read<AdminProductCategoryDto>();
        var storeCategory = await (await admin.Post("/api/admin/store-categories", new { nameFr = Unique("Beauté") })).Read<AdminStoreCategoryDto>();

        var owner = await factory.StoreOwnerClient();
        var store = await (await owner.Post("/api/me/store", new
        {
            name = Unique("Parfumerie"), categoryId = storeCategory.Id, phone = "22 145 678", city = "sousse",
            defaultAffiliatePercent = 10, deliveryFee = 7m, deliveryCities = Tunis, deliveryMinDays = 1, deliveryMaxDays = 2,
        })).Read<MyStoreDto>();

        async Task<ProductDto> Product(string name, decimal price) =>
            await (await owner.Post("/api/me/store/products", new { name, categoryId = category.Id, price, stock = 5, status = "Published" }))
                .Read<ProductDto>();
        var cheap = await Product(Unique("Musc"), 15m);
        var dear = await Product(Unique("Faux parfum"), 999_999m);
        return new Shop(store, cheap, dear, owner, admin);
    }

    private async Task<HttpStatusCode> PublicStatus(string url)
    {
        using var visitor = factory.CreateClient();
        return (await visitor.GetAsync(new Uri(url, UriKind.Relative), Ct)).StatusCode;
    }

    private static object Reason(string reason = "Faux magasin signalé") => new { reason };

    [Fact]
    public async Task DisableStore_HidesItAndItsProducts_OwnerSeesReason_EnableBringsItBack()
    {
        using var shop = await Arrange();

        var disabled = await (await shop.Admin.Post($"/api/admin/stores/{shop.Store.Id}/disable", Reason())).Read<AdminStoreDto>();
        Assert.Equal(StoreStatus.Disabled, disabled.Status);

        Assert.Equal(HttpStatusCode.NotFound, await PublicStatus($"/api/stores/{shop.Store.Slug}"));
        Assert.Equal(HttpStatusCode.NotFound, await PublicStatus($"/api/products/{shop.Cheap.Slug}"));

        var mine = await (await shop.Owner.GetAsync(new Uri("/api/me/store", UriKind.Relative), Ct)).Read<MyStoreDto>();
        Assert.Equal(StoreStatus.Disabled, mine.Status);
        Assert.Equal("Faux magasin signalé", mine.ModerationReason);
        var reopen = await shop.Owner.Post("/api/me/store/reopen");
        Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);

        var audit = await (await shop.Admin.GetAsync(new Uri($"/api/admin/audit?targetId={shop.Store.Id}", UriKind.Relative), Ct))
            .Read<PagedResult<AuditEntryDto>>();
        var entry = Assert.Single(audit.Items);
        Assert.Equal("store.disable", entry.Action);
        Assert.Equal("Faux magasin signalé", entry.Reason);

        (await shop.Admin.Post($"/api/admin/stores/{shop.Store.Id}/enable")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, await PublicStatus($"/api/products/{shop.Cheap.Slug}"));
    }

    [Fact]
    public async Task Disable_WithoutReason_IsRefused()
    {
        using var shop = await Arrange();

        var response = await shop.Admin.Post($"/api/admin/stores/{shop.Store.Id}/disable", Reason(" "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("moderation.reasonRequired", await response.ProblemCode());
    }

    [Fact]
    public async Task DisableProduct_HiddenFromShop_OwnerSeesItDisabled()
    {
        using var shop = await Arrange();

        var response = await shop.Admin.Post($"/api/admin/products/{shop.Dear.Id}/disable", Reason("Prix absurde"));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, await PublicStatus($"/api/products/{shop.Dear.Slug}"));
        Assert.Equal(HttpStatusCode.OK, await PublicStatus($"/api/products/{shop.Cheap.Slug}"));

        var mine = await (await shop.Owner.GetAsync(new Uri($"/api/me/store/products/{shop.Dear.Id}", UriKind.Relative), Ct)).Read<ProductDto>();
        Assert.True(mine.IsDisabled);
        Assert.Equal("Prix absurde", mine.ModerationReason);

        var disabledList = await (await shop.Admin.GetAsync(
            new Uri($"/api/admin/products?storeId={shop.Store.Id}&state=Disabled", UriKind.Relative), Ct)).Read<PagedResult<AdminProductListItem>>();
        Assert.Equal(shop.Dear.Id, Assert.Single(disabledList.Items).Id);

        (await shop.Admin.Post($"/api/admin/products/{shop.Dear.Id}/enable")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, await PublicStatus($"/api/products/{shop.Dear.Slug}"));
    }

    [Fact]
    public async Task ProductList_SortsByPrice_AndFiltersByPriceRange()
    {
        using var shop = await Arrange();

        var dearFirst = await (await shop.Admin.GetAsync(
            new Uri($"/api/admin/products?storeId={shop.Store.Id}&sort=PriceDesc", UriKind.Relative), Ct)).Read<PagedResult<AdminProductListItem>>();
        Assert.Equal([shop.Dear.Id, shop.Cheap.Id], dearFirst.Items.Select(p => p.Id));

        var expensive = await (await shop.Admin.GetAsync(
            new Uri($"/api/admin/products?storeId={shop.Store.Id}&minPrice=100000", UriKind.Relative), Ct)).Read<PagedResult<AdminProductListItem>>();
        Assert.Equal(shop.Dear.Id, Assert.Single(expensive.Items).Id);
    }

    [Fact]
    public async Task BulkDisable_DisablesTheSelection_AndSkipsAlreadyDisabled()
    {
        using var shop = await Arrange();
        await shop.Admin.Post($"/api/admin/products/{shop.Dear.Id}/disable", Reason());

        var result = await (await shop.Admin.Post("/api/admin/products/disable", new { ids = new[] { shop.Cheap.Id, shop.Dear.Id }, reason = "Lot suspect" }))
            .Read<BulkModerationResult>();

        Assert.Equal(1, result.Changed);
        Assert.Equal(HttpStatusCode.NotFound, await PublicStatus($"/api/products/{shop.Cheap.Slug}"));

        var empty = await shop.Admin.Post("/api/admin/products/disable", new { ids = Array.Empty<Guid>(), reason = "x" });
        Assert.Equal("moderation.selectionInvalid", await empty.ProblemCode());
    }

    [Fact]
    public async Task DeleteStore_IsSoft_OwnerCanNoLongerEdit_ListedApart()
    {
        using var shop = await Arrange();

        var deleted = await (await shop.Admin.Post($"/api/admin/stores/{shop.Store.Id}/delete", Reason("Arnaque"))).Read<AdminStoreDto>();
        Assert.True(deleted.IsDeleted);

        var update = await shop.Owner.Post("/api/me/store/products", new { name = "Nouveau", categoryId = shop.Cheap.Category.Id, price = 10m, status = "Draft" });
        Assert.Equal("store.deleted", await update.ProblemCode());
        Assert.Equal("store.deleted", await (await shop.Admin.Post($"/api/admin/stores/{shop.Store.Id}/enable")).ProblemCode());

        var live = await (await shop.Admin.GetAsync(new Uri($"/api/admin/stores?search={shop.Store.Slug}", UriKind.Relative), Ct))
            .Read<PagedResult<AdminStoreListItem>>();
        Assert.Empty(live.Items);
        var gone = await (await shop.Admin.GetAsync(new Uri($"/api/admin/stores?search={shop.Store.Slug}&deleted=true", UriKind.Relative), Ct))
            .Read<PagedResult<AdminStoreListItem>>();
        Assert.Equal("Arnaque", Assert.Single(gone.Items).ModerationReason);
    }

    [Fact]
    public async Task RevokingStoreOwner_DisablesTheStore_UntilRoleGivenBackAndAdminEnables()
    {
        using var shop = await Arrange();
        var detail = await (await shop.Admin.GetAsync(new Uri($"/api/admin/stores/{shop.Store.Id}", UriKind.Relative), Ct)).Read<AdminStoreDto>();

        (await shop.Admin.DeleteAsync(new Uri($"/api/admin/users/{detail.Owner.Id}/roles/StoreOwner", UriKind.Relative), Ct)).EnsureSuccessStatusCode();

        var after = await (await shop.Admin.GetAsync(new Uri($"/api/admin/stores/{shop.Store.Id}", UriKind.Relative), Ct)).Read<AdminStoreDto>();
        Assert.Equal(StoreStatus.Disabled, after.Status);
        Assert.Equal("store.ownerRoleRevoked", after.ModerationReason);
        Assert.Equal("store.ownerNotStoreOwner", await (await shop.Admin.Post($"/api/admin/stores/{shop.Store.Id}/enable")).ProblemCode());

        (await shop.Admin.Post($"/api/admin/users/{detail.Owner.Id}/roles/StoreOwner")).EnsureSuccessStatusCode();
        var enabled = await (await shop.Admin.Post($"/api/admin/stores/{shop.Store.Id}/enable")).Read<AdminStoreDto>();
        Assert.Equal(StoreStatus.Active, enabled.Status);
    }

    [Fact]
    public async Task NonAdmin_IsForbidden()
    {
        using var shop = await Arrange();

        Assert.Equal(HttpStatusCode.Forbidden, (await shop.Owner.GetAsync(new Uri("/api/admin/stores", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await shop.Owner.Post($"/api/admin/products/{shop.Cheap.Id}/disable", Reason())).StatusCode);
    }
}
