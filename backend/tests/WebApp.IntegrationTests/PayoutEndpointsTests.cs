using System.Net;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Application.DTOs;
using WebApp.Application.Interfaces;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>Payout requests (021): request → verify → schedule → paid, coins locked and released through the ledger.</summary>
public sealed class PayoutEndpointsTests(ApiFactory factory)
{
    private const string Rib = "0812 3456 7890 1234 5678";

    private sealed record World(HttpClient Admin, HttpClient Affiliate, UserDto AffiliateUser) : IDisposable
    {
        public void Dispose()
        {
            Admin.Dispose();
            Affiliate.Dispose();
        }
    }

    /// <summary>An affiliate with <paramref name="coins"/> coins (given by an admin adjustment).</summary>
    private async Task<World> Arrange(decimal coins = 200m)
    {
        var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);
        var affiliate = factory.CreateClient();
        var auth = await affiliate.RegisterAffiliate();
        affiliate.WithToken(auth.AccessToken);
        if (coins > 0)
            (await admin.Post("/api/admin/coins/adjustments", new { affiliateId = auth.User.Id, coins, reason = "Solde de test" })).EnsureSuccessStatusCode();
        return new World(admin, affiliate, auth.User);
    }

    private static async Task<T> Get<T>(HttpClient client, string url) =>
        await (await client.GetAsync(new Uri(url, UriKind.Relative), Ct)).Read<T>();

    private static Task<HttpResponseMessage> AskCash(World w, decimal coins) =>
        w.Affiliate.Post("/api/me/payouts", new { coins, method = "Cash", note = "Le samedi si possible" });

    private async Task AssertBalanceMatchesLedger(Guid affiliateId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var (cached, sum) = await scope.ServiceProvider.GetRequiredService<ICoinQueries>().CheckBalanceAsync(affiliateId, Ct);
        Assert.Equal(sum, cached);
    }

    [Fact]
    public async Task Request_ChecksMinimumAndBalance_LocksCoins_OneOpenAtATime()
    {
        using var w = await Arrange(200m);

        var mine = await Get<MyPayoutsDto>(w.Affiliate, "/api/me/payouts");
        Assert.Equal((200m, 80m), (mine.Available, mine.Minimum));
        Assert.Empty(mine.Items);

        Assert.Equal("payout.belowMinimum", await (await AskCash(w, 50m)).ProblemCode());
        Assert.Equal("payout.notEnoughCoins", await (await AskCash(w, 250m)).ProblemCode());

        var created = await AskCash(w, 120m);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var payout = await created.Read<PayoutDto>();
        Assert.Matches(@"^PAY-\d{4}-\d{6}$", payout.Reference);
        Assert.Equal((PayoutStatus.Requested, 120m, 120m), (payout.Status, payout.Coins, payout.AmountTnd)); // 1 coin = 1 TND

        var coins = await Get<CoinSummaryDto>(w.Affiliate, "/api/me/coins");
        Assert.Equal((80m, 120m, 0m), (coins.Balance, coins.Locked, coins.PaidOut));
        Assert.Equal("payout.alreadyOpen", await (await AskCash(w, 80m)).ProblemCode());

        var entries = await Get<PagedResult<CoinEntryDto>>(w.Affiliate, "/api/me/coins/entries?type=PayoutLocked");
        Assert.Equal(-120m, Assert.Single(entries.Items).Coins);
        await AssertBalanceMatchesLedger(w.AffiliateUser.Id);
    }

    [Fact]
    public async Task Admin_Verifies_Schedules_MarksPaid_Audited()
    {
        using var w = await Arrange(200m);
        var payout = await (await AskCash(w, 150m)).Read<PayoutDto>();
        var url = $"/api/admin/payouts/{payout.Id}";

        Assert.Equal("payout.transitionNotAllowed", await (await w.Admin.Post($"{url}/paid", new { reference = "x" })).ProblemCode());
        (await w.Admin.Post($"{url}/verify", new { note = "Commandes livrées, OK" })).EnsureSuccessStatusCode();
        var when = DateTime.UtcNow.Date.AddDays(2).AddHours(10);
        Assert.Equal("payout.placeRequired", await (await w.Admin.Post($"{url}/schedule", new { scheduledFor = when })).ProblemCode());
        var scheduled = await (await w.Admin.Post($"{url}/schedule", new { scheduledFor = when, place = "Bureau Lac 2" })).Read<PayoutDto>();
        Assert.Equal((PayoutStatus.Scheduled, "Bureau Lac 2"), (scheduled.Status, scheduled.Place));

        // Scheduled: the affiliate can no longer cancel.
        Assert.Equal(HttpStatusCode.Conflict, (await w.Affiliate.Post($"/api/me/payouts/{payout.Id}/cancel")).StatusCode);

        var paid = await (await w.Admin.Post($"{url}/paid", new { reference = "Remis par Karim" })).Read<PayoutDto>();
        Assert.Equal((PayoutStatus.Paid, "Remis par Karim"), (paid.Status, paid.PaidReference));
        Assert.Equal([PayoutStatus.Requested, PayoutStatus.Verified, PayoutStatus.Scheduled, PayoutStatus.Paid], paid.History.Select(h => h.Status));
        Assert.All(paid.History.Skip(1), h => Assert.False(string.IsNullOrEmpty(h.ActorName))); // admin view names who did it

        // The affiliate follows it, without the admins' names.
        var mine = Assert.Single((await Get<MyPayoutsDto>(w.Affiliate, "/api/me/payouts")).Items);
        Assert.Equal(PayoutStatus.Paid, mine.Status);
        Assert.Equal(when, mine.ScheduledFor);
        Assert.All(mine.History, h => Assert.Null(h.ActorName));

        var coins = await Get<CoinSummaryDto>(w.Affiliate, "/api/me/coins");
        Assert.Equal((50m, 0m, 150m), (coins.Balance, coins.Locked, coins.PaidOut));

        var audit = await Get<PagedResult<AuditEntryDto>>(w.Admin, $"/api/admin/audit?targetId={payout.Id}");
        Assert.Equal(["payout.paid", "payout.schedule", "payout.verify"], audit.Items.Select(e => e.Action).Take(3));
        await AssertBalanceMatchesLedger(w.AffiliateUser.Id);
    }

    [Fact]
    public async Task RejectOrCancel_ReleaseTheCoins_ThenTheAffiliateCanAskAgain()
    {
        using var w = await Arrange(200m);
        var first = await (await AskCash(w, 100m)).Read<PayoutDto>();
        Assert.Equal("payout.reasonRequired", await (await w.Admin.Post($"/api/admin/payouts/{first.Id}/reject", new { reason = " " })).ProblemCode());
        var rejected = await (await w.Admin.Post($"/api/admin/payouts/{first.Id}/reject", new { reason = "Commandes à revérifier" })).Read<PayoutDto>();
        Assert.Equal((PayoutStatus.Rejected, "Commandes à revérifier"), (rejected.Status, rejected.RejectionReason));
        Assert.Equal(200m, (await Get<CoinSummaryDto>(w.Affiliate, "/api/me/coins")).Balance);

        var second = await (await AskCash(w, 90m)).Read<PayoutDto>();
        using (var other = factory.CreateClient())
        {
            other.WithToken((await other.RegisterAffiliate()).AccessToken);
            Assert.Equal(HttpStatusCode.NotFound, (await other.Post($"/api/me/payouts/{second.Id}/cancel")).StatusCode);
        }
        var cancelled = await (await w.Affiliate.Post($"/api/me/payouts/{second.Id}/cancel")).Read<PayoutDto>();
        Assert.Equal(PayoutStatus.Cancelled, cancelled.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await w.Affiliate.Post($"/api/me/payouts/{second.Id}/cancel")).StatusCode);

        var coins = await Get<CoinSummaryDto>(w.Affiliate, "/api/me/coins");
        Assert.Equal((200m, 0m), (coins.Balance, coins.Locked));
        var released = await Get<PagedResult<CoinEntryDto>>(w.Affiliate, "/api/me/coins/entries?type=PayoutReleased");
        Assert.Equal(2, released.Items.Count);
        await AssertBalanceMatchesLedger(w.AffiliateUser.Id);
    }

    [Fact]
    public async Task TwoRequestsAtOnce_OnlyOneLocksTheCoins()
    {
        using var w = await Arrange(100m);

        var results = await Task.WhenAll(AskCash(w, 90m), AskCash(w, 90m), AskCash(w, 90m));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(10m, (await Get<CoinSummaryDto>(w.Affiliate, "/api/me/coins")).Balance);
        await AssertBalanceMatchesLedger(w.AffiliateUser.Id);
    }

    [Fact]
    public async Task BankTransfer_NeedsValidRib_OfferedAgainNextTime()
    {
        using var w = await Arrange(300m);
        Assert.Equal("payout.bankDetailsRequired",
            await (await w.Affiliate.Post("/api/me/payouts", new { coins = 100m, method = "BankTransfer" })).ProblemCode());
        Assert.Equal("payout.ribInvalid", await (await w.Affiliate.Post("/api/me/payouts",
            new { coins = 100m, method = "BankTransfer", bankName = "BIAT", accountHolder = "Sana Trabelsi", rib = "1234" })).ProblemCode());

        var payout = await (await w.Affiliate.Post("/api/me/payouts",
            new { coins = 100m, method = "BankTransfer", bankName = "BIAT", accountHolder = "Sana Trabelsi", rib = Rib })).Read<PayoutDto>();
        Assert.Equal("08123456789012345678", payout.Rib);

        var mine = await Get<MyPayoutsDto>(w.Affiliate, "/api/me/payouts");
        Assert.Equal(new PayoutBankDetails("BIAT", "Sana Trabelsi", "08123456789012345678"), mine.LastBankDetails);

        // A transfer only needs a date.
        (await w.Admin.Post($"/api/admin/payouts/{payout.Id}/verify")).EnsureSuccessStatusCode();
        (await w.Admin.Post($"/api/admin/payouts/{payout.Id}/schedule", new { scheduledFor = DateTime.UtcNow.Date.AddDays(1) })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Admin_ListFiltersCountsAndVerificationScreen()
    {
        using var w = await Arrange(300m);
        var payout = await (await w.Affiliate.Post("/api/me/payouts",
            new { coins = 120m, method = "BankTransfer", bankName = "BIAT", accountHolder = "Sana Trabelsi", rib = Rib })).Read<PayoutDto>();

        var byRef = await Get<PagedResult<PayoutListItem>>(w.Admin, $"/api/admin/payouts?search={payout.Reference}");
        Assert.Equal(payout.Id, Assert.Single(byRef.Items).Id);
        Assert.Contains((await Get<PagedResult<PayoutListItem>>(w.Admin, "/api/admin/payouts?status=Requested&method=BankTransfer&pageSize=100")).Items,
            p => p.Id == payout.Id);
        Assert.DoesNotContain((await Get<PagedResult<PayoutListItem>>(w.Admin, "/api/admin/payouts?method=Cash&pageSize=100")).Items, p => p.Id == payout.Id);

        var counts = await Get<AdminPayoutCounts>(w.Admin, "/api/admin/payouts/counts");
        Assert.True(counts.Requested >= 1 && counts.LockedCoins >= 120m);

        // Planned for tomorrow: found by the date filter.
        (await w.Admin.Post($"/api/admin/payouts/{payout.Id}/verify")).EnsureSuccessStatusCode();
        var tomorrow = DateTime.UtcNow.Date.AddDays(1);
        (await w.Admin.Post($"/api/admin/payouts/{payout.Id}/schedule", new { scheduledFor = tomorrow })).EnsureSuccessStatusCode();
        var day = tomorrow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains((await Get<PagedResult<PayoutListItem>>(w.Admin, $"/api/admin/payouts?from={day}&to={day}&pageSize=100")).Items, p => p.Id == payout.Id);

        var detail = await Get<AdminPayoutDetail>(w.Admin, $"/api/admin/payouts/{payout.Id}");
        Assert.Equal(w.AffiliateUser.Id, detail.Affiliate.Id);
        Assert.Equal(180m, detail.Affiliate.Balance);
        Assert.Equal(300m, detail.AdjustmentsTotal); // no earned coins: all came from the adjustment
        Assert.Empty(detail.Covered);
        Assert.Equal(HttpStatusCode.NotFound, (await w.Admin.GetAsync(new Uri($"/api/admin/payouts/{Guid.CreateVersion7()}", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task OnlyAffiliatesAsk_OnlyAdminsDecide()
    {
        using var w = await Arrange(200m);
        var payout = await (await AskCash(w, 100m)).Read<PayoutDto>();

        using var buyer = factory.CreateClient();
        buyer.WithToken((await buyer.Register()).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.Post("/api/me/payouts", new { coins = 100m, method = "Cash" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Affiliate.Post($"/api/admin/payouts/{payout.Id}/verify")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Affiliate.GetAsync(new Uri("/api/admin/payouts", UriKind.Relative), Ct)).StatusCode);
    }
}
