using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Application.DTOs;

namespace WebApp.IntegrationTests;

/// <summary>Helpers shared by the integration tests.</summary>
internal static class TestApi
{
    public const string Password = "Secret123";
    public const string AdminEmail = "admin@tests.local";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string NewEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@example.com";

    public static object NewUser(string? email = null) => new
    {
        email = email ?? NewEmail(),
        password = Password,
        fullName = "Amira Ben Ali",
        phone = "22 123 456",
    };

    public static async Task<T> Read<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json, Ct))!;

    public static async Task<string?> ProblemCode(this HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    public static async Task<AuthResponse> Register(this HttpClient client, string? email = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", NewUser(email), Ct);
        response.EnsureSuccessStatusCode();
        return await response.Read<AuthResponse>();
    }

    /// <summary>Signs up with an affiliate account (Affiliate role + code, no Buyer).</summary>
    public static async Task<AuthResponse> RegisterAffiliate(this HttpClient client, string? email = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = email ?? NewEmail("affiliate"),
            password = Password,
            fullName = "Sana Trabelsi",
            phone = "55 321 987",
            accountType = "Affiliate",
            acceptAffiliateTerms = true,
        }, Ct);
        response.EnsureSuccessStatusCode();
        return await response.Read<AuthResponse>();
    }

    /// <summary>Signs in the bootstrap admin (configured in <see cref="ApiFactory"/>), registering it on first use.</summary>
    public static async Task<AuthResponse> SignInAdmin(this HttpClient client)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = AdminEmail, password = Password }, Ct);
        if (login.IsSuccessStatusCode) return await login.Read<AuthResponse>();

        // Tests run in parallel: several may register the admin at once, and only one wins the unique email.
        var register = await client.PostAsJsonAsync("/api/auth/register", NewUser(AdminEmail), Ct);
        if (register.IsSuccessStatusCode) return await register.Read<AuthResponse>();

        login = await client.PostAsJsonAsync("/api/auth/login", new { email = AdminEmail, password = Password }, Ct);
        login.EnsureSuccessStatusCode();
        return await login.Read<AuthResponse>();
    }

    /// <summary>A new user the admin made StoreOwner, with a refreshed token that carries the role.</summary>
    public static async Task<HttpClient> StoreOwnerClient(this ApiFactory factory)
    {
        using var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);
        var owner = factory.CreateClient();
        var user = (await owner.Register()).User;
        (await admin.Post($"/api/admin/users/{user.Id}/roles/StoreOwner")).EnsureSuccessStatusCode();
        owner.WithToken((await (await owner.Post("/api/auth/refresh")).Read<AuthResponse>()).AccessToken);
        return owner;
    }

    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static Task<HttpResponseMessage> Post(this HttpClient client, string url, object? body = null) =>
        body is null
            ? client.PostAsync(new Uri(url, UriKind.Relative), null, Ct)
            : client.PostAsJsonAsync(url, body, Ct);
}
