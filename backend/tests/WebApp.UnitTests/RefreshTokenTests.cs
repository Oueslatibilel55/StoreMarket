using WebApp.Domain.Entities;

namespace WebApp.UnitTests;

public sealed class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Issue_WithoutFamily_StartsNewFamily()
    {
        var a = RefreshToken.Issue(Guid.NewGuid(), "h1", Now.AddDays(1));
        var b = RefreshToken.Issue(Guid.NewGuid(), "h2", Now.AddDays(1));

        Assert.NotEqual(a.FamilyId, b.FamilyId);
    }

    [Fact]
    public void IsActive_ExpiredOrRevoked_IsFalse()
    {
        var expired = RefreshToken.Issue(Guid.NewGuid(), "h", Now.AddMinutes(-1));
        var revoked = RefreshToken.Issue(Guid.NewGuid(), "h", Now.AddDays(1));
        revoked.Revoke(Now);

        Assert.False(expired.IsActive(Now));
        Assert.False(revoked.IsActive(Now));
    }

    [Fact]
    public void Revoke_Twice_KeepsFirstRevocation()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "h", Now.AddDays(1));
        var replacement = Guid.NewGuid();

        token.Revoke(Now, replacement);
        token.Revoke(Now.AddHours(1));

        Assert.Equal(Now, token.RevokedAt);
        Assert.Equal(replacement, token.ReplacedByTokenId);
    }
}
