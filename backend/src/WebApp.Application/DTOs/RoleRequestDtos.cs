using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

/// <summary>Ask for a role. <c>AcceptTerms</c> is required when the role is <c>Affiliate</c>.</summary>
public sealed record CreateRoleRequest(Role Role, string? Message, bool AcceptTerms = false);

public sealed record RefuseRoleRequest(string Reason);

/// <summary>A request as its author sees it.</summary>
public sealed record RoleRequestDto(
    Guid Id,
    Role Role,
    RoleRequestStatus Status,
    string? Message,
    string? DecisionReason,
    DateTime? DecidedAt,
    DateTime CreatedAt)
{
    public static RoleRequestDto From(RoleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(request.Id, request.Role, request.Status, request.Message, request.DecisionReason,
            request.DecidedAt, request.CreatedAt);
    }
}

public sealed record RoleRequestListQuery(RoleRequestStatus? Status, Role? Role, int? Page, int? PageSize);

/// <summary>Admin list row: the request plus who asked and the roles they hold right now.</summary>
public sealed record AdminRoleRequestItem(
    Guid Id,
    Guid UserId,
    string UserEmail,
    string UserFullName,
    IReadOnlyList<Role> UserRoles,
    Role Role,
    RoleRequestStatus Status,
    string? Message,
    string? DecisionReason,
    DateTime? DecidedAt,
    DateTime CreatedAt);
