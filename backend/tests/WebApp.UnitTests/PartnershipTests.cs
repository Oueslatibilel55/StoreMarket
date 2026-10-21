using WebApp.Application.Services;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.UnitTests;

public sealed class PartnershipTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AffiliateId = Guid.CreateVersion7();
    private static readonly Guid StoreId = Guid.CreateVersion7();
    private static readonly Guid OwnerId = Guid.CreateVersion7();

    private static Partnership NewRequest() => Partnership.Request(AffiliateId, StoreId, "  Je vends sur Instagram  ").Value;

    private static Partnership Accepted()
    {
        var partnership = NewRequest();
        partnership.Accept(Now);
        return partnership;
    }

    [Fact]
    public void Request_IsOpen_WithTrimmedMessage()
    {
        var partnership = NewRequest();

        Assert.Equal(PartnershipStatus.Requested, partnership.Status);
        Assert.Equal("Je vends sur Instagram", partnership.Message);
        Assert.True(partnership.IsOpen);
        Assert.False(partnership.IsActive);
    }

    [Fact]
    public void Request_MessageTooLong_Fails() =>
        Assert.Equal(DomainErrors.Partnership.MessageTooLong,
            Partnership.Request(AffiliateId, StoreId, new string('a', Partnership.MaxMessageLength + 1)).Error);

    [Fact]
    public void Accept_MakesItActive()
    {
        var partnership = Accepted();

        Assert.Equal(PartnershipStatus.Accepted, partnership.Status);
        Assert.Equal(Now, partnership.DecidedAt);
        Assert.True(partnership.IsActive);
    }

    [Fact]
    public void Refuse_ReasonIsOptional_AndClosesIt()
    {
        var partnership = NewRequest();

        Assert.True(partnership.Refuse("   ", Now).IsSuccess);
        Assert.Equal(PartnershipStatus.Refused, partnership.Status);
        Assert.Null(partnership.DecisionReason);
        Assert.False(partnership.IsOpen);
    }

    [Fact]
    public void Refuse_KeepsTheReason()
    {
        var partnership = NewRequest();
        partnership.Refuse(" Pas de profil ", Now);
        Assert.Equal("Pas de profil", partnership.DecisionReason);
    }

    [Fact]
    public void Decisions_OnlyWhileRequested()
    {
        var partnership = Accepted();

        Assert.Equal(DomainErrors.Partnership.NotRequested, partnership.Accept(Now).Error);
        Assert.Equal(DomainErrors.Partnership.NotRequested, partnership.Refuse(null, Now).Error);
        Assert.Equal(DomainErrors.Partnership.NotRequested, partnership.Cancel(Now).Error);
    }

    [Fact]
    public void Cancel_ByTheAffiliate_BeforeADecision()
    {
        var partnership = NewRequest();

        Assert.True(partnership.Cancel(Now).IsSuccess);
        Assert.Equal(PartnershipStatus.Cancelled, partnership.Status);
        Assert.False(partnership.IsOpen);
    }

    [Fact]
    public void End_OnlyAnAcceptedPartnership()
    {
        Assert.Equal(DomainErrors.Partnership.NotAccepted, NewRequest().End(OwnerId, null, Now).Error);

        var partnership = Accepted();
        Assert.True(partnership.End(OwnerId, " Ventes trop faibles ", Now).IsSuccess);
        Assert.Equal(PartnershipStatus.Ended, partnership.Status);
        Assert.Equal(OwnerId, partnership.EndedById);
        Assert.Equal("Ventes trop faibles", partnership.EndReason);
        Assert.Equal(Now, partnership.EndedAt);
        Assert.False(partnership.IsActive);

        Assert.Equal(DomainErrors.Partnership.NotAccepted, partnership.End(OwnerId, null, Now).Error);
    }

    [Fact]
    public void End_ReasonTooLong_Fails() =>
        Assert.Equal(DomainErrors.Partnership.ReasonTooLong,
            Accepted().End(OwnerId, new string('a', Partnership.MaxReasonLength + 1), Now).Error);

    [Theory]
    [InlineData("Sana Trabelsi", "Sana T.")]
    [InlineData("  amira ben ali ", "amira A.")]
    [InlineData("Youssef", "Youssef")]
    [InlineData("", "")]
    public void ShortName_FirstNameAndLastInitial(string fullName, string expected) =>
        Assert.Equal(expected, AffiliateCodeService.ShortName(fullName));
}
