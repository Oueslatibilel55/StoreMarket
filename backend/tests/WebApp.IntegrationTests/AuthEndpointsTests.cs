using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WebApp.Application.DTOs;
using WebApp.Domain.Enums;

namespace WebApp.IntegrationTests;

public sealed class AuthEndpointsTests(ApiFactory factory)
{
    private const string Password = "Secret123";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object NewUser(string? email = null) => new
    {
        email = email ?? $"user-{Guid.NewGuid():N}@example.com",
        password = Password,
        fullName = "Amira Ben Ali",
        phone = "22 123 456",
    };

    private static async Task<AuthResponse> ReadAuth(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions, Ct))!;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static async Task<string?> ProblemCode(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static string? RefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith("webapp_refresh=", StringComparison.Ordinal))
            : null;

    [Fact]
    public async Task Register_ReturnsTokenUserAndHttpOnlyRefreshCookie()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewUser("New.Buyer@Example.com"), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await ReadAuth(response);
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.Equal("new.buyer@example.com", auth.User.Email);
        Assert.Equal("+21622123456", auth.User.Phone);
        Assert.Equal([Role.Buyer], auth.User.Roles);

        var cookie = RefreshCookie(response);
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", await response.Content.ReadAsStringAsync(Ct), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_SameEmailTwice_Returns409EmailTaken()
    {
        using var client = factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", NewUser(email), Ct);

        var response = await client.PostAsJsonAsync("/api/auth/register", NewUser(email.ToUpperInvariant()), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("auth.emailTaken", await ProblemCode(response));
    }

    [Fact]
    public async Task Register_InvalidInput_Returns400WithCode()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = "x@example.com", password = "short", fullName = "A", phone = "22123456" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("user.passwordTooWeak", await ProblemCode(response));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401InvalidCredentials()
    {
        using var client = factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", NewUser(email), Ct);

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong1234" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.invalidCredentials", await ProblemCode(response));
    }

    [Fact]
    public async Task Login_ThenMe_ReturnsCurrentUser()
    {
        using var client = factory.CreateClient();
        var email = $"me-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", NewUser(email), Ct);
        var login = await ReadAuth(await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password }, Ct));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = (await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions, Ct))!;
        Assert.Equal(email, me.Email);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndReusingTheOldOneKillsTheSession()
    {
        using var client = factory.CreateClient(); // keeps cookies between calls
        await client.PostAsJsonAsync("/api/auth/register", NewUser(), Ct);

        // First refresh works and rotates the cookie.
        var first = await client.PostAsync(new Uri("/api/auth/refresh", UriKind.Relative), null, Ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var stolenCookie = RefreshCookie(first)!.Split(';')[0];

        var second = await client.PostAsync(new Uri("/api/auth/refresh", UriKind.Relative), null, Ct);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // Replaying the already-rotated token (as a thief would) is refused...
        using var attacker = factory.CreateClient(new() { HandleCookies = false });
        using var replay = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        replay.Headers.Add("Cookie", stolenCookie);
        var replayed = await attacker.SendAsync(replay, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);

        // ...and revokes the whole family, so the legitimate session is gone too.
        var afterReuse = await client.PostAsync(new Uri("/api/auth/refresh", UriKind.Relative), null, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewUser(), Ct);

        var logout = await client.PostAsync(new Uri("/api/auth/logout", UriKind.Relative), null, Ct);
        var refresh = await client.PostAsync(new Uri("/api/auth/refresh", UriKind.Relative), null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
