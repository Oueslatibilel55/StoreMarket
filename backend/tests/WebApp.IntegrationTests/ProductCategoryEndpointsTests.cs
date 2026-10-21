using System.Net;
using System.Net.Http.Json;
using WebApp.Application.DTOs;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>Tests share one database: every category gets a unique name / slug and lists are searched, not counted.</summary>
public sealed class ProductCategoryEndpointsTests(ApiFactory factory)
{
    private async Task<HttpClient> AdminClient()
    {
        var client = factory.CreateClient();
        client.WithToken((await client.SignInAdmin()).AccessToken);
        return client;
    }

    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString("N")[..10]}";

    private static async Task<AdminProductCategoryDto> Create(HttpClient admin, string name, Guid? parentId = null)
    {
        var response = await admin.Post("/api/admin/product-categories", new { parentId, nameFr = Unique(name), nameAr = (string?)null, nameEn = (string?)null });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Read<AdminProductCategoryDto>();
    }

    private static object Attribute(string nameFr, AttributeType type, string? unit = null, string[]? options = null, string? key = null) => new
    {
        key,
        nameFr,
        nameAr = (string?)null,
        nameEn = (string?)null,
        type,
        unit,
        required = true,
        filterable = true,
        options = options?.Select(o => new { key = (string?)null, name = new { fr = o, ar = (string?)null, en = (string?)null } }).ToArray(),
    };

    private static async Task<AdminProductCategoryDto> AddAttribute(HttpClient admin, Guid categoryId, object attribute)
    {
        var response = await admin.Post($"/api/admin/product-categories/{categoryId}/attributes", attribute);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Read<AdminProductCategoryDto>();
    }

    [Fact]
    public async Task Schema_InheritsParentAttributesFirst_AndIsPublic()
    {
        using var admin = await AdminClient();
        var clothes = await Create(admin, "Vêtements");
        await AddAttribute(admin, clothes.Id, Attribute("Taille", AttributeType.SingleChoice, options: ["S", "M", "L"]));
        var dresses = await Create(admin, "Robes", clothes.Id);
        await AddAttribute(admin, dresses.Id, Attribute("Longueur", AttributeType.SingleChoice, options: ["Courte", "Midi", "Longue"]));

        using var visitor = factory.CreateClient();
        var schema = await (await visitor.GetAsync(new Uri($"/api/product-categories/{dresses.Slug}/schema", UriKind.Relative), Ct))
            .Read<CategorySchemaDto>();

        Assert.Equal(["taille", "longueur"], schema.Attributes.Select(a => a.Attribute.Key));
        Assert.Equal(clothes.Id, schema.Attributes[0].DefinedById);
        Assert.Equal([clothes.Id, dresses.Id], schema.Path.Select(p => p.Id));
        Assert.Equal(2, schema.Category.Depth);
        Assert.Equal(["s", "m", "l"], schema.Attributes[0].Attribute.Options.Select(o => o.Key));
    }

    [Fact]
    public async Task HiddenCategory_HidesItsBranch()
    {
        using var admin = await AdminClient();
        var parent = await Create(admin, "Maison");
        var child = await Create(admin, "Cuisine", parent.Id);

        await admin.Post($"/api/admin/product-categories/{parent.Id}/deactivate");

        using var visitor = factory.CreateClient();
        var tree = await (await visitor.GetAsync(new Uri("/api/product-categories", UriKind.Relative), Ct)).Read<List<ProductCategoryNode>>();
        Assert.DoesNotContain(tree, n => n.Id == parent.Id || n.Id == child.Id);
        Assert.Equal(HttpStatusCode.NotFound,
            (await visitor.GetAsync(new Uri($"/api/product-categories/{child.Slug}/schema", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task Tree_IsLimitedToThreeLevels_AndNeverLoops()
    {
        using var admin = await AdminClient();
        var a = await Create(admin, "Niveau 1");
        var b = await Create(admin, "Niveau 2", a.Id);
        var c = await Create(admin, "Niveau 3", b.Id);

        var tooDeep = await admin.Post("/api/admin/product-categories", new { parentId = c.Id, nameFr = Unique("Niveau 4") });
        Assert.Equal("productCategory.tooDeep", await tooDeep.ProblemCode());

        var cycle = await admin.PutAsJsonAsync($"/api/admin/product-categories/{a.Id}", new { parentId = c.Id, nameFr = a.Name.Fr, slug = a.Slug }, Ct);
        Assert.Equal("productCategory.cycle", await cycle.ProblemCode());
    }

    [Fact]
    public async Task AttributeKey_IsUniqueAlongTheBranch()
    {
        using var admin = await AdminClient();
        var parent = await Create(admin, "Électronique");
        await AddAttribute(admin, parent.Id, Attribute("Marque", AttributeType.Text));
        var child = await Create(admin, "Smartphones", parent.Id);

        var clash = await admin.Post($"/api/admin/product-categories/{child.Id}/attributes", Attribute("Marque", AttributeType.Text));

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Equal("productCategory.keyUsedInBranch", await clash.ProblemCode());
    }

    [Fact]
    public async Task AttributeValidation_Returns400()
    {
        using var admin = await AdminClient();
        var category = await Create(admin, "Parfums");

        var noOptions = await admin.Post($"/api/admin/product-categories/{category.Id}/attributes", Attribute("Genre", AttributeType.SingleChoice, options: ["Homme"]));
        Assert.Equal("productCategory.optionsRequired", await noOptions.ProblemCode());

        var unitOnText = await admin.Post($"/api/admin/product-categories/{category.Id}/attributes", Attribute("Notes", AttributeType.Text, unit: "ml"));
        Assert.Equal("productCategory.unitInvalid", await unitOnText.ProblemCode());
    }

    [Fact]
    public async Task EditMoveDeleteAttribute_AreAudited()
    {
        using var admin = await AdminClient();
        var category = await Create(admin, "Pâtisserie");
        var withWeight = await AddAttribute(admin, category.Id, Attribute("Poids", AttributeType.Number, unit: "g"));
        var withBoth = await AddAttribute(admin, category.Id, Attribute("Sans gluten", AttributeType.Boolean));
        var weight = withWeight.Attributes.Single();
        var gluten = withBoth.Attributes.Single(a => a.Key == "sans-gluten");

        var edited = await (await admin.PutAsJsonAsync($"/api/admin/product-categories/{category.Id}/attributes/{weight.Id}",
            Attribute("Poids net", AttributeType.Text, unit: "kg"), Ct)).Read<AdminProductCategoryDto>();
        var editedWeight = edited.Attributes.Single(a => a.Id == weight.Id);
        Assert.Equal(AttributeType.Number, editedWeight.Type); // the type never changes
        Assert.Equal("poids", editedWeight.Key);
        Assert.Equal("kg", editedWeight.Unit);

        var moved = await (await admin.Post($"/api/admin/product-categories/{category.Id}/attributes/{gluten.Id}/move", new { direction = "up" }))
            .Read<AdminProductCategoryDto>();
        Assert.Equal(["sans-gluten", "poids"], moved.Attributes.Select(a => a.Key));

        var deleted = await (await admin.DeleteAsync(new Uri($"/api/admin/product-categories/{category.Id}/attributes/{gluten.Id}", UriKind.Relative), Ct))
            .Read<AdminProductCategoryDto>();
        Assert.Equal(["poids"], deleted.Attributes.Select(a => a.Key));

        var audit = await (await admin.GetAsync(new Uri($"/api/admin/audit?targetId={category.Id}", UriKind.Relative), Ct)).Read<PagedResult<AuditEntryDto>>();
        Assert.Equal(
            ["productCategory.attribute.delete", "productCategory.attribute.move", "productCategory.attribute.update",
             "productCategory.attribute.create", "productCategory.attribute.create", "productCategory.create"],
            audit.Items.Select(e => e.Action));
    }

    [Fact]
    public async Task Delete_OnlyWithoutChildren()
    {
        using var admin = await AdminClient();
        var parent = await Create(admin, "Sport");
        var child = await Create(admin, "Football", parent.Id);

        var refused = await admin.DeleteAsync(new Uri($"/api/admin/product-categories/{parent.Id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("productCategory.hasChildren", await refused.ProblemCode());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(new Uri($"/api/admin/product-categories/{child.Id}", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(new Uri($"/api/admin/product-categories/{parent.Id}", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task MoveUnderAnotherParent_TakesTheBranch()
    {
        using var admin = await AdminClient();
        var shoes = await Create(admin, "Chaussures");
        var clothes = await Create(admin, "Habits");
        var sneakers = await Create(admin, "Baskets", clothes.Id);

        var moved = await (await admin.PutAsJsonAsync($"/api/admin/product-categories/{sneakers.Id}",
            new { parentId = shoes.Id, nameFr = sneakers.Name.Fr, slug = sneakers.Slug }, Ct)).Read<AdminProductCategoryDto>();

        Assert.Equal(shoes.Id, moved.ParentId);
    }

    [Fact]
    public async Task AdminEndpoints_AreAdminOnly()
    {
        using var buyer = factory.CreateClient();
        buyer.WithToken((await buyer.Register()).AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync(new Uri("/api/admin/product-categories", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.Post("/api/admin/product-categories", new { nameFr = "Hack" })).StatusCode);
    }
}
