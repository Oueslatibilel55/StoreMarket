using WebApp.Domain.Common;

namespace WebApp.Domain.Entities;

/// <summary>
/// One admin action, append-only: who did what to which record, why, and the record's
/// state before and after (JSON snapshots). Never updated or deleted.
/// </summary>
public sealed class AuditEntry : BaseEntity
{
    public const int MaxActionLength = 64;
    public const int MaxReasonLength = 500;

    public Guid ActorId { get; private set; }
    public string ActorEmail { get; private set; } = null!;
    /// <summary>Stable code like <c>user.block</c>, <c>user.role.grant</c> — translated by the client.</summary>
    public string Action { get; private set; } = null!;
    public string TargetType { get; private set; } = null!;
    public Guid TargetId { get; private set; }
    public string? TargetLabel { get; private set; }
    public string? Reason { get; private set; }
    public string? Before { get; private set; }
    public string? After { get; private set; }

    private AuditEntry() { }

    public static AuditEntry Record(Guid actorId, string actorEmail, string action, string targetType, Guid targetId,
        string? targetLabel, string? reason, string? beforeJson, string? afterJson) => new()
    {
        ActorId = actorId,
        ActorEmail = actorEmail,
        Action = action,
        TargetType = targetType,
        TargetId = targetId,
        TargetLabel = targetLabel,
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        Before = beforeJson,
        After = afterJson,
    };
}
