namespace WebApp.Domain.Entities;

/// <summary>
/// Cached totals of one affiliate's ledger (019): always equal to the sum of their <see cref="CoinEntry"/> rows.
/// Updated with atomic increments in the same transaction as the entries, so two stores delivering at once never
/// conflict. Never write it any other way.
/// </summary>
public sealed class CoinAccount
{
    public Guid AffiliateId { get; private set; }
    /// <summary>Sum of every entry.</summary>
    public decimal Balance { get; private set; }
    /// <summary>Sum of the Earned entries.</summary>
    public decimal TotalEarned { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CoinAccount() { }
}
