using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using WebApp.Application.DTOs;

namespace WebApp.Api.Auth;

internal static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>The admin performing an action. The Admin policy guarantees a valid `sub` and `email`.</summary>
    public static AdminActor GetAdminActor(this ClaimsPrincipal user) =>
        new(user.GetUserId()!.Value, user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? "unknown");
}
