namespace WebApp.Domain.Enums;

/// <summary>Stored as int — append only, never renumber.</summary>
public enum PartnershipStatus
{
    /// <summary>The affiliate asked; the store owner hasn't decided yet.</summary>
    Requested = 1,
    /// <summary>Active partnership: the affiliate's code is accepted on the store's products.</summary>
    Accepted = 2,
    Refused = 3,
    /// <summary>The store owner (or an admin, for fraud) stopped an accepted partnership. Future orders only.</summary>
    Ended = 4,
    /// <summary>The affiliate withdrew their request before a decision.</summary>
    Cancelled = 5,
}
