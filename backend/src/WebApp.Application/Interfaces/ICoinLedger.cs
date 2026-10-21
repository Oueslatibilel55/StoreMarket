using WebApp.Application.DTOs;
using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

/// <summary>The coin ledger (019): append-only entries and the cached account totals.</summary>
public interface ICoinLedger
{
    /// <summary>Order lines that already have an Earned entry (idempotence: a line is credited once).</summary>
    Task<IReadOnlySet<Guid>> EarnedLineIdsAsync(IReadOnlyCollection<Guid> orderLineIds, CancellationToken ct = default);
    Task<CoinEntry?> GetAsync(Guid id, CancellationToken ct = default);
    /// <summary>Adds the entry and increments the affiliate's account atomically (same transaction, saved at commit).</summary>
    Task AppendAsync(CoinEntry entry, CancellationToken ct = default);
    /// <summary>The affiliate's balance (0 without an account).</summary>
    Task<decimal> BalanceAsync(Guid affiliateId, CancellationToken ct = default);
    /// <summary>The balance, with the account row locked until the transaction ends (021: no coins spent twice).</summary>
    Task<decimal> LockBalanceAsync(Guid affiliateId, CancellationToken ct = default);
}

/// <summary>Read-only coin views (no tracking, projections only).</summary>
public interface ICoinQueries
{
    Task<CoinSummaryDto> SummaryAsync(Guid affiliateId, decimal coinValueInTnd, CancellationToken ct = default);
    /// <summary>Ledger rows, newest first. <paramref name="maskPhones"/> for the affiliate's own view.</summary>
    Task<PagedResult<CoinEntryDto>> ListAsync(CoinEntryQuery query, bool maskPhones, CancellationToken ct = default);
    Task<AdminCoinTotals> TotalsAsync(CancellationToken ct = default);
    /// <summary>Every row matching the filters (no paging, capped) — for the CSV export.</summary>
    Task<IReadOnlyList<CoinEntryDto>> ExportAsync(CoinEntryQuery query, bool maskPhones, int max, CancellationToken ct = default);
    /// <summary>Earned coins grouped by product or store, biggest first (020).</summary>
    Task<IReadOnlyList<CoinBreakdownRow>> BreakdownAsync(Guid affiliateId, CoinBreakdownBy by, DateTime? since, DateTime? until, CancellationToken ct = default);
    /// <summary>The cached balance and the sum of the entries — they must always be equal (checked by tests).</summary>
    Task<(decimal Cached, decimal Sum)> CheckBalanceAsync(Guid affiliateId, CancellationToken ct = default);
}
