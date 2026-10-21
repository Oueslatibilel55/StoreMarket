using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Settings;

namespace WebApp.UnitTests;

public sealed class PlatformSettingTests
{
    private static readonly Guid AdminId = Guid.CreateVersion7();
    private static SettingDefinition Def(string key) => PlatformSettingCatalog.Find(key)!;

    [Theory]
    [InlineData(SettingKeys.PayoutMinimumCoins, " 100 ", "100")]
    [InlineData(SettingKeys.PayoutMinimumCoins, "80.50", "80.5")]
    [InlineData(SettingKeys.CoinValueInTnd, "0.125", "0.125")]
    [InlineData(SettingKeys.BillingGraceDays, "7", "7")]
    public void Validate_ReturnsCanonicalValue(string key, string raw, string expected) =>
        Assert.Equal(expected, Def(key).Validate(raw).Value);

    [Theory]
    [InlineData(SettingKeys.BillingGraceDays, "2.5", DomainErrors.Setting.IntegerRequired)]
    [InlineData(SettingKeys.BillingGraceDays, "abc", DomainErrors.Setting.IntegerRequired)]
    [InlineData(SettingKeys.CoinValueInTnd, "1,5", DomainErrors.Setting.NumberRequired)]
    [InlineData(SettingKeys.CoinValueInTnd, "0.0001", DomainErrors.Setting.NumberRequired)]
    [InlineData(SettingKeys.CoinValueInTnd, "0", DomainErrors.Setting.OutOfRange)]
    [InlineData(SettingKeys.CommissionInvoiceDueDay, "29", DomainErrors.Setting.OutOfRange)]
    [InlineData(SettingKeys.AffiliateCodeLetters, "6", DomainErrors.Setting.OutOfRange)]
    [InlineData(SettingKeys.PayoutMinimumCoins, "-1", DomainErrors.Setting.OutOfRange)]
    [InlineData(SettingKeys.PayoutMinimumCoins, " ", DomainErrors.Setting.ValueRequired)]
    [InlineData(SettingKeys.Currency, "EUR", DomainErrors.Setting.ReadOnly)]
    public void Validate_RejectsBadValues(string key, string raw, string error) =>
        Assert.Equal(error, Def(key).Validate(raw).Error);

    [Fact]
    public void Catalog_DefaultsAreValid_AndMatchTheDecisions()
    {
        foreach (var d in PlatformSettingCatalog.All.Where(d => d.Editable))
            Assert.Equal(d.DefaultValue, d.Validate(d.DefaultValue).Value);

        Assert.Equal("80", Def(SettingKeys.PayoutMinimumCoins).DefaultValue);
        Assert.Equal("3", Def(SettingKeys.BillingGraceDays).DefaultValue);
        Assert.Equal("10", Def(SettingKeys.CommissionInvoiceDueDay).DefaultValue);
        Assert.Equal("30", Def(SettingKeys.SubscriptionDefaultMonthlyPrice).DefaultValue);
        Assert.Equal("50", Def(SettingKeys.AffiliatePercentMax).DefaultValue);
    }

    [Fact]
    public void Change_UpdatesValueAndAuthor_SameValueIsNoOp()
    {
        var setting = PlatformSetting.Create(Def(SettingKeys.BillingGraceDays), "5", AdminId).Value;
        var other = Guid.CreateVersion7();

        Assert.True(setting.Change(Def(SettingKeys.BillingGraceDays), "5", other).IsSuccess);
        Assert.Equal(AdminId, setting.UpdatedById);

        Assert.True(setting.Change(Def(SettingKeys.BillingGraceDays), "7", other).IsSuccess);
        Assert.Equal("7", setting.Value);
        Assert.Equal(other, setting.UpdatedById);
    }

    [Fact]
    public void Change_InvalidValue_KeepsOldOne()
    {
        var setting = PlatformSetting.Create(Def(SettingKeys.BillingGraceDays), "5", AdminId).Value;

        Assert.Equal(DomainErrors.Setting.OutOfRange, setting.Change(Def(SettingKeys.BillingGraceDays), "99", AdminId).Error);
        Assert.Equal("5", setting.Value);
    }
}
