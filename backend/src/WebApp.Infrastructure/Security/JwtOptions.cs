using System.ComponentModel.DataAnnotations;

namespace WebApp.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; init; } = "WebApp.Api";
    [Required] public string Audience { get; init; } = "WebApp.Client";

    /// <summary>HMAC-SHA256 key, at least 32 characters. Never in appsettings: user-secrets / env var.</summary>
    [Required, MinLength(32)] public string Secret { get; init; } = string.Empty;

    [Range(1, 120)] public int AccessTokenMinutes { get; init; } = 15;
    [Range(1, 90)] public int RefreshTokenDays { get; init; } = 14;
}
