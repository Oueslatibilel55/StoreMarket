namespace WebApp.Api.Auth;

/// <summary>
/// The refresh token lives only in this httpOnly cookie: JavaScript can't read it (XSS),
/// SameSite=Strict keeps other sites from sending it (CSRF), and the path limits it to
/// the auth endpoints.
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "webapp_refresh";
    private const string Path = "/api/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Write(HttpResponse response, string token, DateTime expiresAt, bool secure) =>
        response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = expiresAt,
            IsEssential = true,
        });

    public static void Clear(HttpResponse response, bool secure) =>
        response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = Path,
        });
}
