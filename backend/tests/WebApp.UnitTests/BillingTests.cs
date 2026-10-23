using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.UnitTests;

public sealed class BillingTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);
    private static readonly DateTime Now = new(2026, 10, 23, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AdminId = Guid.CreateVersion7();
    private static readonly Guid OwnerId = Guid.CreateVersion7();
    private static readonly Guid CategoryId = Guid.CreateVersion7();

    private static Store NewStore(DateOnly paidUntil, decimal price = 30m) =>
        Store.Create(OwnerId, new StoreDetails("Parfumerie Yasmine", null, CategoryId, null, "22 145 678", "sousse", null, null, null,
            10, 7m, ["sousse"], 1, 3, null, null, null), 0, 50, price, paidUntil).Value;

    private static OfferDetails Offer(Func<OfferDetails, OfferDetails>? change = null)
    {
        var d = new OfferDetails("Trimestriel", null, null, 3, 20, null, null, null, OfferTarget.AllStores, null, false, true);
        return change is null ? d : change(d);
    }

    private static readonly PaymentDetails Cash = new(StorePaymentMethod.Cash, null, null, Today, null);

    private static CommissionLineInput Line(decimal coins, CoinEntryType type = CoinEntryType.Earned, decimal coinValue = 1m) =>
        new(Guid.CreateVersion7(), type, Now, "CMD-1", "Musc", 2, 100m, 10, "DRF-45", coins, coinValue);

    // -- Store -------------------------------------------------------------------

    [Fact]
    public void ExtendSubscription_FromPaidUntil_OrFromTodayWhenLapsed()
    {
        var store = NewStore(new DateOnly(2026, 10, 31));
        Assert.Equal((new DateOnly(2026, 11, 1), new DateOnly(2027, 1, 31)), store.ExtendSubscription(3, Today));
        Assert.Equal(new DateOnly(2027, 1, 31), store.PaidUntil);

        var lapsed = NewStore(new DateOnly(2026, 8, 31));
        Assert.Equal((Today, new DateOnly(2026, 11, 22)), lapsed.ExtendSubscription(1, Today)); // the unpaid gap is not charged
    }

    [Fact]
    public void Suspend_OnlyActiveOrClosed_ReactivateGoesBackToActive()
    {
        var store = NewStore(Today);
        Assert.True(store.Suspend(Now));
        Assert.Equal(StoreStatus.Suspended, store.Status);
        Assert.False(store.Suspend(Now)); // already
        Assert.False(store.Close().IsSuccess); // the owner can't reopen / close a suspended store
        Assert.True(store.Reactivate());
        Assert.Equal((StoreStatus.Active, (DateTime?)null), (store.Status, store.SuspendedAt));

        var disabled = NewStore(Today);
        disabled.Disable("Fausse boutique", Now);
        Assert.False(disabled.Suspend(Now)); // an admin decision wins
        Assert.False(disabled.Reactivate());
    }

    [Fact]
    public void SubscriptionPrice_Validated()
    {
        var store = NewStore(Today);
        Assert.True(store.SetSubscriptionPrice(45.5m).IsSuccess);
        Assert.Equal(45.5m, store.SubscriptionMonthlyPrice);
        Assert.Equal(DomainErrors.Billing.PriceInvalid, store.SetSubscriptionPrice(-1).Error);
        Assert.Equal(DomainErrors.Billing.PriceInvalid, store.SetSubscriptionPrice(1.0001m).Error);
        Assert.True(store.SetSubscriptionPrice(0).IsSuccess); // free is allowed
    }

    // -- Offers ------------------------------------------------------------------

    [Fact]
    public void Offer_Rules()
    {
        Assert.True(BillingOffer.Create(Offer()).IsSuccess);
        Assert.Equal(DomainErrors.Billing.OfferNameRequired, BillingOffer.Create(Offer(d => d with { NameFr = " " })).Error);
        Assert.Equal(DomainErrors.Billing.OfferDiscountInvalid, BillingOffer.Create(Offer(d => d with { DiscountAmount = 5m })).Error); // both
        Assert.Equal(DomainErrors.Billing.OfferDiscountInvalid, BillingOffer.Create(Offer(d => d with { DiscountPercent = null })).Error); // none
        Assert.Equal(DomainErrors.Billing.OfferDiscountInvalid, BillingOffer.Create(Offer(d => d with { DiscountPercent = 120 })).Error);
        Assert.Equal(DomainErrors.Billing.OfferMonthsInvalid, BillingOffer.Create(Offer(d => d with { Months = 0 })).Error);
        Assert.Equal(DomainErrors.Billing.OfferDatesInvalid,
            BillingOffer.Create(Offer(d => d with { StartsOn = Today, EndsOn = Today.AddDays(-1) })).Error);
        Assert.Equal(DomainErrors.Billing.OfferTargetInvalid, BillingOffer.Create(Offer(d => d with { Target = OfferTarget.Stores })).Error);
    }

    [Fact]
    public void Offer_AppliesTo_MonthsDatesTargetAndFirstPayment()
    {
        var storeId = Guid.CreateVersion7();
        var quarterly = BillingOffer.Create(Offer()).Value;
        Assert.True(quarterly.AppliesTo(storeId, CategoryId, 3, false, Today));
        Assert.False(quarterly.AppliesTo(storeId, CategoryId, 1, false, Today)); // exactly 3 months

        var launch = BillingOffer.Create(Offer(d => d with
        {
            Months = 1, DiscountPercent = 100, FirstPaymentOnly = true, EndsOn = new DateOnly(2026, 12, 31),
        })).Value;
        Assert.True(launch.AppliesTo(storeId, CategoryId, 1, true, Today));
        Assert.False(launch.AppliesTo(storeId, CategoryId, 1, false, Today));
        Assert.False(launch.AppliesTo(storeId, CategoryId, 1, true, new DateOnly(2027, 1, 1)));

        var partner = BillingOffer.Create(Offer(d => d with { Months = null, Target = OfferTarget.Stores, TargetIds = [storeId] })).Value;
        Assert.True(partner.AppliesTo(storeId, CategoryId, 7, false, Today));
        Assert.False(partner.AppliesTo(Guid.CreateVersion7(), CategoryId, 7, false, Today));

        var byCategory = BillingOffer.Create(Offer(d => d with { Target = OfferTarget.StoreCategories, TargetIds = [CategoryId] })).Value;
        Assert.True(byCategory.AppliesTo(storeId, CategoryId, 3, false, Today));
        Assert.False(byCategory.AppliesTo(storeId, Guid.CreateVersion7(), 3, false, Today));

        var inactive = BillingOffer.Create(Offer(d => d with { IsActive = false })).Value;
        Assert.False(inactive.AppliesTo(storeId, CategoryId, 3, false, Today));
    }

    [Fact]
    public void Offer_Discount_PercentOrFixed_NeverMoreThanThePayment()
    {
        Assert.Equal(18m, BillingOffer.Create(Offer()).Value.DiscountOn(90m)); // 20 % of 3 × 30
        Assert.Equal(25m, BillingOffer.Create(Offer(d => d with { DiscountPercent = null, DiscountAmount = 25m })).Value.DiscountOn(90m));
        Assert.Equal(30m, BillingOffer.Create(Offer(d => d with { DiscountPercent = null, DiscountAmount = 50m })).Value.DiscountOn(30m));
    }

    // -- Payments ----------------------------------------------------------------

    [Fact]
    public void SubscriptionPayment_FreezesPriceAndOffer_DeclaredOrRecorded()
    {
        var store = NewStore(Today);
        var offer = BillingOffer.Create(Offer()).Value;

        var declared = StorePayment.ForSubscription(1, store, 3, offer, Cash, OwnerId, adminId: null, Now).Value;
        Assert.Equal((StorePaymentStatus.Declared, 72m, 18m, 30m, "Trimestriel"),
            (declared.Status, declared.Amount, declared.Discount, declared.MonthlyPrice!.Value, declared.OfferName));
        Assert.Equal("PMT-2026-000001", declared.Number);

        var recorded = StorePayment.ForSubscription(2, store, 1, null, Cash, null, AdminId, Now).Value;
        Assert.Equal((StorePaymentStatus.Approved, 30m), (recorded.Status, recorded.Amount));
        Assert.Equal(DomainErrors.Billing.MonthsInvalid, StorePayment.ForSubscription(3, store, 0, null, Cash, OwnerId, null, Now).Error);
    }

    [Fact]
    public void Payment_ApproveOrReject_OnlyWhenDeclared()
    {
        var store = NewStore(Today);
        var p = StorePayment.ForSubscription(1, store, 1, null, Cash, OwnerId, null, Now).Value;
        Assert.Equal(DomainErrors.Billing.ReasonRequired, p.Reject(AdminId, " ", Now).Error);
        Assert.True(p.Reject(AdminId, "Aucun virement reçu", Now).IsSuccess);
        Assert.Equal(DomainErrors.Billing.NotDeclared, p.Approve(AdminId, Now).Error);

        var q = StorePayment.ForSubscription(2, store, 1, null, Cash, OwnerId, null, Now).Value;
        Assert.True(q.Approve(AdminId, Now).IsSuccess);
        Assert.Equal(DomainErrors.Billing.NotDeclared, q.Reject(AdminId, "x", Now).Error);
    }

    // -- Invoices ----------------------------------------------------------------

    [Fact]
    public void Invoice_TotalsLines_DueOnTheNextMonth()
    {
        var invoice = CommissionInvoice.Issue(42, Guid.CreateVersion7(), "Yasmine", new DateOnly(2026, 10, 15),
            [Line(25m), Line(10m, coinValue: 1.5m), Line(-5m, CoinEntryType.Adjustment)], dueDay: 10).Value;

        Assert.Equal("COM-202610-000042", invoice.Reference);
        Assert.Equal(new DateOnly(2026, 10, 1), invoice.Month);
        Assert.Equal(new DateOnly(2026, 11, 10), invoice.DueOn);
        Assert.Equal(35m, invoice.Total); // 25 + 15 − 5
        Assert.Equal(CommissionInvoiceStatus.Issued, invoice.Status);
        Assert.Equal(new DateOnly(2027, 1, 28), CommissionInvoice.DueDateOf(new DateOnly(2026, 12, 1), 31)); // clamped to 28
    }

    [Fact]
    public void Invoice_NothingOrNetCredit_IsNotIssued()
    {
        Assert.Equal(DomainErrors.Billing.NothingToInvoice, CommissionInvoice.Issue(1, Guid.CreateVersion7(), "x", Today, [], 10).Error);
        Assert.Equal(DomainErrors.Billing.NothingToInvoice,
            CommissionInvoice.Issue(1, Guid.CreateVersion7(), "x", Today, [Line(5m), Line(-8m, CoinEntryType.Adjustment)], 10).Error);
    }

    [Fact]
    public void Invoice_PaidInParts_NeverMoreThanLeft()
    {
        var invoice = CommissionInvoice.Issue(1, Guid.CreateVersion7(), "x", Today, [Line(50m)], 10).Value;
        Assert.Equal(DomainErrors.Billing.AmountTooHigh, invoice.ApplyPayment(60m, Now).Error);
        Assert.True(invoice.ApplyPayment(20m, Now).IsSuccess);
        Assert.Equal((CommissionInvoiceStatus.PartiallyPaid, 30m), (invoice.Status, invoice.Remaining));
        Assert.Equal(DomainErrors.Billing.AmountTooHigh, StorePayment.ForInvoice(1, invoice, 31m, Cash, OwnerId, null, Now).Error);
        Assert.True(invoice.ApplyPayment(30m, Now).IsSuccess);
        Assert.Equal(CommissionInvoiceStatus.Paid, invoice.Status);
        Assert.Equal(DomainErrors.Billing.InvoicePaid, invoice.ApplyPayment(1m, Now).Error);
        Assert.Equal(DomainErrors.Billing.InvoicePaid, StorePayment.ForInvoice(2, invoice, 1m, Cash, OwnerId, null, Now).Error);
    }

    // -- Suspension rule ------------------------------------------------------------

    [Fact]
    public void Evaluate_GraceDaysCountDown_ThenOverdue()
    {
        var due = new DateOnly(2026, 11, 10);
        BillingState On(DateOnly day, bool declared = false) => BillingRules.Evaluate([new BillingDue(due, false, declared)], day, 3);

        Assert.Equal(new BillingState(false, null, false, false), On(due)); // due today: not late yet
        Assert.Equal(3, On(due.AddDays(1)).DaysLeft);
        Assert.Equal(1, On(due.AddDays(3)).DaysLeft);
        Assert.True(On(due.AddDays(4)).Overdue);
        Assert.Null(On(due.AddDays(4)).DaysLeft);

        var waiting = On(due.AddDays(10), declared: true); // a declared payment stops the countdown
        Assert.Equal((true, false, true, (int?)null), (waiting.Late, waiting.Overdue, waiting.WaitingForApproval, waiting.DaysLeft));
    }

    [Fact]
    public void Evaluate_SettledDuesDontCount_TheEarliestDeadlineWins()
    {
        var state = BillingRules.Evaluate(
        [
            new BillingDue(new DateOnly(2026, 10, 1), true, false),
            new BillingDue(new DateOnly(2026, 10, 21), false, false),
            new BillingDue(new DateOnly(2026, 10, 22), false, false),
        ], Today, 3);

        Assert.False(state.Overdue);
        Assert.Equal(2, state.DaysLeft); // the 21st: grace ends on the 24th
        Assert.True(BillingRules.Evaluate([new BillingDue(new DateOnly(2026, 10, 19), false, false)], Today, 3).Overdue);
        Assert.True(BillingRules.Evaluate([new BillingDue(new DateOnly(2026, 10, 22), false, false)], Today, 0).Overdue); // no grace days
    }
}
