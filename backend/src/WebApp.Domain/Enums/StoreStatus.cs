namespace WebApp.Domain.Enums;

/// <summary>Stored as int — append only, never renumber.</summary>
public enum StoreStatus
{
    /// <summary>Visible and selling.</summary>
    Active = 1,
    /// <summary>Closed for a while by its owner (holidays…); reopens whenever they want.</summary>
    Closed = 2,
    /// <summary>Disabled by an admin (fake store, see 010).</summary>
    Disabled = 3,
    /// <summary>Unpaid subscription or commissions (see 025); paying re-activates it.</summary>
    Suspended = 4,
}
