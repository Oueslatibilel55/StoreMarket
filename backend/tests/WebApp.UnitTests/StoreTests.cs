using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.UnitTests;

public sealed class StoreTests
{
    private static readonly Guid OwnerId = Guid.CreateVersion7();
    private static readonly Guid CategoryId = Guid.CreateVersion7();

    private static StoreDetails Details(Func<StoreDetails, StoreDetails>? change = null)
    {
        var details = new StoreDetails("  Parfumerie Yasmine ", null, CategoryId, "Parfums orientaux", "22 145 678", "sousse",
            "Avenue Habib Bourguiba", null, null, 10, 7.5m, ["sousse", "monastir", "sousse"], 1, 3,
            "https://facebook.com/parfumerie.yasmine", "https://www.instagram.com/yasmine", null);
        return change is null ? details : change(details);
    }

    private static Result<Store> Create(StoreDetails details) => Store.Create(OwnerId, details, 0, 50, 30m, new DateOnly(2026, 11, 22));

    [Fact]
    public void Create_NormalizesAndIsActive()
    {
        var store = Create(Details()).Value;

        Assert.Equal("Parfumerie Yasmine", store.Name);
        Assert.Equal("parfumerie-yasmine", store.Slug);
        Assert.Equal("+21622145678", store.Phone);
        Assert.Equal(StoreStatus.Active, store.Status);
        Assert.Equal(["sousse", "monastir"], store.DeliveryCities); // de-duplicated, governorate order
        Assert.Equal(30m, store.SubscriptionMonthlyPrice);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    public void Create_PercentOutsideThePlatformLimits_Fails(int percent) =>
        Assert.Equal(DomainErrors.Store.PercentOutOfRange, Create(Details(d => d with { DefaultAffiliatePercent = percent })).Error);

    [Fact]
    public void Create_RejectsBadInput()
    {
        Assert.Equal(DomainErrors.Store.NameRequired, Create(Details(d => d with { Name = " " })).Error);
        Assert.Equal(DomainErrors.Store.NameLength, Create(Details(d => d with { Name = "A" })).Error);
        Assert.Equal(DomainErrors.Store.PhoneInvalid, Create(Details(d => d with { Phone = "123" })).Error);
        Assert.Equal(DomainErrors.Store.CityInvalid, Create(Details(d => d with { City = "paris" })).Error);
        Assert.Equal(DomainErrors.Store.CityInvalid, Create(Details(d => d with { DeliveryCities = ["tunis", "lyon"] })).Error);
        Assert.Equal(DomainErrors.Store.DeliveryCitiesRequired, Create(Details(d => d with { DeliveryCities = [] })).Error);
        Assert.Equal(DomainErrors.Store.DeliveryFeeInvalid, Create(Details(d => d with { DeliveryFee = -1m })).Error);
        Assert.Equal(DomainErrors.Store.DeliveryFeeInvalid, Create(Details(d => d with { DeliveryFee = 1.0001m })).Error);
        Assert.Equal(DomainErrors.Store.DeliveryDelayInvalid, Create(Details(d => d with { DeliveryMinDays = 5, DeliveryMaxDays = 2 })).Error);
        Assert.Equal(DomainErrors.Store.SlugInvalid, Create(Details(d => d with { Slug = "my store" })).Error);
    }

    [Theory]
    [InlineData("https://evil.example/facebook.com", null)]
    [InlineData(null, "https://facebook.com/not-instagram")]
    [InlineData("javascript:alert(1)", null)]
    public void Create_RejectsLinksToTheWrongSite(string? facebook, string? instagram) =>
        Assert.Equal(DomainErrors.Store.LinkInvalid,
            Create(Details(d => d with { FacebookUrl = facebook, InstagramUrl = instagram })).Error);

    [Fact]
    public void Update_InvalidInput_KeepsOldValues()
    {
        var store = Create(Details()).Value;

        Assert.False(store.Update(Details(d => d with { Name = "" }), 0, 50).IsSuccess);
        Assert.Equal("Parfumerie Yasmine", store.Name);
    }

    [Fact]
    public void Owner_ClosesAndReopens()
    {
        var store = Create(Details()).Value;

        Assert.True(store.Close().IsSuccess);
        Assert.Equal(StoreStatus.Closed, store.Status);
        Assert.False(store.IsOpen);
        Assert.True(store.Reopen().IsSuccess);
        Assert.True(store.IsOpen);
    }

    [Fact]
    public void Admin_Disable_NeedsAReason_AndOwnerCannotReopen()
    {
        var store = Create(Details()).Value;

        Assert.Equal(DomainErrors.Moderation.ReasonRequired, store.Disable("  ", DateTime.UtcNow).Error);
        Assert.Equal(DomainErrors.Moderation.ReasonRequired, store.Disable(new string('x', 501), DateTime.UtcNow).Error);

        Assert.True(store.Disable(" Faux magasin ", DateTime.UtcNow).IsSuccess);
        Assert.Equal(StoreStatus.Disabled, store.Status);
        Assert.Equal("Faux magasin", store.ModerationReason);
        Assert.Equal(DomainErrors.Store.NotOwnerControlled, store.Reopen().Error);
        Assert.Equal(DomainErrors.Store.NotOwnerControlled, store.Close().Error);

        Assert.True(store.Enable().IsSuccess);
        Assert.Equal(StoreStatus.Active, store.Status);
        Assert.Null(store.ModerationReason);
    }

    [Fact]
    public void Admin_Delete_IsFinal()
    {
        var store = Create(Details()).Value;

        Assert.True(store.Delete("Arnaque", DateTime.UtcNow).IsSuccess);

        Assert.True(store.IsDeleted);
        Assert.Equal(StoreStatus.Disabled, store.Status);
        Assert.Equal(DomainErrors.Store.Deleted, store.Enable().Error);
        Assert.Equal(DomainErrors.Store.Deleted, store.Disable("x", DateTime.UtcNow).Error);
        Assert.Equal(DomainErrors.Store.Deleted, store.Update(Details(), 0, 50).Error);
    }
}
