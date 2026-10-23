using System.Net;
using System.Net.Http.Json;
using WebApp.Application.DTOs;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>Ratings and reviews (017): only delivered parcels are rated, once; owner replies; admin hides; averages follow.</summary>
public sealed class ReviewEndpointsTests(ApiFactory factory)
{
    private static readonly string[] Tunis = ["tunis"];

    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString("N")[..10]}";

    /// <summary>A phone no other test uses: guest orders are matched to accounts by phone.</summary>
    private static string NewPhone() => $"5{Random.Shared.Next(1_000_000, 9_999_999)}";

    private sealed record Shop(HttpClient Admin, HttpClient Owner, string StoreSlug, ProductDto Musc, ProductDto Ambre) : IDisposable
    {
        public void Dispose()
        {
            Admin.Dispose();
            Owner.Dispose();
        }
    }

    private sealed record Buyer(HttpClient Client, string Phone, OrderDto Order, Guid StoreOrderId) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    /// <summary>A store with two published products.</summary>
    private async Task<Shop> ArrangeShop()
    {
        var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);
        var category = await (await admin.Post("/api/admin/product-categories", new { nameFr = Unique("Parfums") })).Read<AdminProductCategoryDto>();
        var storeCategory = await (await admin.Post("/api/admin/store-categories", new { nameFr = Unique("Beauté") })).Read<AdminStoreCategoryDto>();

        var owner = await factory.StoreOwnerClient();
        var store = await (await owner.Post("/api/me/store", new
        {
            name = Unique("Boutique"), categoryId = storeCategory.Id, phone = "22 145 678", city = "tunis",
            defaultAffiliatePercent = 10, deliveryFee = 7m, deliveryCities = Tunis, deliveryMinDays = 1, deliveryMaxDays = 2,
        })).Read<MyStoreDto>();
        async Task<ProductDto> Product(string name) => await (await owner.Post("/api/me/store/products", new
        {
            name = Unique(name), categoryId = category.Id, price = 50m, stock = 20, status = "Published",
        })).Read<ProductDto>();
        return new Shop(admin, owner, store.Slug, await Product("Musc"), await Product("Ambre"));
    }

    /// <summary>A signed-in client with its own phone, who ordered both products.</summary>
    private async Task<Buyer> Order(Shop shop, bool deliver = true)
    {
        var client = factory.CreateClient();
        var phone = NewPhone();
        var auth = await (await client.PostAsJsonAsync("/api/auth/register",
            new { email = NewEmail("client"), password = Password, fullName = "Leila Gharbi", phone, accountType = "Buyer" }, Ct)).Read<AuthResponse>();
        client.WithToken(auth.AccessToken);
        (await client.Post("/api/me/cart/lines", new { productId = shop.Musc.Id, quantity = 1 })).EnsureSuccessStatusCode();
        (await client.Post("/api/me/cart/lines", new { productId = shop.Ambre.Id, quantity = 2 })).EnsureSuccessStatusCode();
        var order = await (await client.Post("/api/orders", new
        {
            contact = new { fullName = "Leila Gharbi", phone, city = "tunis", address = "12 rue de Marseille" },
        })).Read<OrderDto>();
        var storeOrderId = order.StoreOrders.Single().Id;
        if (deliver) await Deliver(shop, storeOrderId);
        return new Buyer(client, phone, order, storeOrderId);
    }

    private static async Task Deliver(Shop shop, Guid storeOrderId)
    {
        (await shop.Owner.Post($"/api/me/store/orders/{storeOrderId}/status", new { status = "Shipped" })).EnsureSuccessStatusCode();
        (await shop.Owner.Post($"/api/me/store/orders/{storeOrderId}/status", new { status = "Delivered" })).EnsureSuccessStatusCode();
    }

    private static async Task<T> Get<T>(HttpClient client, string url) =>
        await (await client.GetAsync(new Uri(url, UriKind.Relative), Ct)).Read<T>();

    private static Guid LineOf(Buyer b, ProductDto product) =>
        b.Order.StoreOrders.Single().Lines.Single(l => l.ProductId == product.Id).Id;

    private static Task<HttpResponseMessage> Rate(Buyer b, Guid? lineId, int stars, string? comment = null) =>
        b.Client.Post("/api/me/reviews", new { storeOrderId = b.StoreOrderId, orderLineId = lineId, stars, comment });

    [Fact]
    public async Task OnlyDeliveredParcels_AreRated_OncePerLineAndStore_AveragesFollow()
    {
        using var shop = await ArrangeShop();
        using var b = await Order(shop, deliver: false);

        Assert.Empty((await Get<MyReviewsDto>(b.Client, "/api/me/reviews")).ToWrite);
        var early = await Rate(b, LineOf(b, shop.Musc), 5);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("review.notDelivered", await early.ProblemCode());

        await Deliver(shop, b.StoreOrderId);
        var mine = await Get<MyReviewsDto>(b.Client, "/api/me/reviews");
        Assert.Equal(3, mine.ToWrite.Count); // two products + the store
        Assert.Single(mine.ToWrite, t => t.Target == ReviewTarget.Store);

        Assert.Equal("review.starsInvalid", await (await Rate(b, LineOf(b, shop.Musc), 6)).ProblemCode());
        var created = await Rate(b, LineOf(b, shop.Musc), 5, "  Tient toute la journée  ");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var review = await created.Read<MyReviewDto>();
        Assert.Equal((ReviewTarget.Product, 5, "Tient toute la journée", true), (review.Target, review.Stars, review.Comment, review.CanEdit));
        (await Rate(b, null, 3, "Livraison un peu lente")).EnsureSuccessStatusCode();

        var again = await Rate(b, LineOf(b, shop.Musc), 1);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("review.alreadyReviewed", await again.ProblemCode());

        mine = await Get<MyReviewsDto>(b.Client, "/api/me/reviews");
        Assert.Equal(LineOf(b, shop.Ambre), Assert.Single(mine.ToWrite).OrderLineId);
        Assert.Equal(2, mine.Written.Count);

        using var anonymous = factory.CreateClient();
        var reviews = await Get<PublicReviewsDto>(anonymous, $"/api/products/{shop.Musc.Slug}/reviews");
        Assert.Equal((5.00m, 1), (reviews.Stats.Average, reviews.Stats.Count));
        Assert.Equal([0, 0, 0, 0, 1], reviews.Stats.Stars);
        var shown = Assert.Single(reviews.Reviews.Items);
        Assert.Equal(("Leila G.", "Tient toute la journée"), (shown.AuthorName, shown.Comment));

        var product = await Get<PublicProductDto>(anonymous, $"/api/products/{shop.Musc.Slug}");
        Assert.Equal(new RatingDto(5.00m, 1), product.Rating);
        var store = await Get<PublicStoreDto>(anonymous, $"/api/stores/{shop.StoreSlug}");
        Assert.Equal(new RatingDto(3.00m, 1), store.Rating);
        var storeReviews = await Get<PublicReviewsDto>(anonymous, $"/api/stores/{shop.StoreSlug}/reviews");
        Assert.Equal("Livraison un peu lente", Assert.Single(storeReviews.Reviews.Items).Comment);

        // The rated product comes first when sorting by rating.
        var topRated = await Get<PagedResult<ProductCardDto>>(anonymous, $"/api/products?store={shop.StoreSlug}&sort=TopRated");
        Assert.Equal(shop.Musc.Id, topRated.Items[0].Id);
        Assert.Equal(new RatingDto(null, 0), topRated.Items[1].Rating);
    }

    [Fact]
    public async Task SomeoneElse_CannotRate_TheOrder_NorEditTheReview()
    {
        using var shop = await ArrangeShop();
        using var b = await Order(shop);
        var review = await (await Rate(b, LineOf(b, shop.Musc), 4)).Read<MyReviewDto>();

        using var stranger = factory.CreateClient();
        stranger.WithToken((await stranger.Register()).AccessToken);
        var foreign = await stranger.Post("/api/me/reviews", new { storeOrderId = b.StoreOrderId, orderLineId = LineOf(b, shop.Ambre), stars = 1 });
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal("review.orderNotFound", await foreign.ProblemCode());
        var edit = await stranger.PutAsJsonAsync($"/api/me/reviews/{review.Id}", new { stars = 1 }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);

        // The store owner never rates their own store.
        var own = await shop.Owner.Post("/api/me/reviews", new { storeOrderId = b.StoreOrderId, stars = 5 });
        Assert.Equal(HttpStatusCode.NotFound, own.StatusCode);
    }

    [Fact]
    public async Task Author_Edits_WithinTheWindow_Only()
    {
        using var shop = await ArrangeShop();
        using var b = await Order(shop);
        var review = await (await Rate(b, LineOf(b, shop.Musc), 4, "Bien")).Read<MyReviewDto>();

        var edited = await (await b.Client.PutAsJsonAsync($"/api/me/reviews/{review.Id}", new { stars = 2, comment = "Odeur qui part vite" }, Ct))
            .Read<MyReviewDto>();
        Assert.Equal((2, "Odeur qui part vite"), (edited.Stars, edited.Comment));
        Assert.NotNull(edited.EditedAt);
        Assert.Equal(new RatingDto(2.00m, 1), (await Get<PublicProductDto>(b.Client, $"/api/products/{shop.Musc.Slug}")).Rating);

        // A zero-day window (setting) closes new reviews at once; older reviews keep their frozen window.
        (await shop.Admin.PutAsJsonAsync("/api/admin/settings/reviews.editWindowDays", new { value = "0", reason = "Test" }, Ct)).EnsureSuccessStatusCode();
        try
        {
            var store = await (await Rate(b, null, 5)).Read<MyReviewDto>();
            Assert.False(store.CanEdit);
            var late = await b.Client.PutAsJsonAsync($"/api/me/reviews/{store.Id}", new { stars = 1 }, Ct);
            Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
            Assert.Equal("review.editWindowClosed", await late.ProblemCode());
            (await b.Client.PutAsJsonAsync($"/api/me/reviews/{review.Id}", new { stars = 3 }, Ct)).EnsureSuccessStatusCode();
        }
        finally
        {
            (await shop.Admin.PutAsJsonAsync("/api/admin/settings/reviews.editWindowDays", new { value = "30", reason = "Test" }, Ct)).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Owner_Replies_Publicly_OnlyOnTheirStore()
    {
        using var shop = await ArrangeShop();
        using var b = await Order(shop);
        var review = await (await Rate(b, LineOf(b, shop.Musc), 3, "Correct")).Read<MyReviewDto>();

        var list = await Get<StoreReviewsDto>(shop.Owner, "/api/me/store/reviews");
        Assert.Equal((1, 1), (list.Unanswered, list.Products.Count));
        Assert.Equal(0, list.Store.Count);
        Assert.Equal(b.Order.Reference, Assert.Single(list.Reviews.Items).OrderReference);

        Assert.Equal("review.replyRequired", await (await shop.Owner.PutAsJsonAsync($"/api/me/store/reviews/{review.Id}/reply", new { reply = " " }, Ct)).ProblemCode());
        var replied = await (await shop.Owner.PutAsJsonAsync($"/api/me/store/reviews/{review.Id}/reply", new { reply = "Merci Leila !" }, Ct))
            .Read<StoreReviewDto>();
        Assert.Equal("Merci Leila !", replied.Reply);
        Assert.Equal(0, (await Get<StoreReviewsDto>(shop.Owner, "/api/me/store/reviews?replied=false")).Reviews.Total);

        var shown = Assert.Single((await Get<PublicReviewsDto>(b.Client, $"/api/products/{shop.Musc.Slug}/reviews")).Reviews.Items);
        Assert.Equal("Merci Leila !", shown.Reply);

        using var otherShop = await ArrangeShop();
        var foreign = await otherShop.Owner.PutAsJsonAsync($"/api/me/store/reviews/{review.Id}/reply", new { reply = "Hello" }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        var removed = await (await shop.Owner.DeleteAsync(new Uri($"/api/me/store/reviews/{review.Id}/reply", UriKind.Relative), Ct)).Read<StoreReviewDto>();
        Assert.Null(removed.Reply);
    }

    [Fact]
    public async Task Admin_Hides_AbusiveReview_LeftOutOfAverages_Audited_ThenUnhides()
    {
        using var shop = await ArrangeShop();
        using var b = await Order(shop);
        var review = await (await Rate(b, LineOf(b, shop.Musc), 1, "Arnaque !!!")).Read<MyReviewDto>();

        Assert.Equal("review.reasonRequired", await (await shop.Admin.Post($"/api/admin/reviews/{review.Id}/hide", new { reason = "" })).ProblemCode());
        var hidden = await (await shop.Admin.Post($"/api/admin/reviews/{review.Id}/hide", new { reason = "Propos injurieux" })).Read<AdminReviewDto>();
        Assert.Equal("Propos injurieux", hidden.HiddenReason);

        var product = await Get<PublicProductDto>(b.Client, $"/api/products/{shop.Musc.Slug}");
        Assert.Equal(new RatingDto(null, 0), product.Rating);
        Assert.Empty((await Get<PublicReviewsDto>(b.Client, $"/api/products/{shop.Musc.Slug}/reviews")).Reviews.Items);

        var mine = Assert.Single((await Get<MyReviewsDto>(b.Client, "/api/me/reviews")).Written);
        Assert.True(mine.Hidden);
        Assert.False(mine.CanEdit);
        Assert.Equal("review.hidden", await (await b.Client.PutAsJsonAsync($"/api/me/reviews/{review.Id}", new { stars = 5 }, Ct)).ProblemCode());
        Assert.True((await Get<StoreReviewsDto>(shop.Owner, "/api/me/store/reviews")).Reviews.Items.Single().Hidden);

        var listed = await Get<PagedResult<AdminReviewDto>>(shop.Admin, $"/api/admin/reviews?hidden=true&search={Uri.EscapeDataString(shop.Musc.Name)}");
        Assert.Equal(review.Id, Assert.Single(listed.Items).Id);

        var audit = await Get<PagedResult<AuditEntryDto>>(shop.Admin, $"/api/admin/audit?targetType=Review&targetId={review.Id}");
        var entry = Assert.Single(audit.Items);
        Assert.Equal(("review.hide", "Propos injurieux"), (entry.Action, entry.Reason));

        (await shop.Admin.Post($"/api/admin/reviews/{review.Id}/unhide", new { reason = "Client contacté, avis légitime" })).EnsureSuccessStatusCode();
        Assert.Equal(new RatingDto(1.00m, 1), (await Get<PublicProductDto>(b.Client, $"/api/products/{shop.Musc.Slug}")).Rating);
        Assert.Equal("review.notHidden", await (await shop.Admin.Post($"/api/admin/reviews/{review.Id}/unhide", new { reason = "x" })).ProblemCode());

        using var plain = factory.CreateClient();
        plain.WithToken((await plain.Register()).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.Post($"/api/admin/reviews/{review.Id}/hide", new { reason = "x" })).StatusCode);
    }

    [Fact]
    public async Task GuestOrder_IsRated_AfterRegisteringWithTheSamePhone()
    {
        using var shop = await ArrangeShop();
        var phone = NewPhone();
        using var guest = factory.CreateClient();
        var order = await (await guest.Post("/api/orders", new
        {
            contact = new { fullName = "Amel Zouari", phone, city = "tunis", address = "3 avenue Habib Bourguiba" },
            lines = new object[] { new { productId = shop.Musc.Id, variantId = (Guid?)null, quantity = 1, affiliateCode = (string?)null } },
        })).Read<OrderDto>();
        var storeOrderId = order.StoreOrders.Single().Id;
        await Deliver(shop, storeOrderId);

        using var other = factory.CreateClient();
        other.WithToken((await (await other.PostAsJsonAsync("/api/auth/register",
            new { email = NewEmail("other"), password = Password, fullName = "Sami Ben Ali", phone = NewPhone(), accountType = "Buyer" }, Ct))
            .Read<AuthResponse>()).AccessToken);
        Assert.Empty((await Get<MyReviewsDto>(other, "/api/me/reviews")).ToWrite);

        using var amel = factory.CreateClient();
        amel.WithToken((await (await amel.PostAsJsonAsync("/api/auth/register",
            new { email = NewEmail("amel"), password = Password, fullName = "Amel Zouari", phone, accountType = "Buyer" }, Ct))
            .Read<AuthResponse>()).AccessToken);
        var toWrite = (await Get<MyReviewsDto>(amel, "/api/me/reviews")).ToWrite;
        Assert.Equal(2, toWrite.Count);
        var line = toWrite.Single(t => t.Target == ReviewTarget.Product);
        Assert.Equal(order.Reference, line.OrderReference);

        var created = await amel.Post("/api/me/reviews", new { storeOrderId, orderLineId = line.OrderLineId, stars = 5 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Amel Z.", Assert.Single((await Get<PublicReviewsDto>(amel, $"/api/products/{shop.Musc.Slug}/reviews")).Reviews.Items).AuthorName);
    }
}
