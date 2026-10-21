using System.Net;
using WebApp.Application.DTOs;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>Affiliate ↔ store partnerships and the affiliate code check (012). Each test builds its own store.</summary>
public sealed class PartnershipEndpointsTests(ApiFactory factory)
{
    private static readonly string[] Tunis = ["tunis"];

    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString("N")[..10]}";

    private sealed record Shop(MyStoreDto Store, ProductDto Product, HttpClient Owner, HttpClient Admin) : IDisposable
    {
        public void Dispose()
        {
            Owner.Dispose();
            Admin.Dispose();
        }
    }

    private sealed record Affiliate(HttpClient Client, UserDto User) : IDisposable
    {
        public string Code => User.AffiliateCode!;
        public void Dispose() => Client.Dispose();
    }

    /// <summary>A store (default 10 %) with one published product at 12 %.</summary>
    private async Task<Shop> Arrange(HttpClient? owner = null)
    {
        var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);
        var category = await (await admin.Post("/api/admin/product-categories", new { nameFr = Unique("Parfums") })).Read<AdminProductCategoryDto>();
        var storeCategory = await (await admin.Post("/api/admin/store-categories", new { nameFr = Unique("Beauté") })).Read<AdminStoreCategoryDto>();

        owner ??= await factory.StoreOwnerClient();
        var store = await (await owner.Post("/api/me/store", new
        {
            name = Unique("Parfumerie"), categoryId = storeCategory.Id, phone = "22 145 678", city = "sousse",
            defaultAffiliatePercent = 10, deliveryFee = 7m, deliveryCities = Tunis, deliveryMinDays = 1, deliveryMaxDays = 2,
        })).Read<MyStoreDto>();
        var product = await (await owner.Post("/api/me/store/products", new
        {
            name = Unique("Musc"), categoryId = category.Id, price = 50m, stock = 5, status = "Published", affiliatePercent = 12,
        })).Read<ProductDto>();
        return new Shop(store, product, owner, admin);
    }

    private async Task<Affiliate> NewAffiliate()
    {
        var client = factory.CreateClient();
        var auth = await client.RegisterAffiliate();
        client.WithToken(auth.AccessToken);
        return new Affiliate(client, auth.User);
    }

    private static async Task<AffiliatePartnershipDto> Ask(Affiliate affiliate, Shop shop, string? message = "Je vends sur Instagram")
    {
        var response = await affiliate.Client.Post("/api/me/partnerships", new { storeId = shop.Store.Id, message });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Read<AffiliatePartnershipDto>();
    }

    private async Task<HttpResponseMessage> Check(string code, Guid productId, HttpClient? client = null)
    {
        using var visitor = factory.CreateClient();
        return await (client ?? visitor).Post("/api/affiliate-codes/check", new { code, productId });
    }

    [Fact]
    public async Task Request_Accept_CodeValid_End_CodeRefused_AskAgain()
    {
        using var shop = await Arrange();
        using var affiliate = await NewAffiliate();

        var request = await Ask(affiliate, shop);
        Assert.Equal(PartnershipStatus.Requested, request.Status);
        Assert.Equal(shop.Store.Slug, request.Store.Slug);

        // Not a partner yet: the code is refused on the store's products.
        var early = await Check(affiliate.Code, shop.Product.Id);
        Assert.Equal("affiliateCode.notPartner", await early.ProblemCode());

        // The owner sees the waiting request with the affiliate's profile, and accepts it.
        var incoming = Assert.Single(await (await shop.Owner.GetAsync(new Uri("/api/me/store/partnerships", UriKind.Relative), Ct))
            .Read<IReadOnlyList<StorePartnershipDto>>());
        Assert.Equal(PartnershipStatus.Requested, incoming.Status);
        Assert.Equal("Je vends sur Instagram", incoming.Message);
        Assert.Equal(affiliate.Code, incoming.Affiliate.Code);
        Assert.True(incoming.Affiliate.CodeActive);

        var accepted = await (await shop.Owner.Post($"/api/me/store/partnerships/{request.Id}/accept")).Read<StorePartnershipDto>();
        Assert.Equal(PartnershipStatus.Accepted, accepted.Status);
        Assert.Equal(1, accepted.Affiliate.ActivePartnerships);

        var mine = Assert.Single(await (await affiliate.Client.GetAsync(new Uri("/api/me/partnerships", UriKind.Relative), Ct))
            .Read<IReadOnlyList<AffiliatePartnershipDto>>());
        Assert.Equal(PartnershipStatus.Accepted, mine.Status);

        // Any spelling of the code works; the client sees a short name and the product's %.
        var valid = await Check($"#{affiliate.Code.ToLowerInvariant().Replace("-", "", StringComparison.Ordinal)}", shop.Product.Id);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        var check = await valid.Read<AffiliateCodeCheckDto>();
        Assert.Equal(affiliate.Code, check.Code);
        Assert.Equal("Sana T.", check.AffiliateName);
        Assert.Equal(12, check.AffiliatePercent);
        Assert.Equal(shop.Store.Id, check.StoreId);

        // The owner stops working with the affiliate: future checks fail, the affiliate may ask again.
        var ended = await (await shop.Owner.Post($"/api/me/store/partnerships/{request.Id}/end", new { reason = "Fin de saison" }))
            .Read<StorePartnershipDto>();
        Assert.Equal(PartnershipStatus.Ended, ended.Status);
        Assert.Equal("Fin de saison", ended.EndReason);
        Assert.Equal("affiliateCode.notPartner", await (await Check(affiliate.Code, shop.Product.Id)).ProblemCode());

        await Ask(affiliate, shop);
    }

    [Fact]
    public async Task Refuse_WithReason_ShownToAffiliate_ThenAskAgain()
    {
        using var shop = await Arrange();
        using var affiliate = await NewAffiliate();
        var request = await Ask(affiliate, shop);

        var refused = await (await shop.Owner.Post($"/api/me/store/partnerships/{request.Id}/refuse", new { reason = "Profil incomplet" }))
            .Read<StorePartnershipDto>();
        Assert.Equal(PartnershipStatus.Refused, refused.Status);

        var mine = Assert.Single(await (await affiliate.Client.GetAsync(new Uri("/api/me/partnerships", UriKind.Relative), Ct))
            .Read<IReadOnlyList<AffiliatePartnershipDto>>());
        Assert.Equal("Profil incomplet", mine.DecisionReason);

        // A refusal can't be accepted afterwards, but the affiliate may send a new request.
        Assert.Equal(HttpStatusCode.Conflict, (await shop.Owner.Post($"/api/me/store/partnerships/{request.Id}/accept")).StatusCode);
        await Ask(affiliate, shop);
    }

    [Fact]
    public async Task Cancel_WithdrawsAWaitingRequest_OnlyMine()
    {
        using var shop = await Arrange();
        using var affiliate = await NewAffiliate();
        using var other = await NewAffiliate();
        var request = await Ask(affiliate, shop);

        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.Post($"/api/me/partnerships/{request.Id}/cancel")).StatusCode);

        var cancelled = await (await affiliate.Client.Post($"/api/me/partnerships/{request.Id}/cancel")).Read<AffiliatePartnershipDto>();
        Assert.Equal(PartnershipStatus.Cancelled, cancelled.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await shop.Owner.Post($"/api/me/store/partnerships/{request.Id}/accept")).StatusCode);
    }

    [Fact]
    public async Task Request_Rules()
    {
        using var shop = await Arrange();
        using var affiliate = await NewAffiliate();
        await Ask(affiliate, shop);

        // One open request or partnership per store.
        var twice = await affiliate.Client.Post("/api/me/partnerships", new { storeId = shop.Store.Id });
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal("partnership.alreadyOpen", await twice.ProblemCode());

        // Unknown store.
        var unknown = await affiliate.Client.Post("/api/me/partnerships", new { storeId = Guid.NewGuid() });
        Assert.Equal("partnership.storeUnavailable", await unknown.ProblemCode());

        // Only affiliates ask.
        using var buyer = factory.CreateClient();
        buyer.WithToken((await buyer.Register()).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.Post("/api/me/partnerships", new { storeId = shop.Store.Id })).StatusCode);

        // A disabled store accepts no new request.
        using var late = await NewAffiliate();
        (await shop.Admin.Post($"/api/admin/stores/{shop.Store.Id}/disable", new { reason = "Faux magasin" })).EnsureSuccessStatusCode();
        Assert.Equal("partnership.storeUnavailable",
            await (await late.Client.Post("/api/me/partnerships", new { storeId = shop.Store.Id })).ProblemCode());
    }

    [Fact]
    public async Task OwnStore_NoSelfAffiliation()
    {
        // An affiliate who also became a store owner can't partner with, or earn on, their own store.
        using var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);
        using var affiliate = await NewAffiliate();
        (await admin.Post($"/api/admin/users/{affiliate.User.Id}/roles/StoreOwner")).EnsureSuccessStatusCode();
        affiliate.Client.WithToken((await (await affiliate.Client.Post("/api/auth/refresh")).Read<AuthResponse>()).AccessToken);

        using var shop = await Arrange(affiliate.Client);
        var own = await affiliate.Client.Post("/api/me/partnerships", new { storeId = shop.Store.Id });
        Assert.Equal(HttpStatusCode.Conflict, own.StatusCode);
        Assert.Equal("partnership.ownStore", await own.ProblemCode());
        Assert.Equal("affiliateCode.ownStore", await (await Check(affiliate.Code, shop.Product.Id)).ProblemCode());
    }

    [Fact]
    public async Task OnlyTheOwnerOfThatStore_Decides()
    {
        using var shop = await Arrange();
        using var otherShop = await Arrange();
        using var affiliate = await NewAffiliate();
        var request = await Ask(affiliate, shop);

        Assert.Equal(HttpStatusCode.NotFound, (await otherShop.Owner.Post($"/api/me/store/partnerships/{request.Id}/accept")).StatusCode);
        Assert.Empty(await (await otherShop.Owner.GetAsync(new Uri("/api/me/store/partnerships", UriKind.Relative), Ct))
            .Read<IReadOnlyList<StorePartnershipDto>>());
        Assert.Equal(HttpStatusCode.Forbidden, (await affiliate.Client.Post($"/api/me/store/partnerships/{request.Id}/accept")).StatusCode);
    }

    [Fact]
    public async Task CodeCheck_RefusesEveryInvalidCase()
    {
        using var shop = await Arrange();
        using var affiliate = await NewAffiliate();
        var request = await Ask(affiliate, shop);
        (await shop.Owner.Post($"/api/me/store/partnerships/{request.Id}/accept")).EnsureSuccessStatusCode();

        Assert.Equal("affiliateCode.invalid", await (await Check("not a code!", shop.Product.Id)).ProblemCode());
        Assert.Equal("affiliateCode.unknown", await (await Check("ZZZZZ-999999", shop.Product.Id)).ProblemCode());
        Assert.Equal("affiliateCode.productUnavailable", await (await Check(affiliate.Code, Guid.NewGuid())).ProblemCode());
        // An affiliate never earns on their own purchase.
        Assert.Equal("affiliateCode.ownCode", await (await Check(affiliate.Code, shop.Product.Id, affiliate.Client)).ProblemCode());

        // The admin deactivates the code (fraud, audited): refused everywhere until re-enabled.
        var disabled = await shop.Admin.Post($"/api/admin/users/{affiliate.User.Id}/affiliate-code/disable", new { reason = "Faux clients" });
        Assert.NotNull((await disabled.Read<AdminUserDto>()).AffiliateCodeDisabledAt);
        Assert.Equal("affiliateCode.inactive", await (await Check(affiliate.Code, shop.Product.Id)).ProblemCode());

        var audit = await (await shop.Admin.GetAsync(new Uri($"/api/admin/audit?targetId={affiliate.User.Id}", UriKind.Relative), Ct))
            .Read<PagedResult<AuditEntryDto>>();
        Assert.Contains(audit.Items, e => e.Action == "user.affiliateCode.disable" && e.Reason == "Faux clients");

        (await shop.Admin.Post($"/api/admin/users/{affiliate.User.Id}/affiliate-code/enable")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await Check(affiliate.Code, shop.Product.Id)).StatusCode);
    }

    [Fact]
    public async Task Admin_ListsAndEndsWithReason_Audited()
    {
        using var shop = await Arrange();
        using var affiliate = await NewAffiliate();
        var request = await Ask(affiliate, shop);
        (await shop.Owner.Post($"/api/me/store/partnerships/{request.Id}/accept")).EnsureSuccessStatusCode();

        var list = await (await shop.Admin.GetAsync(new Uri($"/api/admin/partnerships?storeId={shop.Store.Id}&status=Accepted", UriKind.Relative), Ct))
            .Read<PagedResult<AdminPartnershipItem>>();
        var item = Assert.Single(list.Items);
        Assert.Equal(affiliate.Code, item.AffiliateCode);

        var byCode = await (await shop.Admin.GetAsync(new Uri($"/api/admin/partnerships?search=%23{affiliate.Code}", UriKind.Relative), Ct))
            .Read<PagedResult<AdminPartnershipItem>>();
        Assert.Contains(byCode.Items, i => i.Id == request.Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.Admin.Post($"/api/admin/partnerships/{request.Id}/end", new { reason = " " })).StatusCode);

        var ended = await (await shop.Admin.Post($"/api/admin/partnerships/{request.Id}/end", new { reason = "Fraude" })).Read<AdminPartnershipItem>();
        Assert.Equal(PartnershipStatus.Ended, ended.Status);

        var audit = await (await shop.Admin.GetAsync(new Uri($"/api/admin/audit?targetId={shop.Store.Id}", UriKind.Relative), Ct))
            .Read<PagedResult<AuditEntryDto>>();
        Assert.Contains(audit.Items, e => e.Action == "partnership.end" && e.Reason == "Fraude");

        using var buyer = factory.CreateClient();
        buyer.WithToken((await buyer.Register()).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync(new Uri("/api/admin/partnerships", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task Admin_SeesWhoOwnsTheStore_AndWhichStoreAUserOwns()
    {
        using var shop = await Arrange();
        using var affiliate = await NewAffiliate();
        var request = await Ask(affiliate, shop);

        var item = (await (await shop.Admin.GetAsync(new Uri($"/api/admin/partnerships?storeId={shop.Store.Id}", UriKind.Relative), Ct))
            .Read<PagedResult<AdminPartnershipItem>>()).Items.Single(i => i.Id == request.Id);
        Assert.NotEqual(Guid.Empty, item.StoreOwnerId);
        Assert.False(string.IsNullOrEmpty(item.StoreOwnerEmail));

        // The owner is found in the user list by the store's name, with the store on their row and their page.
        var users = await (await shop.Admin.GetAsync(new Uri($"/api/admin/users?search={Uri.EscapeDataString(shop.Store.Name)}", UriKind.Relative), Ct))
            .Read<PagedResult<AdminUserListItem>>();
        var owner = Assert.Single(users.Items);
        Assert.Equal(item.StoreOwnerId, owner.Id);
        Assert.Equal(shop.Store.Id, owner.Store!.Id);

        var detail = await (await shop.Admin.GetAsync(new Uri($"/api/admin/users/{owner.Id}", UriKind.Relative), Ct)).Read<AdminUserDto>();
        Assert.Equal(shop.Store.Name, detail.Store!.Name);
        Assert.Null((await (await shop.Admin.GetAsync(new Uri($"/api/admin/users/{affiliate.User.Id}", UriKind.Relative), Ct)).Read<AdminUserDto>()).Store);
    }
}
