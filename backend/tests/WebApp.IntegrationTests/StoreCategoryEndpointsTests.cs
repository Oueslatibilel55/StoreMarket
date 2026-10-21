using System.Net;
using System.Net.Http.Json;
using WebApp.Application.DTOs;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>Tests share one database: every category gets a unique name, and lists are searched, not counted.</summary>
public sealed class StoreCategoryEndpointsTests(ApiFactory factory)
{
    private async Task<HttpClient> AdminClient()
    {
        var client = factory.CreateClient();
        client.WithToken((await client.SignInAdmin()).AccessToken);
        return client;
    }

    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString("N")[..12]}";

    private static async Task<AdminStoreCategoryDto> Create(HttpClient admin, string nameFr, string? slug = null)
    {
        var response = await admin.Post("/api/admin/store-categories", new { nameFr, nameAr = "عطور", nameEn = "Perfumes", slug, icon = "🌸" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Read<AdminStoreCategoryDto>();
    }

    private static async Task<List<StoreCategoryDto>> Public(HttpClient client) =>
        await (await client.GetAsync(new Uri("/api/store-categories", UriKind.Relative), Ct)).Read<List<StoreCategoryDto>>();

    [Fact]
    public async Task Create_ShowsInPublicList_WithNamesAndSlug_AndIsAudited()
    {
        using var admin = await AdminClient();
        var name = Unique("Parfums");

        var created = await Create(admin, name);

        Assert.True(created.IsActive);
        Assert.StartsWith("parfums-", created.Slug, StringComparison.Ordinal);
        using var visitor = factory.CreateClient();
        var shown = Assert.Single(await Public(visitor), c => c.Id == created.Id);
        Assert.Equal(new LocalizedName(name, "عطور", "Perfumes"), shown.Name);

        var audit = await (await admin.GetAsync(new Uri($"/api/admin/audit?targetId={created.Id}", UriKind.Relative), Ct))
            .Read<PagedResult<AuditEntryDto>>();
        Assert.Equal("storeCategory.create", Assert.Single(audit.Items).Action);
    }

    [Fact]
    public async Task Deactivated_IsHiddenFromShop_ButStaysInAdminList()
    {
        using var admin = await AdminClient();
        var created = await Create(admin, Unique("Vêtements"));

        await admin.Post($"/api/admin/store-categories/{created.Id}/deactivate");

        using var visitor = factory.CreateClient();
        Assert.DoesNotContain(await Public(visitor), c => c.Id == created.Id);
        var all = await (await admin.GetAsync(new Uri("/api/admin/store-categories", UriKind.Relative), Ct)).Read<List<AdminStoreCategoryDto>>();
        Assert.False(Assert.Single(all, c => c.Id == created.Id).IsActive);

        await admin.Post($"/api/admin/store-categories/{created.Id}/activate");
        Assert.Contains(await Public(visitor), c => c.Id == created.Id);
    }

    [Fact]
    public async Task DuplicateSlugOrName_Returns409()
    {
        using var admin = await AdminClient();
        var name = Unique("Cosmétiques");
        var first = await Create(admin, name);

        var sameSlug = await admin.Post("/api/admin/store-categories", new { nameFr = Unique("Autre"), slug = first.Slug });
        Assert.Equal(HttpStatusCode.Conflict, sameSlug.StatusCode);
        Assert.Equal("storeCategory.slugTaken", await sameSlug.ProblemCode());

        var sameName = await admin.Post("/api/admin/store-categories", new { nameFr = name.ToUpperInvariant(), slug = $"x-{Guid.NewGuid():N}" });
        Assert.Equal("storeCategory.nameTaken", await sameName.ProblemCode());
    }

    [Fact]
    public async Task Update_RenamesAndKeepsItsOwnSlug()
    {
        using var admin = await AdminClient();
        var created = await Create(admin, Unique("Bijoux"));

        var response = await admin.PutAsJsonAsync($"/api/admin/store-categories/{created.Id}",
            new { nameFr = created.Name.Fr + " fins", nameAr = (string?)null, nameEn = "Jewellery", slug = created.Slug, icon = "💍" }, Ct);

        var updated = await response.Read<AdminStoreCategoryDto>();
        Assert.Equal(created.Slug, updated.Slug);
        Assert.Null(updated.Name.Ar);
        Assert.Equal("💍", updated.Icon);
    }

    [Fact]
    public async Task InvalidInput_Returns400()
    {
        using var admin = await AdminClient();

        var response = await admin.Post("/api/admin/store-categories", new { nameFr = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("storeCategory.nameRequired", await response.ProblemCode());
    }

    [Fact]
    public async Task MoveUp_PutsCategoryBeforeItsNeighbour_AndIsAudited()
    {
        using var admin = await AdminClient();
        await Create(admin, Unique("Maison"));
        var second = await Create(admin, Unique("Jardin"));

        var moved = await (await admin.Post($"/api/admin/store-categories/{second.Id}/move", new { direction = "up" })).Read<AdminStoreCategoryDto>();

        Assert.True(moved.SortOrder < second.SortOrder);
        var audit = await (await admin.GetAsync(new Uri($"/api/admin/audit?targetId={second.Id}&action=storeCategory.move", UriKind.Relative), Ct))
            .Read<PagedResult<AuditEntryDto>>();
        Assert.Equal("up", Assert.Single(audit.Items).Reason);

        var bad = await admin.Post($"/api/admin/store-categories/{second.Id}/move", new { direction = "left" });
        Assert.Equal("storeCategory.directionInvalid", await bad.ProblemCode());
    }

    [Fact]
    public async Task Delete_RemovesIt_AuditKeepsTheTrace()
    {
        using var admin = await AdminClient();
        var created = await Create(admin, Unique("Jouets"));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(new Uri($"/api/admin/store-categories/{created.Id}", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync(new Uri($"/api/admin/store-categories/{created.Id}", UriKind.Relative), Ct)).StatusCode);

        var audit = await (await admin.GetAsync(new Uri($"/api/admin/audit?targetId={created.Id}&action=storeCategory.delete", UriKind.Relative), Ct))
            .Read<PagedResult<AuditEntryDto>>();
        Assert.Contains(created.Slug, Assert.Single(audit.Items).Before!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminEndpoints_AreAdminOnly()
    {
        using var buyer = factory.CreateClient();
        buyer.WithToken((await buyer.Register()).AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync(new Uri("/api/admin/store-categories", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.Post("/api/admin/store-categories", new { nameFr = "Hack" })).StatusCode);
    }
}
