using WebApp.Domain.Enums;

namespace WebApp.Api.Auth;

/// <summary>Authorization policies. Use with <c>.RequireAuthorization(AuthPolicies.Admin)</c>.</summary>
internal static class AuthPolicies
{
    public const string Admin = nameof(Role.Admin);
    public const string StoreOwner = nameof(Role.StoreOwner);
    public const string Affiliate = nameof(Role.Affiliate);

    public static IServiceCollection AddAuthPolicies(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .AddPolicy(Admin, p => p.RequireRole(nameof(Role.Admin)))
            .AddPolicy(StoreOwner, p => p.RequireRole(nameof(Role.StoreOwner)))
            .AddPolicy(Affiliate, p => p.RequireRole(nameof(Role.Affiliate)))
            .Services;
}
