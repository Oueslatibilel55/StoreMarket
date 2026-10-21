using WebApp.Application.DTOs;
using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

public interface IPayoutRepository
{
    /// <summary>Next number of the <c>PAY-YYYY-NNNNNN</c> reference (database sequence).</summary>
    Task<long> NextNumberAsync(CancellationToken ct = default);
    /// <summary>The request with its history, tracked.</summary>
    Task<PayoutRequest?> GetAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasOpenAsync(Guid affiliateId, CancellationToken ct = default);
    void Add(PayoutRequest request);
}

/// <summary>Read-only payout views (no tracking, projections).</summary>
public interface IPayoutQueries
{
    Task<PayoutDto?> GetAsync(Guid id, bool adminView, CancellationToken ct = default);
    /// <summary>The affiliate's requests (newest first) and the bank details of their last transfer request.</summary>
    Task<(IReadOnlyList<PayoutDto> Items, PayoutBankDetails? LastBankDetails)> MineAsync(Guid affiliateId, CancellationToken ct = default);
    Task<PagedResult<PayoutListItem>> ListAsync(PayoutQuery query, CancellationToken ct = default);
    Task<AdminPayoutCounts> CountsAsync(CancellationToken ct = default);
    Task<AdminPayoutDetail?> DetailAsync(Guid id, CancellationToken ct = default);
}
