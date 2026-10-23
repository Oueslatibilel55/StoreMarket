using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Application.DTOs;
using WebApp.Infrastructure.Persistence;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>
/// Shop search and filters (018). Tests share one database: each one searches for its own random word, or filters
/// on its own categories, so other tests' data never shows up.
/// </summary>
public sealed class SearchEndpointsTests(ApiFactory factory)
{
    private static readonly string[] Tunis = ["tunis"];
    private static readonly string[] BlackAndRed = ["noir", "rouge"];
    private static readonly string[] Cotton = ["coton"];

    /// <summary>A word no other test uses (letters only, so it is one search word).</summary>
    private static string Word() => "zq" + new string([.. Guid.NewGuid().ToString("N")[..10].Select(c => (char)('a' + c % 26))]);

    private static object P(string name, Guid category, decimal price, string? description = null, object[]? values = null,
        string[]? axes = null, object[]? variants = null, string? slug = null) => new
    {
        name, slug, categoryId = category, price, description, stock = 5, status = "Published",
        values = values ?? [], variantAxes = axes ?? [], variants = variants ?? [],
    };

    private static async Task Create(HttpClient owner, object product)
    {
        var response = await owner.Post("/api/me/store/products", product);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {await response.ProblemCode()}");
    }

    private async Task<(HttpClient Owner, MyStoreDto Store)> NewStore(string name, Guid? storeCategoryId = null)
    {
        if (storeCategoryId is null)
        {
            using var admin = factory.CreateClient();
            admin.WithToken((await admin.SignInAdmin()).AccessToken);
            storeCategoryId = (await (await admin.Post("/api/admin/store-categories", new { nameFr = $"Cat {Word()}" })).Read<AdminStoreCategoryDto>()).Id;
        }
        var owner = await factory.StoreOwnerClient();
        var store = await (await owner.Post("/api/me/store", new
        {
            name, categoryId = storeCategoryId, phone = "22 145 678", city = "sfax",
            defaultAffiliatePercent = 10, deliveryFee = 7m, deliveryCities = Tunis, deliveryMinDays = 1, deliveryMaxDays = 2,
        })).Read<MyStoreDto>();
        return (owner, store);
    }

    private async Task<AdminProductCategoryDto> NewCategory(object[]? attributes = null)
    {
        using var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);
        var category = await (await admin.Post("/api/admin/product-categories", new { nameFr = $"Rayon {Word()}" })).Read<AdminProductCategoryDto>();
        foreach (var attribute in attributes ?? [])
            (await admin.Post($"/api/admin/product-categories/{category.Id}/attributes", attribute)).EnsureSuccessStatusCode();
        return category;
    }

    private async Task<PagedResult<ProductCardDto>> Products(string query)
    {
        using var visitor = factory.CreateClient();
        return await (await visitor.GetAsync(new Uri($"/api/products?{query}", UriKind.Relative), Ct)).Read<PagedResult<ProductCardDto>>();
    }

    private async Task<PagedResult<StoreCardDto>> Stores(string query)
    {
        using var visitor = factory.CreateClient();
        return await (await visitor.GetAsync(new Uri($"/api/stores?{query}", UriKind.Relative), Ct)).Read<PagedResult<StoreCardDto>>();
    }

    /// <summary>Test setup only: in the app, ratings are written by the review repository (017).</summary>
    private async Task Rate(string productName, decimal score)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Products.Where(p => p.Name == productName).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.AverageRating, score).SetProperty(p => p.RatingCount, 3).SetProperty(p => p.RankingScore, score), Ct);
    }

    [Fact]
    public async Task Search_IgnoresAccentsAndCase_PrefixesMatch_DescriptionToo()
    {
        var word = Word();
        var category = (await NewCategory()).Id;
        var (owner, _) = await NewStore($"Boutique {Word()}");
        using var _ = owner;
        await Create(owner, P($"Crème Hydratante {word}", category, 20m));
        await Create(owner, P("Savon doux", category, 8m, description: $"Au lait d'ânesse, {word}."));
        await Create(owner, P("عطر رجالي فاخر", category, 90m, description: word, slug: $"parfum-{word}"));

        Assert.Equal([$"Crème Hydratante {word}"], (await Products($"search=CREME {word}")).Items.Select(p => p.Name));
        Assert.Equal([$"Crème Hydratante {word}"], (await Products($"search=hydrat {word[..6]}")).Items.Select(p => p.Name));
        Assert.Equal(["Savon doux"], (await Products($"search=anesse {word}")).Items.Select(p => p.Name));
        Assert.Equal(["عطر رجالي فاخر"], (await Products($"search={Uri.EscapeDataString($"عطر {word}")}")).Items.Select(p => p.Name));
        Assert.Equal(3, (await Products($"search={word}")).Total);
        Assert.Empty((await Products($"search=shampoing {word}")).Items); // every word must match
        Assert.Equal(3, (await Products($"search=!!! {word} &|:*")).Total); // operators typed by the visitor are just ignored
    }

    [Fact]
    public async Task Search_FindsTheStore_AndTheProductsOfAMatchingStore()
    {
        var word = Word();
        var category = (await NewCategory()).Id;
        var (owner, store) = await NewStore($"Atelier {word}");
        using var _ = owner;
        await Create(owner, P("Panier tressé", category, 35m));

        Assert.Equal([store.Slug], (await Stores($"search=atel {word}")).Items.Select(s => s.Slug));
        Assert.Equal(["Panier tressé"], (await Products($"search={word}")).Items.Select(p => p.Name));
    }

    [Fact]
    public async Task Search_SortsBestRatedFirst_ThenRelevance_UnratedLast()
    {
        var word = Word();
        var category = (await NewCategory()).Id;
        var (owner, _) = await NewStore($"Boutique {Word()}");
        using var _ = owner;
        foreach (var (name, description) in new[]
        {
            ("Good", (string?)null), ("Best", null), ("Unrated, in the name", null), ("Unrated, in the text", word),
        })
        {
            var full = description is null ? $"{name} {word}" : name;
            await Create(owner, P(full, category, 10m, description));
        }
        await Rate($"Good {word}", 4.2m);
        await Rate($"Best {word}", 4.8m);

        var found = await Products($"search={word}");
        Assert.Equal([$"Best {word}", $"Good {word}", $"Unrated, in the name {word}", "Unrated, in the text"], found.Items.Select(p => p.Name));

        var newestFirst = await Products($"search={word}&sort=Newest");
        Assert.Equal("Unrated, in the text", newestFirst.Items[0].Name); // an explicit sort wins
    }

    [Fact]
    public async Task Filters_FromPrice_MinRating_StoreCategory()
    {
        var word = Word();
        var created = await NewCategory(
        [
            new { nameFr = "Taille", type = "SingleChoice", options = new[] { new { name = new { fr = "Petit" } }, new { name = new { fr = "Grand" } } } },
        ]);
        var category = created.Id;
        var (owner, store) = await NewStore($"Boutique {Word()}");
        using var _ = owner;
        await Create(owner, P($"Bol {word}", category, 15m));
        await Create(owner, P($"Plat {word}", category, 60m, axes: ["taille"], variants:
        [
            new { options = new Dictionary<string, string> { ["taille"] = "petit" }, stock = 2, price = 45m },
            new { options = new Dictionary<string, string> { ["taille"] = "grand" }, stock = 1 },
        ]));
        await Rate($"Plat {word}", 4.5m);

        Assert.Equal([$"Plat {word}"], (await Products($"search={word}&minPrice=40&maxPrice=50")).Items.Select(p => p.Name)); // from 45
        Assert.Equal([$"Bol {word}", $"Plat {word}"], (await Products($"search={word}&sort=PriceAsc")).Items.Select(p => p.Name));
        Assert.Equal([$"Plat {word}"], (await Products($"search={word}&minRating=4")).Items.Select(p => p.Name));
        Assert.Empty((await Products($"search={word}&minRating=5")).Items);
        Assert.Equal(2, (await Products($"search={word}&storeCategory={store.Category.Slug}")).Total);

        using var visitor = factory.CreateClient();
        var unknown = await visitor.GetAsync(new Uri("/api/products?storeCategory=nope", UriKind.Relative), Ct);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Filters_FilterableFields_OwnValuesAndVariants()
    {
        var created = await NewCategory(
        [
            new { nameFr = "Couleur", type = "MultiChoice", filterable = true,
                options = new[] { new { name = new { fr = "Noir" } }, new { name = new { fr = "Rouge" } }, new { name = new { fr = "Bleu" } } } },
            new { nameFr = "Bio", type = "Boolean", filterable = true },
            new { nameFr = "Matière", type = "SingleChoice", filterable = false, options = new[] { new { name = new { fr = "Coton" } }, new { name = new { fr = "Lin" } } } },
        ]);
        var category = created.Id;
        var slug = created.Slug;
        var (owner, _) = await NewStore($"Boutique {Word()}");
        using var _ = owner;
        await Create(owner, P("Noir et rouge", category, 10m, values:
        [
            new { key = "couleur", options = BlackAndRed }, new { key = "bio", boolean = true },
        ]));
        await Create(owner, P("Bleu par variante", category, 10m, axes: ["couleur"], variants:
        [
            new { options = new Dictionary<string, string> { ["couleur"] = "bleu" }, stock = 1 },
        ]));
        await Create(owner, P("Sans couleur", category, 10m, values:
        [
            new { key = "bio", boolean = false }, new { key = "matiere", options = Cotton },
        ]));

        string[] Names(PagedResult<ProductCardDto> r) => [.. r.Items.Select(p => p.Name).Order(StringComparer.Ordinal)];

        Assert.Equal(["Noir et rouge"], Names(await Products($"category={slug}&attr=couleur:rouge")));
        Assert.Equal(["Bleu par variante", "Noir et rouge"], Names(await Products($"category={slug}&attr=couleur:bleu,noir")));
        Assert.Equal(["Noir et rouge"], Names(await Products($"category={slug}&attr=couleur:noir,bleu&attr=bio:true")));
        Assert.Equal(["Sans couleur"], Names(await Products($"category={slug}&attr=bio:false")));
        // Not filterable, unknown field or choice, malformed: ignored.
        Assert.Equal(3, (await Products($"category={slug}&attr=matiere:coton&attr=nope:x&attr=couleur:vert&attr=junk")).Total);
    }

    [Fact]
    public async Task StoreSearch_OpenStoresFirst_ThenBestRated()
    {
        var word = Word();
        var (closedOwner, closed) = await NewStore($"Fermée {word}");
        var (ratedOwner, rated) = await NewStore($"Zeta {word}");
        var (plainOwner, plain) = await NewStore($"Alpha {word}");
        using var _ = closedOwner;
        using var __ = ratedOwner;
        using var ___ = plainOwner;
        (await closedOwner.Post("/api/me/store/close")).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Stores.Where(s => s.Id == rated.Id || s.Id == closed.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AverageRating, 4.5m).SetProperty(x => x.RatingCount, 2).SetProperty(x => x.RankingScore, 4.5m), Ct);
        }

        Assert.Equal([rated.Slug, plain.Slug, closed.Slug], (await Stores($"search={word}")).Items.Select(s => s.Slug));
        Assert.Equal([plain.Slug, rated.Slug, closed.Slug], (await Stores($"search={word}&sort=Name")).Items.Select(s => s.Slug)); // open first, then name
    }
}
