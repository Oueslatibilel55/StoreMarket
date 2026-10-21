using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WebApp.Application.DTOs;
using WebApp.Application.Interfaces;
using WebApp.Application.Options;
using WebApp.Application.Services;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Api.DemoSeed;

/// <summary>
/// Test data for the hosted app (ADR 0006), created once at startup when <c>DemoSeed:Password</c> is set: one
/// account per role (client, affiliate, two store owners — the admin is the BootstrapAdmin account), the store and
/// product categories, two stores with a few products and photos, an accepted partnership and an order with the
/// affiliate's code (one store delivered, the other still to confirm). Everything goes through the application
/// services, so the usual rules, audit and coin ledger apply. Data: <c>DemoSeed/demo-seed.json</c>, built by
/// <c>backend/scripts/demo-data/export-hosted-seed.mjs</c>. Skipped when the demo client already exists.
/// </summary>
internal sealed partial class DemoSeeder(IServiceScopeFactory scopes, IConfiguration config, ILogger<DemoSeeder> logger) : BackgroundService
{
    public const string ClientEmail = "leila.gharbi@storemarket.demo";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private sealed record Person(string Key, string Email, string FullName, string Phone, Role AccountType);

    private static readonly Person[] People =
    [
        new("client", ClientEmail, "Leila Gharbi", "50 112 233", Role.Buyer),
        new("affiliate", "sana.trabelsi@storemarket.demo", "Sana Trabelsi", "55 321 987", Role.Affiliate),
        new("owner", "karim.bensalah@storemarket.demo", "Karim Ben Salah", "22 145 678", Role.Buyer),
        new("owner2", "amel.zouari@storemarket.demo", "Amel Zouari", "22 693 580", Role.Buyer),
    ];

    private sealed record SeedFile(List<SeedStoreCategory> StoreCategories, List<SeedProductCategory> ProductCategories, List<SeedStore> Stores);
    private sealed record SeedStoreCategory(SaveStoreCategoryRequest Body, string? Image);
    private sealed record SeedProductCategory(string? ParentSlug, SaveProductCategoryRequest Body, List<SaveAttributeRequest> Attributes);
    private sealed record SeedStore(string Owner, string CategorySlug, SeedStoreBody Body, string? Logo, string? Banner, List<SeedProduct> Products);
    private sealed record SeedStoreBody(string Name, string Slug, string? Description, string City, int DefaultAffiliatePercent, decimal DeliveryFee,
        List<string>? DeliveryCities, int DeliveryMinDays, int DeliveryMaxDays);
    private sealed record SeedProduct(string CategorySlug, SeedProductBody Body, Dictionary<string, JsonElement> Values, List<string> Images);
    private sealed record SeedProductBody(string Name, string Slug, string? Description, decimal Price, decimal? CompareAtPrice, int Stock, int? AffiliatePercent);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var password = config["DemoSeed:Password"];
        if (string.IsNullOrWhiteSpace(password)) return;
        try
        {
            await SeedAsync(password, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never take the API down for demo data: log and carry on.
            LogFailed(ex);
        }
    }

    private async Task SeedAsync(string password, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<IUserRepository>();

        if (await users.EmailExistsAsync(User.NormalizeEmail(ClientEmail)!, ct))
        {
            LogAlreadyDone(ClientEmail);
            return;
        }

        // The admin is the BootstrapAdmin account: it must have signed up first.
        User? adminUser = null;
        foreach (var email in sp.GetRequiredService<IOptions<BootstrapAdminOptions>>().Value.Emails ?? [])
        {
            if (User.NormalizeEmail(email) is { } normalized && await users.GetByEmailAsync(normalized, ct) is { } u && u.HasRole(Role.Admin))
            {
                adminUser = u;
                break;
            }
        }
        if (adminUser is null)
        {
            LogNoAdmin();
            return;
        }
        var admin = new AdminActor(adminUser.Id, adminUser.Email);
        var adminUploader = new Uploader(admin.Id, IsAdmin: true, IsStoreOwner: false);

        var path = Path.Combine(AppContext.BaseDirectory, "DemoSeed");
        var seed = JsonSerializer.Deserialize<SeedFile>(await File.ReadAllTextAsync(Path.Combine(path, "demo-seed.json"), ct), Json)
            ?? throw new InvalidOperationException("demo-seed.json is empty");
        var images = sp.GetRequiredService<ImageService>();
        async Task<Guid?> UploadAsync(Uploader uploader, ImagePurpose purpose, string? file)
        {
            if (file is null) return null;
            await using var stream = File.OpenRead(Path.Combine(path, "images", file));
            return Must(await images.UploadAsync(uploader, purpose, stream, stream.Length, Path.GetFileName(file), ct), $"image {file}").Id;
        }

        // 1. Accounts (affiliate terms accepted at sign-up), store owners approved by the admin.
        var auth = sp.GetRequiredService<AuthService>();
        var ids = new Dictionary<string, UserDto>();
        foreach (var p in People)
        {
            var session = Must(await auth.RegisterAsync(new RegisterRequest(p.Email, password, p.FullName, p.Phone, p.AccountType, p.AccountType == Role.Affiliate), ct), p.Email);
            ids[p.Key] = session.Response.User;
        }
        var roleRequests = sp.GetRequiredService<RoleRequestService>();
        foreach (var key in new[] { "owner", "owner2" })
        {
            var request = Must(await roleRequests.CreateAsync(ids[key].Id, new CreateRoleRequest(Role.StoreOwner, "Ouverture de ma boutique (démo)"), ct), "role request");
            Must(await roleRequests.ApproveAsync(admin, request.Id, ct), "approve StoreOwner");
        }

        // 2. Store categories and the product category tree with its fields (admin, audited).
        var storeCategories = sp.GetRequiredService<StoreCategoryService>();
        var storeCategoryIds = new Dictionary<string, Guid>();
        foreach (var c in seed.StoreCategories)
        {
            var created = Must(await storeCategories.CreateAsync(admin, c.Body with { ImageId = await UploadAsync(adminUploader, ImagePurpose.CategoryImage, c.Image) }, ct), $"store category {c.Body.Slug}");
            storeCategoryIds[created.Slug] = created.Id;
        }
        var productCategories = sp.GetRequiredService<ProductCategoryService>();
        var productCategoryIds = new Dictionary<string, Guid>();
        foreach (var c in seed.ProductCategories)
        {
            var parentId = c.ParentSlug is null ? (Guid?)null : productCategoryIds[c.ParentSlug];
            var created = Must(await productCategories.CreateAsync(admin, c.Body with { ParentId = parentId }, ct), $"product category {c.Body.Slug}");
            foreach (var a in c.Attributes) Must(await productCategories.AddAttributeAsync(admin, created.Id, a, ct), $"field of {c.Body.Slug}");
            productCategoryIds[created.Slug] = created.Id;
        }

        // 3. Two stores with their products (owners).
        var stores = sp.GetRequiredService<StoreService>();
        var products = sp.GetRequiredService<ProductService>();
        var storeIds = new Dictionary<string, Guid>();
        var productIds = new Dictionary<string, List<Guid>>();
        foreach (var s in seed.Stores)
        {
            var owner = ids[s.Owner];
            var uploader = new Uploader(owner.Id, IsAdmin: false, IsStoreOwner: true);
            var b = s.Body;
            var store = Must(await stores.CreateAsync(owner.Id, new SaveStoreRequest(
                b.Name, b.Slug, storeCategoryIds[s.CategorySlug], b.Description, owner.Phone, b.City, null,
                await UploadAsync(uploader, ImagePurpose.StoreLogo, s.Logo), await UploadAsync(uploader, ImagePurpose.StoreBanner, s.Banner),
                b.DefaultAffiliatePercent, b.DeliveryFee, b.DeliveryCities ?? [.. Governorates.All], b.DeliveryMinDays, b.DeliveryMaxDays, null, null, null), ct), $"store {b.Slug}");
            storeIds[s.Owner] = store.Id;
            productIds[s.Owner] = [];

            foreach (var p in s.Products)
            {
                var schema = Must(await productCategories.GetSchemaAsync(p.CategorySlug, ct), $"schema {p.CategorySlug}");
                var types = schema.Attributes.ToDictionary(a => a.Attribute.Key, a => a.Attribute.Type);
                var imageIds = new List<Guid>();
                foreach (var file in p.Images) imageIds.Add((await UploadAsync(uploader, ImagePurpose.ProductPhoto, file))!.Value);
                var pb = p.Body;
                var product = Must(await products.CreateAsync(owner.Id, new SaveProductRequest(
                    pb.Name, pb.Slug, productCategoryIds[p.CategorySlug], pb.Description, pb.Price, pb.CompareAtPrice, null, pb.Stock, pb.AffiliatePercent,
                    ProductStatus.Published, imageIds, [.. p.Values.Select(v => Value(types, v.Key, v.Value))], [], []), ct), $"product {pb.Slug}");
                productIds[s.Owner].Add(product.Id);
            }
        }

        // 4. The affiliate works with both stores.
        var partnerships = sp.GetRequiredService<PartnershipService>();
        foreach (var key in new[] { "owner", "owner2" })
        {
            var asked = Must(await partnerships.RequestAsync(ids["affiliate"].Id, new CreatePartnershipRequest(storeIds[key], "Je partage vos produits sur Instagram"), ct), "partnership");
            Must(await partnerships.AcceptAsync(ids[key].Id, asked.Id, ct), "accept partnership");
        }

        // 5. The client orders with the affiliate's code; the first store delivers (coins), the second still has to confirm.
        var cart = sp.GetRequiredService<CartService>();
        var code = ids["affiliate"].AffiliateCode;
        foreach (var key in new[] { "owner", "owner2" })
        {
            foreach (var (productId, i) in productIds[key].Take(2).Select((id, i) => (id, i)))
                Must(await cart.AddAsync(ids["client"].Id, new AddCartLineRequest(productId, null, i + 1, code), ct), "cart line");
        }
        var client = ids["client"];
        var order = Must(await sp.GetRequiredService<CheckoutService>().PlaceAsync(client.Id,
            new CheckoutRequest(new OrderContactRequest(client.FullName, client.Phone, "tunis", "12 rue de Marseille, Tunis", null)), ct), "order");
        var lifecycle = sp.GetRequiredService<OrderLifecycleService>();
        var delivered = order.StoreOrders.First(so => so.StoreId == storeIds["owner"]);
        foreach (var status in new[] { StoreOrderStatus.Confirmed, StoreOrderStatus.Shipped, StoreOrderStatus.Delivered })
            Must(await lifecycle.ChangeAsStoreAsync(ids["owner"].Id, delivered.Id, new ChangeStoreOrderStatusRequest(status), ct), $"status {status}");

        LogDone(People.Length, storeIds.Count, order.Reference);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Demo seed failed")]
    private partial void LogFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo seed: already done ({Email} exists)")]
    private partial void LogAlreadyDone(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo seed: no admin yet — sign up with a BootstrapAdmin email, then restart the service")]
    private partial void LogNoAdmin();

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo seed done: {Accounts} accounts, {Stores} stores, order {Reference}")]
    private partial void LogDone(int accounts, int stores, string reference);

    /// <summary>A catalogue value → the request shape of its field type.</summary>
    private static AttributeValueRequest Value(Dictionary<string, AttributeType> types, string key, JsonElement v)
    {
        if (!types.TryGetValue(key, out var type)) throw new InvalidOperationException($"field '{key}' is not in the category");
        return type switch
        {
            AttributeType.SingleChoice or AttributeType.MultiChoice => new(key, null, null, null,
                v.ValueKind == JsonValueKind.Array ? [.. v.EnumerateArray().Select(o => o.GetString()!)] : [v.GetString()!]),
            AttributeType.Number => new(key, null, v.GetDecimal(), null, null),
            AttributeType.Boolean => new(key, null, null, v.GetBoolean(), null),
            _ => new(key, v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText(), null, null, null),
        };
    }

    private static T Must<T>(Result<T> result, string what) =>
        result.IsSuccess ? result.Value : throw new InvalidOperationException($"Demo seed — {what}: {result.Error}");
}
