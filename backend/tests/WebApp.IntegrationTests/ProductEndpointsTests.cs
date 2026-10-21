using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WebApp.Application.DTOs;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

public sealed class ProductEndpointsTests(ApiFactory factory)
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private static readonly string[] Sizes = ["S", "M", "L"];
    private static readonly string[] Tunis = ["tunis"];

    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString("N")[..10]}";

    /// <summary>A "Vêtements › Robes" branch: Taille (required, S/M/L) on the parent, Longueur (number) on the child.</summary>
    private sealed record Setup(HttpClient Admin, HttpClient Owner, AdminProductCategoryDto Clothes, AdminProductCategoryDto Dresses) : IDisposable
    {
        public void Dispose()
        {
            Admin.Dispose();
            Owner.Dispose();
        }
    }

    private async Task<Setup> Arrange()
    {
        var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);

        var clothes = await (await admin.Post("/api/admin/product-categories", new { nameFr = Unique("Vêtements") })).Read<AdminProductCategoryDto>();
        clothes = await (await admin.Post($"/api/admin/product-categories/{clothes.Id}/attributes", new
        {
            nameFr = "Taille", type = "SingleChoice", required = true, filterable = true,
            options = Sizes.Select(o => new { name = new { fr = o } }),
        })).Read<AdminProductCategoryDto>();
        var dresses = await (await admin.Post("/api/admin/product-categories", new { parentId = clothes.Id, nameFr = Unique("Robes") })).Read<AdminProductCategoryDto>();
        dresses = await (await admin.Post($"/api/admin/product-categories/{dresses.Id}/attributes",
            new { nameFr = "Longueur", type = "Number", unit = "cm" })).Read<AdminProductCategoryDto>();

        var storeCategory = await (await admin.Post("/api/admin/store-categories", new { nameFr = Unique("Mode") })).Read<AdminStoreCategoryDto>();
        var owner = await factory.StoreOwnerClient();
        (await owner.Post("/api/me/store", new
        {
            name = Unique("Boutique"), categoryId = storeCategory.Id, phone = "22 145 678", city = "tunis",
            defaultAffiliatePercent = 10, deliveryFee = 7m, deliveryCities = Tunis, deliveryMinDays = 1, deliveryMaxDays = 2,
        })).EnsureSuccessStatusCode();
        return new Setup(admin, owner, clothes, dresses);
    }

    private static object Product(Guid categoryId, string name = "Robe fleurie", string status = "Published", string? size = "m",
        Guid[]? images = null, int? percent = null, object[]? variants = null) => new
    {
        name,
        categoryId,
        description = "Robe d'été en coton",
        price = 89.9m,
        compareAtPrice = 120m,
        sku = "RF-01",
        stock = 12,
        affiliatePercent = percent,
        status,
        imageIds = images ?? [],
        values = size is null
            ? new object[] { new { key = "longueur", number = 95m } }
            : [new { key = "taille", options = new[] { size } }, new { key = "longueur", number = 95m }],
        variantAxes = variants is null ? Array.Empty<string>() : ["taille"],
        variants = variants ?? [],
    };

    private static async Task<ImageDto> Upload(HttpClient client)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "photo.png");
        form.Add(new StringContent("ProductPhoto"), "purpose");
        return await (await client.PostAsync(new Uri("/api/images", UriKind.Relative), form, Ct)).Read<ImageDto>();
    }

    [Fact]
    public async Task Owner_AddsProduct_PublishedOneIsPublic_WithLabelsAndInheritedFields()
    {
        using var s = await Arrange();
        var photo = await Upload(s.Owner);

        var response = await s.Owner.Post("/api/me/store/products", Product(s.Dresses.Id, images: [photo.Id]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Read<ProductDto>();
        Assert.Equal(10, product.EffectiveAffiliatePercent); // the store's default
        Assert.Equal(photo.Url, Assert.Single(product.Images).Url);

        using var visitor = factory.CreateClient();
        var shown = await (await visitor.GetAsync(new Uri($"/api/products/{product.Slug}", UriKind.Relative), Ct)).Read<PublicProductDto>();
        Assert.Equal(["taille", "longueur"], shown.Attributes.Select(a => a.Key)); // parent's field first
        Assert.Equal("M", Assert.Single(shown.Attributes[0].Options).Name.Fr);
        Assert.Equal(95m, shown.Attributes[1].Number);
        Assert.Equal([s.Clothes.Id, s.Dresses.Id], shown.CategoryPath.Select(c => c.Id));
        Assert.True(shown.InStock);
    }

    [Fact]
    public async Task Draft_IsPrivate_AndMayBeIncomplete()
    {
        using var s = await Arrange();

        var incomplete = await s.Owner.Post("/api/me/store/products", Product(s.Dresses.Id, size: null));
        Assert.Equal("product.requiredAttributeMissing", await incomplete.ProblemCode());

        var draft = await (await s.Owner.Post("/api/me/store/products", Product(s.Dresses.Id, status: "Draft", size: null))).Read<ProductDto>();
        using var visitor = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri($"/api/products/{draft.Slug}", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task SameName_GetsAUniqueSlug()
    {
        using var s = await Arrange();
        var name = Unique("Robe");

        var first = await (await s.Owner.Post("/api/me/store/products", Product(s.Dresses.Id, name))).Read<ProductDto>();
        var second = await (await s.Owner.Post("/api/me/store/products", Product(s.Dresses.Id, name))).Read<ProductDto>();

        Assert.Equal(first.Slug + "-2", second.Slug);
    }

    [Fact]
    public async Task Variants_HaveTheirOwnStock_AndKeepTheirIds()
    {
        using var s = await Arrange();
        var product = await (await s.Owner.Post("/api/me/store/products", Product(s.Dresses.Id, size: null, variants:
        [
            new { options = new Dictionary<string, string> { ["taille"] = "s" }, stock = 3 },
            new { options = new Dictionary<string, string> { ["taille"] = "m" }, stock = 0, price = 95m },
        ]))).Read<ProductDto>();
        Assert.Equal(3, product.TotalStock);
        var small = product.Variants.Single(v => v.Options["taille"] == "s");

        var updated = await (await s.Owner.PutAsJsonAsync($"/api/me/store/products/{product.Id}", Product(s.Dresses.Id, product.Name, size: null, variants:
        [
            new { id = small.Id, options = new Dictionary<string, string> { ["taille"] = "s" }, stock = 9 },
        ]), Ct)).Read<ProductDto>();
        Assert.Equal(small.Id, Assert.Single(updated.Variants).Id);
        Assert.Equal(9, updated.TotalStock);
        Assert.Equal(product.Slug, updated.Slug);

        using var visitor = factory.CreateClient();
        var shown = await (await visitor.GetAsync(new Uri($"/api/products/{product.Slug}", UriKind.Relative), Ct)).Read<PublicProductDto>();
        Assert.Equal("taille", Assert.Single(shown.VariantAxes).Key);
        Assert.True(Assert.Single(shown.Variants).InStock);
    }

    [Fact]
    public async Task Rules_PercentLimits_OwnPhotosOnly_OtherStoresProductsHidden()
    {
        using var s = await Arrange();
        Assert.Equal("product.percentOutOfRange", await (await s.Owner.Post("/api/me/store/products", Product(s.Dresses.Id, percent: 80))).ProblemCode());

        using var other = await factory.StoreOwnerClient();
        var stolen = await Upload(other);
        Assert.Equal("image.notYours", await (await s.Owner.Post("/api/me/store/products", Product(s.Dresses.Id, images: [stolen.Id]))).ProblemCode());

        var mine = await (await s.Owner.Post("/api/me/store/products", Product(s.Dresses.Id))).Read<ProductDto>();
        // `other` has no store: their product endpoints say so; nobody reaches someone else's product.
        Assert.Equal("store.notFound", await (await other.GetAsync(new Uri($"/api/me/store/products/{mine.Id}", UriKind.Relative), Ct)).ProblemCode());
    }

    [Fact]
    public async Task List_SearchesAndFilters()
    {
        using var s = await Arrange();
        var name = Unique("Caftan");
        await s.Owner.Post("/api/me/store/products", Product(s.Dresses.Id, name));
        await s.Owner.Post("/api/me/store/products", Product(s.Dresses.Id, Unique("Jupe"), status: "Draft"));

        var found = await (await s.Owner.GetAsync(new Uri($"/api/me/store/products?search={Uri.EscapeDataString(name)}", UriKind.Relative), Ct))
            .Read<PagedResult<ProductListItem>>();
        Assert.Equal(name, Assert.Single(found.Items).Name);

        var drafts = await (await s.Owner.GetAsync(new Uri("/api/me/store/products?status=Draft", UriKind.Relative), Ct)).Read<PagedResult<ProductListItem>>();
        Assert.All(drafts.Items, i => Assert.Equal(ProductStatus.Draft, i.Status));
    }

    [Fact]
    public async Task Delete_IsSoft_ProductLeavesStoreAndShop()
    {
        using var s = await Arrange();
        var product = await (await s.Owner.Post("/api/me/store/products", Product(s.Dresses.Id))).Read<ProductDto>();

        Assert.Equal(HttpStatusCode.NoContent, (await s.Owner.DeleteAsync(new Uri($"/api/me/store/products/{product.Id}", UriKind.Relative), Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await s.Owner.GetAsync(new Uri($"/api/me/store/products/{product.Id}", UriKind.Relative), Ct)).StatusCode);
        using var visitor = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri($"/api/products/{product.Slug}", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task UsedCategoryFieldAndChoice_CantBeDeleted()
    {
        using var s = await Arrange();
        await s.Owner.Post("/api/me/store/products", Product(s.Dresses.Id, size: "m"));
        var size = s.Clothes.Attributes.Single();

        var category = await s.Admin.DeleteAsync(new Uri($"/api/admin/product-categories/{s.Dresses.Id}", UriKind.Relative), Ct);
        Assert.Equal("productCategory.hasProducts", await category.ProblemCode());

        var field = await s.Admin.DeleteAsync(new Uri($"/api/admin/product-categories/{s.Clothes.Id}/attributes/{size.Id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, field.StatusCode);
        Assert.Equal("productCategory.attributeInUse", await field.ProblemCode());

        // Removing "M" (used) is refused; removing "L" (unused) and renaming "S" is fine.
        object Edit(params (string? Key, string Fr)[] options) => new
        {
            nameFr = "Taille", type = "SingleChoice", required = true, filterable = true,
            options = options.Select(o => new { key = o.Key, name = new { fr = o.Fr } }),
        };
        var withoutM = await s.Admin.PutAsJsonAsync($"/api/admin/product-categories/{s.Clothes.Id}/attributes/{size.Id}",
            Edit(("s", "S"), ("l", "L")), Ct);
        Assert.Equal("productCategory.optionInUse", await withoutM.ProblemCode());

        var ok = await s.Admin.PutAsJsonAsync($"/api/admin/product-categories/{s.Clothes.Id}/attributes/{size.Id}",
            Edit(("s", "Small"), ("m", "M")), Ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task OwnerEndpoints_NeedAStoreOwner()
    {
        using var buyer = factory.CreateClient();
        buyer.WithToken((await buyer.Register()).AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync(new Uri("/api/me/store/products", UriKind.Relative), Ct)).StatusCode);
    }
}
