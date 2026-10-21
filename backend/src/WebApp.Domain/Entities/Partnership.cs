using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Domain.Entities;

/// <summary>
/// Link between an affiliate and a store (012). The affiliate asks, <b>only the store owner</b> accepts
/// or refuses; an accepted partnership can be ended later by the owner (or by an admin, for fraud).
/// An affiliate earns on a store's products only while their partnership is accepted. A refused,
/// cancelled or ended partnership is kept as history; the affiliate may ask again afterwards.
/// </summary>
public sealed class Partnership : AggregateRoot
{
    public const int MaxMessageLength = 500;
    public const int MaxReasonLength = 500;

    public Guid AffiliateId { get; private set; }
    public Guid StoreId { get; private set; }
    public PartnershipStatus Status { get; private set; }
    /// <summary>Optional note from the affiliate (who they are, how they sell…).</summary>
    public string? Message { get; private set; }
    /// <summary>When the owner accepted or refused (or the affiliate cancelled).</summary>
    public DateTime? DecidedAt { get; private set; }
    /// <summary>Optional reason of a refusal — shown to the affiliate.</summary>
    public string? DecisionReason { get; private set; }
    public DateTime? EndedAt { get; private set; }
    /// <summary>The store owner, or the admin who ended it.</summary>
    public Guid? EndedById { get; private set; }
    public string? EndReason { get; private set; }

    private Partnership() { }

    /// <summary>
    /// New request. Whether the user is an active affiliate, the store visible, not their own, and
    /// without an open partnership already is checked by the caller.
    /// </summary>
    public static Result<Partnership> Request(Guid affiliateId, Guid storeId, string? message)
    {
        var note = Optional(message);
        if (note?.Length > MaxMessageLength) return Result.Fail<Partnership>(DomainErrors.Partnership.MessageTooLong);

        return Result.Ok(new Partnership
        {
            AffiliateId = affiliateId,
            StoreId = storeId,
            Status = PartnershipStatus.Requested,
            Message = note,
        });
    }

    /// <summary>Requested or accepted: blocks a second request for the same store.</summary>
    public bool IsOpen => Status is PartnershipStatus.Requested or PartnershipStatus.Accepted;
    public bool IsActive => Status == PartnershipStatus.Accepted;

    public Result Accept(DateTime utcNow) => Decide(PartnershipStatus.Accepted, reason: null, utcNow);

    public Result Refuse(string? reason, DateTime utcNow)
    {
        var trimmed = Optional(reason);
        if (trimmed?.Length > MaxReasonLength) return Result.Fail(DomainErrors.Partnership.ReasonTooLong);
        return Decide(PartnershipStatus.Refused, trimmed, utcNow);
    }

    /// <summary>The affiliate withdraws their request while it is still waiting.</summary>
    public Result Cancel(DateTime utcNow) => Decide(PartnershipStatus.Cancelled, reason: null, utcNow);

    /// <summary>
    /// Stops an accepted partnership: the code is refused on the store's products from now on. Orders
    /// already placed keep their affiliate line (their coins are still credited on delivery).
    /// </summary>
    public Result End(Guid endedById, string? reason, DateTime utcNow)
    {
        if (!IsActive) return Result.Fail(DomainErrors.Partnership.NotAccepted);
        var trimmed = Optional(reason);
        if (trimmed?.Length > MaxReasonLength) return Result.Fail(DomainErrors.Partnership.ReasonTooLong);

        Status = PartnershipStatus.Ended;
        EndedAt = utcNow;
        EndedById = endedById;
        EndReason = trimmed;
        MarkUpdated();
        return Result.Ok();
    }

    private Result Decide(PartnershipStatus status, string? reason, DateTime utcNow)
    {
        if (Status != PartnershipStatus.Requested) return Result.Fail(DomainErrors.Partnership.NotRequested);

        Status = status;
        DecidedAt = utcNow;
        DecisionReason = reason;
        MarkUpdated();
        return Result.Ok();
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
