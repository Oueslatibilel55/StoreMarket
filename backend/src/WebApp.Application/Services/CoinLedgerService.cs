using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Domain.Settings;

namespace WebApp.Application.Services;

/// <summary>
/// The coin ledger (019). Coins are credited when a store order becomes Delivered, <b>inside the same transaction</b>
/// as the status change (not by an after-commit event handler), so a delivered affiliate line can never miss its
/// coins and is never credited twice (one Earned entry per order line, unique in the database). Corrections are
/// admin adjustments, audited; nothing is ever edited or deleted.
/// </summary>
public sealed class CoinLedgerService(
    ICoinLedger ledger,
    ICoinQueries queries,
    IUserRepository users,
    IPlatformSettings settings,
    IAuditLog audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Writes the Earned entries of a store order that just became Delivered. Called by the order lifecycle before
    /// it commits; lines already credited are skipped (idempotent).
    /// </summary>
    public async Task CreditDeliveredAsync(Order order, StoreOrder storeOrder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(storeOrder);

        var paying = storeOrder.Lines.Where(l => l.AffiliateId is not null && l.ExpectedCoins > 0).ToList();
        if (paying.Count == 0) return;

        var already = await ledger.EarnedLineIdsAsync([.. paying.Select(l => l.Id)], ct).ConfigureAwait(false);
        foreach (var line in paying.Where(l => !already.Contains(l.Id)))
        {
            if (CoinEntry.Earned(order, storeOrder, line) is { } entry)
                await ledger.AppendAsync(entry, ct).ConfigureAwait(false);
        }
    }

    public async Task<CoinSummaryDto> SummaryAsync(Guid affiliateId, CancellationToken ct = default) =>
        await queries.SummaryAsync(affiliateId, await CoinValueAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    /// <summary>The affiliate's own entries (client phones masked).</summary>
    public Task<PagedResult<CoinEntryDto>> ListMineAsync(Guid affiliateId, CoinEntryQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return queries.ListAsync(query with { AffiliateId = affiliateId }, maskPhones: true, ct);
    }

    /// <summary>Most rows an export returns at once.</summary>
    public const int MaxExportRows = 5000;

    /// <summary>The affiliate's entries matching the filters, for their CSV export (built by the client in its language).</summary>
    public Task<IReadOnlyList<CoinEntryDto>> ExportMineAsync(Guid affiliateId, CoinEntryQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return queries.ExportAsync(query with { AffiliateId = affiliateId }, maskPhones: true, MaxExportRows, ct);
    }

    /// <summary>Earned coins per product or per store over a period (020).</summary>
    public Task<IReadOnlyList<CoinBreakdownRow>> BreakdownAsync(Guid affiliateId, CoinBreakdownBy by, DateTime? from, DateTime? to, CancellationToken ct = default) =>
        queries.BreakdownAsync(affiliateId, by, from, to, ct);

    public Task<PagedResult<CoinEntryDto>> ListAsync(CoinEntryQuery query, CancellationToken ct = default) =>
        queries.ListAsync(query, maskPhones: false, ct);

    public Task<AdminCoinTotals> TotalsAsync(CancellationToken ct = default) => queries.TotalsAsync(ct);

    /// <summary>An admin corrects an affiliate's coins with a signed adjustment. Reason required, audited on the affiliate.</summary>
    public async Task<Result<CoinSummaryDto>> AdjustAsync(AdminActor actor, CreateCoinAdjustmentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);

        var affiliate = await users.GetByIdAsync(request.AffiliateId, ct).ConfigureAwait(false);
        if (affiliate?.AffiliateCode is null) return Result.Fail<CoinSummaryDto>(CoinErrors.AffiliateNotFound);
        if (request.RelatedEntryId is { } related)
        {
            // The corrected entry must be one of this affiliate's.
            var relatedEntry = await ledger.GetAsync(related, ct).ConfigureAwait(false);
            if (relatedEntry is null || relatedEntry.AffiliateId != affiliate.Id) return Result.Fail<CoinSummaryDto>(CoinErrors.EntryNotFound);
        }

        var entry = CoinEntry.Adjustment(affiliate.Id, request.Coins, request.Reason, actor.Id, request.RelatedEntryId);
        if (!entry.IsSuccess) return Result.Fail<CoinSummaryDto>(entry.Error!);

        var before = await SummaryAsync(affiliate.Id, ct).ConfigureAwait(false);
        var result = await unitOfWork.InTransactionAsync(async () =>
        {
            await ledger.AppendAsync(entry.Value, ct).ConfigureAwait(false);
            audit.Record(actor.Email, actor.Id, AuditActions.CoinAdjustment, AuditTargets.User, affiliate.Id, affiliate.Email,
                new { before.Balance }, new { Balance = before.Balance + request.Coins, Coins = request.Coins }, entry.Value.Reason);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            return Result.Ok(true);
        }, ct).ConfigureAwait(false);
        return result.IsSuccess
            ? Result.Ok(await SummaryAsync(affiliate.Id, ct).ConfigureAwait(false))
            : Result.Fail<CoinSummaryDto>(result.Error!);
    }

    private Task<decimal> CoinValueAsync(CancellationToken ct) => settings.GetDecimalAsync(SettingKeys.CoinValueInTnd, ct);
}
