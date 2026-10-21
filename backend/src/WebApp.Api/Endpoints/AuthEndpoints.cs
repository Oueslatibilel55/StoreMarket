using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;

namespace WebApp.Api.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", Register)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Create an account (buyer) and sign in");

        group.MapPost("/login", Login)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Sign in with email and password");

        group.MapPost("/refresh", Refresh)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get a new access token from the refresh cookie (rotates it)");

        group.MapPost("/logout", Logout)
            .Produces(StatusCodes.Status204NoContent)
            .WithSummary("Revoke the refresh cookie's session");

        group.MapGet("/me", Me)
            .RequireAuthorization()
            .Produces<UserDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Current user");

        return app;
    }

    private static async Task<IResult> Register(
        RegisterRequest request, AuthService auth, HttpContext http, IHostEnvironment env, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        return result.IsSuccess ? TypedResults.Ok(Issue(result.Value, http, env)) : result.ToProblem();
    }

    private static async Task<IResult> Login(
        LoginRequest request, AuthService auth, HttpContext http, IHostEnvironment env, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        return result.IsSuccess ? TypedResults.Ok(Issue(result.Value, http, env)) : result.ToProblem();
    }

    private static async Task<IResult> Refresh(AuthService auth, HttpContext http, IHostEnvironment env, CancellationToken ct)
    {
        var result = await auth.RefreshAsync(RefreshTokenCookie.Read(http.Request), ct);
        if (result.IsSuccess) return TypedResults.Ok(Issue(result.Value, http, env));

        RefreshTokenCookie.Clear(http.Response, SecureCookies(env));
        return result.ToProblem();
    }

    private static async Task<IResult> Logout(AuthService auth, HttpContext http, IHostEnvironment env, CancellationToken ct)
    {
        await auth.LogoutAsync(RefreshTokenCookie.Read(http.Request), ct);
        RefreshTokenCookie.Clear(http.Response, SecureCookies(env));
        return TypedResults.NoContent();
    }

    private static async Task<IResult> Me(AuthService auth, HttpContext http, CancellationToken ct)
    {
        if (http.User.GetUserId() is not { } userId) return TypedResults.Unauthorized();

        var result = await auth.GetMeAsync(userId, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }

    private static AuthResponse Issue(AuthSession session, HttpContext http, IHostEnvironment env)
    {
        RefreshTokenCookie.Write(http.Response, session.RefreshToken, session.RefreshTokenExpiresAt, SecureCookies(env));
        return session.Response;
    }

    // Development runs on plain http (localhost); everywhere else the cookie is https-only.
    private static bool SecureCookies(IHostEnvironment env) => !env.IsDevelopment();
}
