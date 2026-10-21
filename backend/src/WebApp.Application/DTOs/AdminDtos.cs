using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

/// <summary>Who performs an admin action (taken from the access token).</summary>
public sealed record AdminActor(Guid Id, string Email);

public sealed record UserListQuery(string? Search, Role? Role, UserStatus? Status, int? Page, int? PageSize);

public sealed record AdminUserListItem(
    Guid Id,
    string Email,
    string FullName,
    string Phone,
    IReadOnlyList<Role> Roles,
    UserStatus Status,
    string? AffiliateCode,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    AdminUserStore? Store);

/// <summary>The store a user owns (one per owner), shown in the admin user screens.</summary>
public sealed record AdminUserStore(Guid Id, string Name, string Slug, StoreStatus Status);

public sealed record AuditQuery(string? TargetType, Guid? TargetId, Guid? ActorId, string? Action, int? Page, int? PageSize);

public sealed record AuditEntryDto(
    Guid Id,
    DateTime CreatedAt,
    Guid ActorId,
    string ActorEmail,
    string Action,
    string TargetType,
    Guid TargetId,
    string? TargetLabel,
    string? Reason,
    string? Before,
    string? After);

public sealed record UserCounts(int Total, int Active, int Blocked, int Buyers, int Affiliates, int StoreOwners, int Admins);
