using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Domain.Entities;

/// <summary>
/// A user asks for a role they don't have yet (a buyer or store owner who wants to become an
/// affiliate, an affiliate who wants to open a store…). Only an admin decides. Approving it does
/// not change the user here: the caller then grants the role on the <see cref="User"/>.
/// </summary>
public sealed class RoleRequest : AggregateRoot
{
    public const int MaxMessageLength = 500;
    public const int MaxReasonLength = 500;

    public Guid UserId { get; private set; }
    public Role Role { get; private set; }
    public RoleRequestStatus Status { get; private set; }
    /// <summary>Optional note from the user (why, which store they plan to open…).</summary>
    public string? Message { get; private set; }
    /// <summary>When the user accepted the affiliate terms — set on affiliate requests only.</summary>
    public DateTime? TermsAcceptedAt { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public Guid? DecidedById { get; private set; }
    /// <summary>Why the admin refused — shown to the user.</summary>
    public string? DecisionReason { get; private set; }

    private RoleRequest() { }

    /// <summary>
    /// Buyer, Affiliate and StoreOwner can be requested; Admin never. An affiliate request needs
    /// the terms accepted now, by the user — the admin can't accept them on their behalf.
    /// Whether the user already has the role or a pending request is checked by the caller.
    /// </summary>
    public static Result<RoleRequest> Create(Guid userId, Role role, string? message, bool termsAccepted, DateTime utcNow)
    {
        if (role is not (Role.Buyer or Role.Affiliate or Role.StoreOwner))
            return Result.Fail<RoleRequest>(DomainErrors.RoleRequest.RoleNotRequestable);
        if (role == Role.Affiliate && !termsAccepted)
            return Result.Fail<RoleRequest>(DomainErrors.User.AffiliateTermsRequired);

        var note = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        if (note?.Length > MaxMessageLength) return Result.Fail<RoleRequest>(DomainErrors.RoleRequest.MessageTooLong);

        return Result.Ok(new RoleRequest
        {
            UserId = userId,
            Role = role,
            Status = RoleRequestStatus.Pending,
            Message = note,
            TermsAcceptedAt = role == Role.Affiliate ? utcNow : null,
        });
    }

    public bool IsPending => Status == RoleRequestStatus.Pending;

    public Result Approve(Guid adminId, DateTime utcNow) => Decide(RoleRequestStatus.Approved, adminId, reason: null, utcNow);

    public Result Refuse(Guid adminId, string reason, DateTime utcNow)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxReasonLength)
            return Result.Fail(DomainErrors.RoleRequest.ReasonRequired);
        return Decide(RoleRequestStatus.Refused, adminId, trimmed, utcNow);
    }

    /// <summary>The user withdraws their own request while it is still pending.</summary>
    public Result Cancel(DateTime utcNow) => Decide(RoleRequestStatus.Cancelled, decidedById: null, reason: null, utcNow);

    private Result Decide(RoleRequestStatus status, Guid? decidedById, string? reason, DateTime utcNow)
    {
        if (!IsPending) return Result.Fail(DomainErrors.RoleRequest.NotPending);

        Status = status;
        DecidedAt = utcNow;
        DecidedById = decidedById;
        DecisionReason = reason;
        MarkUpdated();
        return Result.Ok();
    }
}
