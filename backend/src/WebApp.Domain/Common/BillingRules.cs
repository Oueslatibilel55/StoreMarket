namespace WebApp.Domain.Common;

/// <summary>Something a store must pay by a date: its subscription (paid-until) or a commission invoice (due date).</summary>
public sealed record BillingDue(DateOnly Due, bool Settled, bool PaymentDeclared);

/// <summary>
/// Where a store stands with billing (025). <c>DaysLeft</c>: grace days left before suspension (3, 2, 1) while something
/// is late; <c>Overdue</c>: past the grace days — the store must be suspended.
/// </summary>
public sealed record BillingState(bool Late, int? DaysLeft, bool Overdue, bool WaitingForApproval);

/// <summary>
/// The suspension rule (025): a store is suspended once something is unpaid <c>graceDays</c> days after its due date.
/// A payment declared and waiting for an admin stops the countdown for what it pays.
/// </summary>
public static class BillingRules
{
    /// <summary>
    /// Due on day D with 3 grace days: D+1, D+2 and D+3 are the grace days ("closes in 3 / 2 / 1 day(s)"), and the store
    /// is suspended from D+4.
    /// </summary>
    public static BillingState Evaluate(IEnumerable<BillingDue> dues, DateOnly today, int graceDays)
    {
        ArgumentNullException.ThrowIfNull(dues);
        var grace = Math.Max(0, graceDays);
        var late = dues.Where(d => !d.Settled && today > d.Due).ToList();
        var counting = late.Where(d => !d.PaymentDeclared).ToList();

        var overdue = counting.Any(d => today > d.Due.AddDays(grace));
        int? daysLeft = counting.Count == 0 || overdue
            ? null
            : counting.Min(d => d.Due.AddDays(grace).DayNumber - today.DayNumber + 1);
        return new BillingState(late.Count > 0, daysLeft, overdue, late.Any(d => d.PaymentDeclared));
    }
}
