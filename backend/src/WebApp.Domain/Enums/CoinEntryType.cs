namespace WebApp.Domain.Enums;

/// <summary>Kind of coin movement (019). Stored as int — append only, never renumber.</summary>
public enum CoinEntryType
{
    /// <summary>Coins of a delivered order line with an affiliate code.</summary>
    Earned = 1,
    /// <summary>Coins set aside by a payout request (021).</summary>
    PayoutLocked = 2,
    /// <summary>Locked coins given back (payout refused / cancelled — 021).</summary>
    PayoutReleased = 3,
    /// <summary>Admin correction, signed, with a reason.</summary>
    Adjustment = 4,
}
