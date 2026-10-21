using System.Net;
using System.Net.Http.Json;
using WebApp.Application.DTOs;
using WebApp.Domain.Settings;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>
/// Settings are shared by every test of the assembly: each test that changes one puts the
/// default back, and only changes values no other test depends on.
/// </summary>
public sealed class SettingsEndpointsTests(ApiFactory factory)
{
    private async Task<HttpClient> AdminClient()
    {
        var client = factory.CreateClient();
        client.WithToken((await client.SignInAdmin()).AccessToken);
        return client;
    }

    private static Task<HttpResponseMessage> Put(HttpClient client, string key, string value, string? reason = null) =>
        client.PutAsJsonAsync($"/api/admin/settings/{key}", new { value, reason }, Ct);

    [Fact]
    public async Task PublicValues_ListEveryKeyWithItsDefault()
    {
        using var client = factory.CreateClient();

        var values = await (await client.GetAsync(new Uri("/api/settings", UriKind.Relative), Ct)).Read<Dictionary<string, string>>();

        Assert.Equal(PlatformSettingCatalog.All.Count, values.Count);
        Assert.Equal("TND", values[SettingKeys.Currency]);
        Assert.Equal("1", values[SettingKeys.CoinValueInTnd]);
    }

    [Fact]
    public async Task Admin_ChangesSetting_ValueIsServedAndAudited()
    {
        using var admin = await AdminClient();

        var response = await Put(admin, SettingKeys.RankingMinReviewsPrior, "25", "More reviews before trusting a rating");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Read<AdminSettingDto>();
        Assert.Equal("25", updated.Value);
        Assert.Equal(AdminEmail, updated.UpdatedByEmail);

        // The cache was dropped: the public read sees the new value at once.
        var values = await (await admin.GetAsync(new Uri("/api/settings", UriKind.Relative), Ct)).Read<Dictionary<string, string>>();
        Assert.Equal("25", values[SettingKeys.RankingMinReviewsPrior]);

        var audit = await (await admin.GetAsync(new Uri("/api/admin/audit?action=setting.update&targetType=Setting", UriKind.Relative), Ct))
            .Read<PagedResult<AuditEntryDto>>();
        var entry = Assert.Single(audit.Items, e => e.TargetLabel == SettingKeys.RankingMinReviewsPrior && e.After!.Contains("25", StringComparison.Ordinal));
        Assert.Contains("10", entry.Before!, StringComparison.Ordinal);
        Assert.Equal("More reviews before trusting a rating", entry.Reason);

        await Put(admin, SettingKeys.RankingMinReviewsPrior, "10");
    }

    [Fact]
    public async Task SameValue_IsNotAudited()
    {
        using var admin = await AdminClient();

        await Put(admin, SettingKeys.BillingGraceDays, "3"); // already the default

        var audit = await (await admin.GetAsync(new Uri("/api/admin/audit?action=setting.update", UriKind.Relative), Ct))
            .Read<PagedResult<AuditEntryDto>>();
        Assert.DoesNotContain(audit.Items, e => e.TargetLabel == SettingKeys.BillingGraceDays);
    }

    [Theory]
    [InlineData(SettingKeys.BillingGraceDays, "2.5", "setting.integerRequired")]
    [InlineData(SettingKeys.CommissionInvoiceDueDay, "31", "setting.outOfRange")]
    [InlineData(SettingKeys.Currency, "EUR", "setting.readOnly")]
    [InlineData(SettingKeys.AffiliatePercentMin, "60", "setting.percentMinAboveMax")]
    public async Task InvalidValue_Returns400(string key, string value, string code)
    {
        using var admin = await AdminClient();

        var response = await Put(admin, key, value);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await response.ProblemCode());
    }

    [Fact]
    public async Task UnknownKey_Returns404()
    {
        using var admin = await AdminClient();

        var response = await Put(admin, "nope", "1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("setting.unknown", await response.ProblemCode());
    }

    [Fact]
    public async Task AffiliateCodeShape_AppliesToNewCodes_OldCodesStayValid()
    {
        using var admin = await AdminClient();
        using var oldAffiliate = factory.CreateClient();
        var old = await oldAffiliate.RegisterAffiliate();

        await Put(admin, SettingKeys.AffiliateCodeLetters, "4");
        await Put(admin, SettingKeys.AffiliateCodeMinDigits, "4");
        try
        {
            using var newAffiliate = factory.CreateClient();
            Assert.Matches("^[A-Z]{4}-[0-9]{4,6}$", (await newAffiliate.RegisterAffiliate()).User.AffiliateCode);

            // The affiliate created before the change keeps a working code.
            var refreshed = await (await oldAffiliate.Post("/api/auth/refresh")).Read<AuthResponse>();
            Assert.Equal(old.User.AffiliateCode, refreshed.User.AffiliateCode);
            oldAffiliate.WithToken(refreshed.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await oldAffiliate.GetAsync(new Uri("/api/me/affiliate", UriKind.Relative), Ct)).StatusCode);
        }
        finally
        {
            await Put(admin, SettingKeys.AffiliateCodeLetters, "3");
            await Put(admin, SettingKeys.AffiliateCodeMinDigits, "2");
        }
    }

    [Fact]
    public async Task AdminList_ShowsDefinitions_AndIsAdminOnly()
    {
        using var admin = await AdminClient();
        var list = await (await admin.GetAsync(new Uri("/api/admin/settings", UriKind.Relative), Ct)).Read<List<AdminSettingDto>>();
        var payout = Assert.Single(list, s => s.Key == SettingKeys.PayoutMinimumCoins);
        Assert.Equal("80", payout.DefaultValue);
        Assert.Equal(SettingType.Number, payout.Type);
        Assert.False(Assert.Single(list, s => s.Key == SettingKeys.Currency).Editable);

        using var buyer = factory.CreateClient();
        buyer.WithToken((await buyer.Register()).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync(new Uri("/api/admin/settings", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Put(buyer, SettingKeys.BillingGraceDays, "5")).StatusCode);
    }
}
