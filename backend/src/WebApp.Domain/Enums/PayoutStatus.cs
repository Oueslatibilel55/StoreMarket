namespace WebApp.Domain.Enums;

/// <summary>Where a payout request stands (021). Stored as int — append only, never renumber.</summary>
public enum PayoutStatus
{
    /// <summary>Sent by the affiliate; coins locked.</summary>
    Requested = 1,
    /// <summary>An admin checked the coins it covers.</summary>
    Verified = 2,
    /// <summary>A date is set for the cash handover or the transfer.</summary>
    Scheduled = 3,
    /// <summary>Money handed over / transferred — final.</summary>
    Paid = 4,
    /// <summary>Refused by an admin (reason given); coins released.</summary>
    Rejected = 5,
    /// <summary>Withdrawn by the affiliate before it was scheduled; coins released.</summary>
    Cancelled = 6,
}
