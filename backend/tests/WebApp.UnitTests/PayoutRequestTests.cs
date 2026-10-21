using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.UnitTests;

public sealed class PayoutRequestTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AffiliateId = Guid.CreateVersion7();
    private static readonly Guid AdminId = Guid.CreateVersion7();
    private const string Rib = "0812 3456 7890 1234 5678";

    private static PayoutRequest Cash(decimal coins = 100m) =>
        PayoutRequest.Create(12, AffiliateId, coins, 1.5m, PayoutMethod.Cash, null, null, null, "  Merci  ", Now).Value;

    private static PayoutRequest Transfer() =>
        PayoutRequest.Create(13, AffiliateId, 90m, 1m, PayoutMethod.BankTransfer, " BIAT ", " Sana Trabelsi ", Rib, null, Now).Value;

    private static PayoutRequest Verified(PayoutRequest p)
    {
        Assert.True(p.Verify(AdminId, null, Now).IsSuccess);
        return p;
    }

    private static PayoutRequest Scheduled(PayoutRequest p)
    {
        Assert.True(Verified(p).Schedule(AdminId, Now.AddDays(2), "Bureau Lac 2, 10 h", Now).IsSuccess);
        return p;
    }

    [Fact]
    public void Create_FreezesAmount_Reference_AndHistory()
    {
        var p = Cash();

        Assert.Equal("PAY-2026-000012", p.Reference);
        Assert.Equal(PayoutStatus.Requested, p.Status);
        Assert.Equal(150m, p.AmountTnd); // 100 × 1.5, frozen at request time
        Assert.Equal("Merci", p.Note);
        Assert.Null(p.Rib); // cash keeps no bank details
        Assert.True(p.IsOpen);
        var step = Assert.Single(p.History);
        Assert.Equal((PayoutStatus.Requested, AffiliateId), (step.Status, step.ActorId));
    }

    [Fact]
    public void Transfer_NeedsBankDetails_AndA20DigitRib()
    {
        var p = Transfer();
        Assert.Equal("08123456789012345678", p.Rib);
        Assert.Equal(("BIAT", "Sana Trabelsi"), (p.BankName, p.AccountHolder));

        Assert.Equal(DomainErrors.Payout.BankDetailsRequired,
            PayoutRequest.Create(1, AffiliateId, 90m, 1m, PayoutMethod.BankTransfer, "BIAT", " ", Rib, null, Now).Error);
        Assert.Equal(DomainErrors.Payout.RibInvalid,
            PayoutRequest.Create(1, AffiliateId, 90m, 1m, PayoutMethod.BankTransfer, "BIAT", "Sana", "0812 3456", null, Now).Error);
        Assert.Equal(DomainErrors.Payout.RibInvalid,
            PayoutRequest.Create(1, AffiliateId, 90m, 1m, PayoutMethod.BankTransfer, "BIAT", "Sana", "0812345678901234567X", null, Now).Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.0005)]
    public void Create_RefusesInvalidCoins(decimal coins) =>
        Assert.Equal(DomainErrors.Payout.CoinsInvalid,
            PayoutRequest.Create(1, AffiliateId, coins, 1m, PayoutMethod.Cash, null, null, null, null, Now).Error);

    [Fact]
    public void Create_RefusesUnknownMethod() =>
        Assert.Equal(DomainErrors.Payout.MethodInvalid,
            PayoutRequest.Create(1, AffiliateId, 90m, 1m, (PayoutMethod)9, null, null, null, null, Now).Error);

    [Fact]
    public void HappyPath_Requested_Verified_Scheduled_Paid()
    {
        var p = Scheduled(Cash());
        Assert.Equal(PayoutStatus.Scheduled, p.Status);
        Assert.Equal("Bureau Lac 2, 10 h", p.Place);

        Assert.True(p.MarkPaid(AdminId, null, " Remis par Karim ", Now).IsSuccess);

        Assert.Equal(PayoutStatus.Paid, p.Status);
        Assert.Equal((Now, "Remis par Karim"), (p.PaidAt, p.PaidReference));
        Assert.False(p.IsOpen);
        Assert.Equal([PayoutStatus.Requested, PayoutStatus.Verified, PayoutStatus.Scheduled, PayoutStatus.Paid], p.History.Select(h => h.Status));
    }

    [Fact]
    public void Schedule_CashNeedsPlace_TransferDoesNot_DateNotInPast()
    {
        var cash = Verified(Cash());
        Assert.Equal(DomainErrors.Payout.PlaceRequired, cash.Schedule(AdminId, Now.AddDays(1), " ", Now).Error);
        Assert.Equal(DomainErrors.Payout.DateRequired, cash.Schedule(AdminId, null, "Bureau", Now).Error);
        Assert.Equal(DomainErrors.Payout.DateInPast, cash.Schedule(AdminId, Now.AddDays(-1), "Bureau", Now).Error);

        var transfer = Verified(Transfer());
        Assert.True(transfer.Schedule(AdminId, Now.Date, "ignored", Now).IsSuccess); // today is fine
        Assert.Null(transfer.Place);

        // A scheduled request can be moved to another date.
        Assert.True(transfer.Schedule(AdminId, Now.AddDays(3), null, Now).IsSuccess);
        Assert.Equal(Now.AddDays(3), transfer.ScheduledFor);
    }

    [Fact]
    public void Steps_OutOfOrder_AreRefused()
    {
        var p = Cash();
        Assert.Equal(DomainErrors.Payout.TransitionNotAllowed, p.Schedule(AdminId, Now.AddDays(1), "Bureau", Now).Error); // not verified
        Assert.Equal(DomainErrors.Payout.TransitionNotAllowed, p.MarkPaid(AdminId, null, "x", Now).Error);              // not scheduled

        var scheduled = Scheduled(Cash());
        Assert.Equal(DomainErrors.Payout.TransitionNotAllowed, scheduled.Reject(AdminId, "trop tard", Now).Error);
        Assert.Equal(DomainErrors.Payout.TransitionNotAllowed, scheduled.Cancel(AffiliateId, Now).Error);
        Assert.Equal(DomainErrors.Payout.TransitionNotAllowed, scheduled.Verify(AdminId, null, Now).Error);
    }

    [Fact]
    public void MarkPaid_NeedsReference_NotInFuture()
    {
        var p = Scheduled(Cash());
        Assert.Equal(DomainErrors.Payout.PaidReferenceRequired, p.MarkPaid(AdminId, null, "  ", Now).Error);
        Assert.Equal(DomainErrors.Payout.PaidInFuture, p.MarkPaid(AdminId, Now.AddDays(1), "VIR-889", Now).Error);
        Assert.True(p.MarkPaid(AdminId, Now.AddHours(-3), "VIR-889", Now).IsSuccess);
        Assert.Equal(Now.AddHours(-3), p.PaidAt);
    }

    [Fact]
    public void Reject_NeedsReason_FromRequestedOrVerified()
    {
        var p = Cash();
        Assert.Equal(DomainErrors.Payout.ReasonRequired, p.Reject(AdminId, " ", Now).Error);
        Assert.True(Verified(p).Reject(AdminId, " Commandes suspectes ", Now).IsSuccess);
        Assert.Equal((PayoutStatus.Rejected, "Commandes suspectes"), (p.Status, p.RejectionReason));
    }

    [Fact]
    public void Cancel_OnlyByTheAffiliate_BeforeScheduled()
    {
        var p = Verified(Cash());
        Assert.Equal(DomainErrors.Payout.TransitionNotAllowed, p.Cancel(Guid.CreateVersion7(), Now).Error);
        Assert.True(p.Cancel(AffiliateId, Now).IsSuccess);
        Assert.Equal(PayoutStatus.Cancelled, p.Status);
    }

    [Fact]
    public void LedgerEntries_LockNegative_ReleaseOnlyWhenEnded()
    {
        var p = Cash(100m);
        var locked = CoinEntry.PayoutLocked(p);
        Assert.Equal((CoinEntryType.PayoutLocked, -100m, p.Id), (locked.Type, locked.Coins, locked.PayoutRequestId));
        Assert.Throws<InvalidOperationException>(() => CoinEntry.PayoutReleased(p)); // still open

        p.Reject(AdminId, "Doublon", Now);
        var released = CoinEntry.PayoutReleased(p);
        Assert.Equal((CoinEntryType.PayoutReleased, 100m, "Doublon"), (released.Type, released.Coins, released.Reason));
    }
}
