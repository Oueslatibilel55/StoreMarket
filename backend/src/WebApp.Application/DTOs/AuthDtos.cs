using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

/// <summary>
/// Sign-up. <c>AccountType</c> is <c>Buyer</c> (client account, the default) or <c>Affiliate</c>; an affiliate
/// account needs <c>AcceptAffiliateTerms</c>. Other roles come later through an admin-approved role request.
/// </summary>
public sealed record RegisterRequest(
    string Email, string Password, string FullName, string Phone,
    Role AccountType = Role.Buyer, bool AcceptAffiliateTerms = false);

public sealed record LoginRequest(string Email, string Password);

public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string Phone,
    IReadOnlyList<Role> Roles,
    string? AffiliateCode,
    DateTime CreatedAt)
{
    public static UserDto From(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new(user.Id, user.Email, user.FullName, user.Phone, [.. user.Roles],
            user.HasRole(Role.Affiliate) ? user.AffiliateCode : null, user.CreatedAt);
    }
}

/// <summary>What the API returns to the client. The refresh token travels in an httpOnly cookie, never in JSON.</summary>
public sealed record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, UserDto User);

/// <summary>Result of a successful sign-in or refresh, before the API splits it into body + cookie.</summary>
public sealed record AuthSession(AuthResponse Response, string RefreshToken, DateTime RefreshTokenExpiresAt);

public sealed record BlockUserRequest(string Reason);

/// <summary>Admin view of a user (more than the user sees about themselves).</summary>
public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string FullName,
    string Phone,
    IReadOnlyList<Role> Roles,
    UserStatus Status,
    string? AffiliateCode,
    DateTime? AffiliateCodeDisabledAt,
    string? AffiliateCodeDisabledReason,
    DateTime? BlockedAt,
    string? BlockedReason,
    DateTime? LastLoginAt,
    DateTime CreatedAt)
{
    /// <summary>The store the user owns, if any (filled by the admin user service, not part of audit snapshots).</summary>
    public AdminUserStore? Store { get; init; }

    public static AdminUserDto From(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new(user.Id, user.Email, user.FullName, user.Phone, [.. user.Roles], user.Status,
            user.AffiliateCode, user.AffiliateCodeDisabledAt, user.AffiliateCodeDisabledReason, user.BlockedAt, user.BlockedReason, user.LastLoginAt, user.CreatedAt);
    }
}
