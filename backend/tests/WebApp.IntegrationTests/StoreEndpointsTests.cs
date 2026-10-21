using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WebApp.Application.DTOs;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

public sealed class StoreEndpointsTests(ApiFactory factory)
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private async Task<HttpClient> AdminClient()
    {
        var client = factory.CreateClient();
        client.WithToken((await client.SignInAdmin()).AccessToken);
        return client;
    }

    private static async Task<AdminStoreCategoryDto> NewCategory(HttpClient admin)
    {
        var response = await admin.Post("/api/admin/store-categories", new { nameFr = $"Cat {Guid.NewGuid():N}"[..20] });
        return await response.Read<AdminStoreCategoryDto>();
    }

    private static object Store(Guid categoryId, string? name = null, Guid? logo = null, int percent = 10) => new
    {
        name = name ?? $"Boutique {Guid.NewGuid():N}"[..24],
        slug = (string?)null,
        categoryId,
        description = "Parfums orientaux",
        phone = "22 145 678",
        city = "sousse",
        address = "Avenue Habib Bourguiba",
        logoImageId = logo,
        bannerImageId = (Guid?)null,
        defaultAffiliatePercent = percent,
        deliveryFee = 7.5m,
        deliveryCities = new[] { "sousse", "monastir" },
        deliveryMinDays = 1,
        deliveryMaxDays = 3,
        facebookUrl = "https://facebook.com/boutique",
        instagramUrl = (string?)null,
        websiteUrl = (string?)null,
    };

    private static async Task<ImageDto> Upload(HttpClient client, string purpose)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "logo.png");
        form.Add(new StringContent(purpose), "purpose");
        return await (await client.PostAsync(new Uri("/api/images", UriKind.Relative), form, Ct)).Read<ImageDto>();
    }

    [Fact]
    public async Task StoreOwner_CreatesStore_LiveAtOnce_VisibleBySlug()
    {
        using var admin = await AdminClient();
        var category = await NewCategory(admin);
        using var owner = await factory.StoreOwnerClient();
        var logo = await Upload(owner, "StoreLogo");

        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(new Uri("/api/me/store", UriKind.Relative), Ct)).StatusCode);

        var response = await owner.Post("/api/me/store", Store(category.Id, "Parfumerie Yasmine " + Guid.NewGuid().ToString("N")[..6], logo.Id));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var store = await response.Read<MyStoreDto>();
        Assert.Equal(StoreStatus.Active, store.Status);
        Assert.StartsWith("parfumerie-yasmine-", store.Slug, StringComparison.Ordinal);
        Assert.Equal(logo.Url, store.LogoUrl);
        Assert.Equal(category.Id, store.Category.Id);
        Assert.Equal(30m, store.SubscriptionMonthlyPrice); // copied from the default setting

        using var visitor = factory.CreateClient();
        var shown = await (await visitor.GetAsync(new Uri($"/api/stores/{store.Slug}", UriKind.Relative), Ct)).Read<PublicStoreDto>();
        Assert.True(shown.IsOpen);
        Assert.Equal(["sousse", "monastir"], shown.DeliveryCities);
    }

    [Fact]
    public async Task OnlyStoreOwners_OneStoreEach()
    {
        using var admin = await AdminClient();
        var category = await NewCategory(admin);

        using var buyer = factory.CreateClient();
        buyer.WithToken((await buyer.Register()).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.Post("/api/me/store", Store(category.Id))).StatusCode);

        using var owner = await factory.StoreOwnerClient();
        await owner.Post("/api/me/store", Store(category.Id));
        var second = await owner.Post("/api/me/store", Store(category.Id));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("store.alreadyExists", await second.ProblemCode());
    }

    [Fact]
    public async Task Validation_AndUniqueSlug()
    {
        using var admin = await AdminClient();
        var category = await NewCategory(admin);
        using var first = await factory.StoreOwnerClient();
        var name = "Chez Amel " + Guid.NewGuid().ToString("N")[..6];
        await first.Post("/api/me/store", Store(category.Id, name));

        using var second = await factory.StoreOwnerClient();
        var sameName = await second.Post("/api/me/store", Store(category.Id, name));
        Assert.Equal("store.slugTaken", await sameName.ProblemCode());

        var tooMuch = await second.Post("/api/me/store", Store(category.Id, percent: 80));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Equal("store.percentOutOfRange", await tooMuch.ProblemCode());
    }

    [Fact]
    public async Task HiddenCategory_CantBeChosen_ButStaysOnItsStores()
    {
        using var admin = await AdminClient();
        var category = await NewCategory(admin);
        using var owner = await factory.StoreOwnerClient();
        var store = await (await owner.Post("/api/me/store", Store(category.Id))).Read<MyStoreDto>();

        await admin.Post($"/api/admin/store-categories/{category.Id}/deactivate");

        // The store keeps its hidden category when saving other changes…
        var update = await owner.PutAsJsonAsync("/api/me/store", Store(category.Id, store.Name), Ct);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        // …but a new store can't pick it.
        using var other = await factory.StoreOwnerClient();
        Assert.Equal("store.categoryUnavailable", await (await other.Post("/api/me/store", Store(category.Id))).ProblemCode());

        // And a category in use can't be deleted.
        var delete = await admin.DeleteAsync(new Uri($"/api/admin/store-categories/{category.Id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal("storeCategory.inUse", await delete.ProblemCode());
    }

    [Fact]
    public async Task SomeoneElsesImage_IsRefused_OwnLogoCantBeDeletedWhileUsed()
    {
        using var admin = await AdminClient();
        var category = await NewCategory(admin);
        using var thief = await factory.StoreOwnerClient();
        using var owner = await factory.StoreOwnerClient();
        var logo = await Upload(owner, "StoreLogo");

        Assert.Equal("image.notYours", await (await thief.Post("/api/me/store", Store(category.Id, logo: logo.Id))).ProblemCode());

        var banner = await Upload(owner, "StoreBanner");
        Assert.Equal("image.wrongPurpose", await (await owner.Post("/api/me/store", Store(category.Id, logo: banner.Id))).ProblemCode());

        await owner.Post("/api/me/store", Store(category.Id, logo: logo.Id));
        var delete = await owner.DeleteAsync(new Uri($"/api/images/{logo.Id}", UriKind.Relative), Ct);
        Assert.Equal("image.inUse", await delete.ProblemCode());
    }

    [Fact]
    public async Task ClosedStore_StillVisible_AsClosed_ThenReopens()
    {
        using var admin = await AdminClient();
        var category = await NewCategory(admin);
        using var owner = await factory.StoreOwnerClient();
        var store = await (await owner.Post("/api/me/store", Store(category.Id))).Read<MyStoreDto>();

        Assert.Equal(StoreStatus.Closed, (await (await owner.Post("/api/me/store/close")).Read<MyStoreDto>()).Status);
        using var visitor = factory.CreateClient();
        Assert.False((await (await visitor.GetAsync(new Uri($"/api/stores/{store.Slug}", UriKind.Relative), Ct)).Read<PublicStoreDto>()).IsOpen);

        Assert.Equal(StoreStatus.Active, (await (await owner.Post("/api/me/store/reopen")).Read<MyStoreDto>()).Status);
    }

    [Fact]
    public async Task UnknownSlug_Returns404_AndCitiesAreListed()
    {
        using var visitor = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri("/api/stores/does-not-exist", UriKind.Relative), Ct)).StatusCode);
        var cities = await (await visitor.GetAsync(new Uri("/api/cities", UriKind.Relative), Ct)).Read<List<string>>();
        Assert.Equal(24, cities.Count);
        Assert.Contains("ben-arous", cities);
    }
}
