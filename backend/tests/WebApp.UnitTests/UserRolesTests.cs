using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Domain.Events;

namespace WebApp.UnitTests;

public sealed class UserRolesTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private static User NewUser() =>
        User.Register("amira@example.com", "Secret123", "Amira", "22123456", p => p).Value;

    private static User NewAffiliate() =>
        User.RegisterAffiliate("sana@example.com", "Secret123", "Sana", "22123456", p => p, "DRF-45", termsAccepted: true, Now).Value;

    [Fact]
    public void ActivateAffiliate_WithTerms_AddsRoleCodeAndEvent()
    {
        var user = NewUser();

        var result = user.ActivateAffiliate("DRF-45", termsAccepted: true, Now);

        Assert.True(result.IsSuccess);
        Assert.True(user.HasRole(Role.Affiliate));
        Assert.Equal("DRF-45", user.AffiliateCode);
        Assert.Equal(Now, user.AffiliateTermsAcceptedAt);
        Assert.IsType<AffiliateActivatedEvent>(Assert.Single(user.DomainEvents));
    }

    [Fact]
    public void ActivateAffiliate_WithoutTerms_Fails()
    {
        var user = NewUser();

        Assert.Equal(DomainErrors.User.AffiliateTermsRequired, user.ActivateAffiliate("DRF-45", false, Now).Error);
        Assert.False(user.HasRole(Role.Affiliate));
    }

    [Fact]
    public void ActivateAffiliate_Twice_KeepsFirstCode()
    {
        var user = NewUser();
        user.ActivateAffiliate("DRF-45", true, Now);

        user.ActivateAffiliate("XYZ-99", true, Now);

        Assert.Equal("DRF-45", user.AffiliateCode);
        Assert.Single(user.Roles, Role.Affiliate);
    }

    [Fact]
    public void ActivateAffiliate_AfterRevoke_GetsSameCodeBack()
    {
        var user = NewUser();
        user.ActivateAffiliate("DRF-45", true, Now);
        user.RevokeRole(Role.Affiliate);

        user.ActivateAffiliate(user.AffiliateCode!, true, Now);

        Assert.True(user.HasRole(Role.Affiliate));
        Assert.Equal("DRF-45", user.AffiliateCode);
    }

    [Fact]
    public void ActivateAffiliate_WhenBlocked_Fails()
    {
        var user = NewUser();
        user.Block("fraud", Now);

        Assert.Equal(DomainErrors.User.Blocked, user.ActivateAffiliate("DRF-45", true, Now).Error);
    }

    [Fact]
    public void GrantRole_Affiliate_MustGoThroughActivation() =>
        Assert.Equal(DomainErrors.User.UseAffiliateActivation, NewUser().GrantRole(Role.Affiliate).Error);

    [Fact]
    public void GrantRole_Twice_DoesNotDuplicate()
    {
        var user = NewUser();
        user.GrantRole(Role.Admin);
        user.GrantRole(Role.Admin);

        Assert.Equal([Role.Buyer, Role.Admin], user.Roles);
    }

    [Fact]
    public void RevokeRole_LastBaseRole_Fails()
    {
        var buyer = NewUser();
        buyer.GrantRole(Role.StoreOwner);

        Assert.Equal(DomainErrors.User.BaseRoleRequired, buyer.RevokeRole(Role.Buyer).Error);
        Assert.Equal(DomainErrors.User.BaseRoleRequired, NewAffiliate().RevokeRole(Role.Affiliate).Error);
    }

    [Fact]
    public void RevokeRole_Buyer_AllowedWhenAlsoAffiliate()
    {
        var user = NewUser();
        user.ActivateAffiliate("DRF-45", true, Now);

        Assert.True(user.RevokeRole(Role.Buyer).IsSuccess);
        Assert.Equal([Role.Affiliate], user.Roles);
    }

    [Fact]
    public void RegisterAffiliate_GivesAffiliateRoleAndCode_WithoutBuyer()
    {
        var user = NewAffiliate();

        Assert.Equal([Role.Affiliate], user.Roles);
        Assert.Equal("DRF-45", user.AffiliateCode);
        Assert.Equal(Now, user.AffiliateTermsAcceptedAt);
    }

    [Fact]
    public void RegisterAffiliate_WithoutTerms_Fails() =>
        Assert.Equal(DomainErrors.User.AffiliateTermsRequired,
            User.RegisterAffiliate("sana@example.com", "Secret123", "Sana", "22123456", p => p, "DRF-45", false, Now).Error);

    [Fact]
    public void Block_ThenUnblock_RestoresActiveStatus()
    {
        var user = NewUser();

        Assert.True(user.Block("  fake orders  ", Now).IsSuccess);
        Assert.Equal(UserStatus.Blocked, user.Status);
        Assert.Equal("fake orders", user.BlockedReason);

        user.Unblock();
        Assert.True(user.IsActive);
        Assert.Null(user.BlockedReason);
    }

    [Fact]
    public void Block_WithoutReason_Fails() =>
        Assert.Equal(DomainErrors.User.BlockReasonRequired, NewUser().Block("  ", Now).Error);

    [Fact]
    public void Block_Admin_Fails()
    {
        var user = NewUser();
        user.GrantRole(Role.Admin);

        Assert.Equal(DomainErrors.User.CannotBlockAdmin, user.Block("x", Now).Error);
    }

    [Fact]
    public void AffiliateCode_DisableAndEnable_DecideWhetherItEarns()
    {
        var user = NewAffiliate();
        Assert.True(user.CanEarnAsAffiliate);

        Assert.True(user.DisableAffiliateCode(" Faux clients ", Now).IsSuccess);
        Assert.False(user.CanEarnAsAffiliate);
        Assert.Equal(Now, user.AffiliateCodeDisabledAt);
        Assert.Equal("Faux clients", user.AffiliateCodeDisabledReason);
        Assert.Equal("DRF-45", user.AffiliateCode); // the code itself never changes

        user.EnableAffiliateCode();
        Assert.True(user.CanEarnAsAffiliate);
        Assert.Null(user.AffiliateCodeDisabledReason);
    }

    [Fact]
    public void AffiliateCode_DisableNeedsAReason_AndACode()
    {
        Assert.Equal(DomainErrors.User.AffiliateCodeReasonRequired, NewAffiliate().DisableAffiliateCode("  ", Now).Error);
        Assert.Equal(DomainErrors.User.NotAffiliate, NewUser().DisableAffiliateCode("Fraude", Now).Error);
    }

    [Fact]
    public void CanEarnAsAffiliate_NeedsTheRoleAndAnActiveAccount()
    {
        Assert.False(NewUser().CanEarnAsAffiliate);

        var revoked = NewAffiliate();
        revoked.GrantRole(Role.Buyer);
        revoked.RevokeRole(Role.Affiliate);
        Assert.False(revoked.CanEarnAsAffiliate);

        var blocked = NewAffiliate();
        blocked.Block("Fraude", Now);
        Assert.False(blocked.CanEarnAsAffiliate);
    }
}
