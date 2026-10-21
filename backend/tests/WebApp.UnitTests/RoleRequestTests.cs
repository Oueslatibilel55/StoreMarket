using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.UnitTests;

public sealed class RoleRequestTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid AdminId = Guid.CreateVersion7();

    private static RoleRequest NewRequest(Role role = Role.StoreOwner) =>
        RoleRequest.Create(UserId, role, "  I sell perfumes  ", termsAccepted: true, Now).Value;

    [Theory]
    [InlineData(Role.Buyer)]
    [InlineData(Role.Affiliate)]
    [InlineData(Role.StoreOwner)]
    public void Create_RequestableRole_IsPending(Role role)
    {
        var request = NewRequest(role);

        Assert.Equal(RoleRequestStatus.Pending, request.Status);
        Assert.Equal("I sell perfumes", request.Message);
    }

    [Fact]
    public void Create_Admin_IsRefused() =>
        Assert.Equal(DomainErrors.RoleRequest.RoleNotRequestable, RoleRequest.Create(UserId, Role.Admin, null, true, Now).Error);

    [Fact]
    public void Create_AffiliateWithoutTerms_Fails() =>
        Assert.Equal(DomainErrors.User.AffiliateTermsRequired, RoleRequest.Create(UserId, Role.Affiliate, null, false, Now).Error);

    [Fact]
    public void Create_KeepsTermsDate_OnlyForAffiliate()
    {
        Assert.Equal(Now, NewRequest(Role.Affiliate).TermsAcceptedAt);
        Assert.Null(NewRequest(Role.StoreOwner).TermsAcceptedAt);
    }

    [Fact]
    public void Create_MessageTooLong_Fails() =>
        Assert.Equal(DomainErrors.RoleRequest.MessageTooLong,
            RoleRequest.Create(UserId, Role.StoreOwner, new string('x', RoleRequest.MaxMessageLength + 1), false, Now).Error);

    [Fact]
    public void Approve_RecordsDecision()
    {
        var request = NewRequest();

        Assert.True(request.Approve(AdminId, Now).IsSuccess);
        Assert.Equal(RoleRequestStatus.Approved, request.Status);
        Assert.Equal(AdminId, request.DecidedById);
        Assert.Equal(Now, request.DecidedAt);
    }

    [Fact]
    public void Refuse_NeedsReason()
    {
        var request = NewRequest();

        Assert.Equal(DomainErrors.RoleRequest.ReasonRequired, request.Refuse(AdminId, "  ", Now).Error);
        Assert.True(request.Refuse(AdminId, " No store documents ", Now).IsSuccess);
        Assert.Equal(RoleRequestStatus.Refused, request.Status);
        Assert.Equal("No store documents", request.DecisionReason);
    }

    [Fact]
    public void Decided_CannotBeDecidedAgain()
    {
        var request = NewRequest();
        request.Approve(AdminId, Now);

        Assert.Equal(DomainErrors.RoleRequest.NotPending, request.Refuse(AdminId, "late", Now).Error);
        Assert.Equal(DomainErrors.RoleRequest.NotPending, request.Cancel(Now).Error);
        Assert.Equal(DomainErrors.RoleRequest.NotPending, request.Approve(AdminId, Now).Error);
    }

    [Fact]
    public void Cancel_PendingRequest()
    {
        var request = NewRequest();

        Assert.True(request.Cancel(Now).IsSuccess);
        Assert.Equal(RoleRequestStatus.Cancelled, request.Status);
        Assert.Null(request.DecidedById);
    }
}
