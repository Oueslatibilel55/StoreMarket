using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WebApp.Application.DTOs;
using WebApp.Domain.Settings;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

public sealed class ImageEndpointsTests(ApiFactory factory)
{
    /// <summary>A real 1×1 PNG.</summary>
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private async Task<HttpClient> AdminClient()
    {
        var client = factory.CreateClient();
        client.WithToken((await client.SignInAdmin()).AccessToken);
        return client;
    }

    /// <summary>A buyer the admin made StoreOwner, with a refreshed token that carries the role.</summary>
    private async Task<HttpClient> StoreOwnerClient()
    {
        using var admin = await AdminClient();
        var owner = factory.CreateClient();
        var user = (await owner.Register()).User;
        await admin.Post($"/api/admin/users/{user.Id}/roles/StoreOwner");
        owner.WithToken((await (await owner.Post("/api/auth/refresh")).Read<AuthResponse>()).AccessToken);
        return owner;
    }

    private static Task<HttpResponseMessage> Upload(HttpClient client, byte[] bytes, string purpose,
        string fileName = "photo.png", string contentType = "image/png")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent(purpose), "purpose");
        return client.PostAsync(new Uri("/api/images", UriKind.Relative), form, Ct);
    }

    [Fact]
    public async Task StoreOwner_UploadsPhoto_ServedAsStaticFile()
    {
        using var owner = await StoreOwnerClient();

        var response = await Upload(owner, Png, "ProductPhoto");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var image = await response.Read<ImageDto>();
        Assert.Matches("^/uploads/product-photos/[0-9]{4}/[0-9]{2}/[0-9a-f]{32}\\.png$", image.Url);
        Assert.Equal("photo.png", image.OriginalName);

        using var visitor = factory.CreateClient();
        var file = await visitor.GetAsync(new Uri(image.Url, UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("image/png", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", file.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(Png, await file.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task DatabaseStorage_ServesTheSameUrl_AndForgetsDeletedFiles()
    {
        // The hosted API keeps files in PostgreSQL (Storage:Provider=Database, ADR 0006).
        using var hosted = factory.WithWebHostBuilder(b => b.UseSetting("Storage:Provider", "Database"));
        using var admin = hosted.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);

        var image = await (await Upload(admin, Png, "CategoryImage")).Read<ImageDto>();
        Assert.StartsWith("/uploads/", image.Url, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(factory.StorageRoot, image.Url["/uploads/".Length..])));

        using var visitor = hosted.CreateClient();
        var file = await visitor.GetAsync(new Uri(image.Url, UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("image/png", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", file.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(Png, await file.Content.ReadAsByteArrayAsync(Ct));

        (await admin.DeleteAsync(new Uri($"/api/images/{image.Id}", UriKind.Relative), Ct)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri(image.Url, UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri("/uploads/nothing/here.png", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task FakeImage_IsRefused_WhateverItsNameAndContentType()
    {
        using var owner = await StoreOwnerClient();

        var svg = await Upload(owner, "<svg onload=\"alert(1)\"/>"u8.ToArray(), "ProductPhoto", "evil.png", "image/png");
        Assert.Equal(HttpStatusCode.BadRequest, svg.StatusCode);
        Assert.Equal("image.unsupportedType", await svg.ProblemCode());

        var empty = await Upload(owner, [], "ProductPhoto");
        Assert.Equal("image.required", await empty.ProblemCode());
    }

    [Fact]
    public async Task TooLarge_Returns413_LimitIsTheSetting()
    {
        using var admin = await AdminClient();
        using var owner = await StoreOwnerClient();
        var big = new byte[(1024 * 1024) + 10];
        Png.CopyTo(big, 0);

        await admin.PutAsJsonAsync($"/api/admin/settings/{SettingKeys.UploadMaxImageSizeMb}", new { value = "1" }, Ct);
        try
        {
            var response = await Upload(owner, big, "ProductPhoto");
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            Assert.Equal("image.tooLarge", await response.ProblemCode());
        }
        finally
        {
            await admin.PutAsJsonAsync($"/api/admin/settings/{SettingKeys.UploadMaxImageSizeMb}", new { value = "5" }, Ct);
        }
    }

    [Fact]
    public async Task Purposes_AreLimitedByRole()
    {
        using var buyer = factory.CreateClient();
        buyer.WithToken((await buyer.Register()).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(buyer, Png, "ProductPhoto")).StatusCode);

        using var owner = await StoreOwnerClient();
        var category = await Upload(owner, Png, "CategoryImage");
        Assert.Equal("image.purposeNotAllowed", await category.ProblemCode());

        using var visitor = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Upload(visitor, Png, "ProductPhoto")).StatusCode);
    }

    [Fact]
    public async Task CategoryImage_ShowsInShop_AndCantBeDeletedWhileUsed()
    {
        using var admin = await AdminClient();
        var image = await (await Upload(admin, Png, "CategoryImage")).Read<ImageDto>();

        var created = await admin.Post("/api/admin/store-categories",
            new { nameFr = $"Déco {Guid.NewGuid():N}"[..20], imageId = image.Id });
        var category = await created.Read<AdminStoreCategoryDto>();
        Assert.Equal(image.Url, category.ImageUrl);

        using var visitor = factory.CreateClient();
        var shop = await (await visitor.GetAsync(new Uri("/api/store-categories", UriKind.Relative), Ct)).Read<List<StoreCategoryDto>>();
        Assert.Equal(image.Url, Assert.Single(shop, c => c.Id == category.Id).ImageUrl);

        var inUse = await admin.DeleteAsync(new Uri($"/api/images/{image.Id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Conflict, inUse.StatusCode);
        Assert.Equal("image.inUse", await inUse.ProblemCode());

        // Once the category drops it, the image can go — file included.
        await admin.PutAsJsonAsync($"/api/admin/store-categories/{category.Id}",
            new { nameFr = category.Name.Fr, slug = category.Slug, imageId = (Guid?)null }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(new Uri($"/api/images/{image.Id}", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri(image.Url, UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task Category_RefusesAnImageOfAnotherPurpose()
    {
        using var owner = await StoreOwnerClient();
        var photo = await (await Upload(owner, Png, "ProductPhoto")).Read<ImageDto>();
        using var admin = await AdminClient();

        var response = await admin.Post("/api/admin/store-categories", new { nameFr = $"Sport {Guid.NewGuid():N}"[..20], imageId = photo.Id });

        Assert.Equal("image.wrongPurpose", await response.ProblemCode());
    }

    [Fact]
    public async Task OnlyTheUploader_CanDelete()
    {
        using var owner = await StoreOwnerClient();
        var image = await (await Upload(owner, Png, "StoreLogo")).Read<ImageDto>();
        using var other = await StoreOwnerClient();

        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(new Uri($"/api/images/{image.Id}", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(new Uri($"/api/images/{image.Id}", UriKind.Relative), Ct)).StatusCode);
    }
}
