using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.Services;

namespace WebApp.Api.Endpoints;

/// <summary>The signed-in user's own account. Roles are asked for in <see cref="RoleRequestEndpoints"/>.</summary>
internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me").WithTags("Account").RequireAuthorization();

        group.MapGet("/affiliate", GetAffiliate)
            .RequireAuthorization(AuthPolicies.Affiliate)
            .Produces<AffiliateInfo>()
            .WithSummary("My affiliate code (affiliates only)");

        return app;
    }

    internal sealed record AffiliateInfo(string Code);

    private static async Task<IResult> GetAffiliate(AuthService auth, HttpContext http, CancellationToken ct)
    {
        if (http.User.GetUserId() is not { } userId) return TypedResults.Unauthorized();

        var result = await auth.GetMeAsync(userId, ct);
        if (!result.IsSuccess) return result.ToProblem();
        return result.Value.AffiliateCode is { } code ? TypedResults.Ok(new AffiliateInfo(code)) : TypedResults.Forbid();
    }
}
