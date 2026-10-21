using WebApp.Domain.Common;

namespace WebApp.Domain.Entities;

/// <summary>
/// A refresh token, stored as a SHA-256 hash only. Tokens rotate: each use revokes the
/// token and issues a new one in the same <see cref="FamilyId"/>. Presenting a token that
/// was already rotated means it leaked, so the whole family is revoked.
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime expiresAt, Guid? familyId = null) => new()
    {
        UserId = userId,
        FamilyId = familyId ?? Guid.CreateVersion7(),
        TokenHash = tokenHash,
        ExpiresAt = expiresAt,
    };

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActive(DateTime utcNow) => !IsRevoked && ExpiresAt > utcNow;

    public void Revoke(DateTime utcNow, Guid? replacedBy = null)
    {
        if (IsRevoked) return;
        RevokedAt = utcNow;
        ReplacedByTokenId = replacedBy;
        MarkUpdated();
    }
}
