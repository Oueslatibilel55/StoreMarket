using WebApp.Application.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Settings;

namespace WebApp.Application.Services;

/// <summary>
/// Payout requests (021). The affiliate asks for N coins (at least the platform minimum, at most their balance, one
/// open request at a time); the coins are locked in the ledger <b>in the same transaction</b>, under a lock on their
/// coin account so two requests (or a request and an adjustment) can never spend the same coins. An admin then
/// verifies — deciding case by case, with every covered commission shown — schedules and marks it paid; a rejection
/// or the affiliate's cancellation releases the coins. Every admin step is audited.
/// </summary>
public sealed class PayoutService(
    IPayoutRepository payouts,
    IPayoutQueries queries,
    ICoinLedger ledger,
    IUserRepository users,
    IPlatformSettings settings,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    // -- Affiliate ----------------------------------------------------------------

    public async Task<MyPayoutsDto> MineAsync(Guid affiliateId, CancellationToken ct = default)
    {
        var (items, bank) = await queries.MineAsync(affiliateId, ct).ConfigureAwait(false);
        var available = await ledger.BalanceAsync(affiliateId, ct).ConfigureAwait(false);
        return new MyPayoutsDto(available, await MinimumAsync(ct).ConfigureAwait(false), await CoinValueAsync(ct).ConfigureAwait(false), bank, items);
    }

    public async Task<Result<PayoutDto>> RequestAsync(Guid affiliateId, CreatePayoutRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var affiliate = await users.GetByIdAsync(affiliateId, ct).ConfigureAwait(false);
        if (affiliate is null || !affiliate.IsActive || affiliate.AffiliateCode is null) return Result.Fail<PayoutDto>(PayoutErrors.NotAffiliate);

        var minimum = await MinimumAsync(ct).ConfigureAwait(false);
        if (request.Coins < minimum) return Result.Fail<PayoutDto>(PayoutErrors.BelowMinimum);
        var coinValue = await CoinValueAsync(ct).ConfigureAwait(false);
        var now = clock.GetUtcNow().UtcDateTime;

        var result = await unitOfWork.InTransactionAsync(async () =>
        {
            // Locks the account row until commit: concurrent requests of this affiliate wait here, then see the
            // open request and the lower balance.
            var balance = await ledger.LockBalanceAsync(affiliateId, ct).ConfigureAwait(false);
            if (await payouts.HasOpenAsync(affiliateId, ct).ConfigureAwait(false)) return Result.Fail<Guid>(PayoutErrors.AlreadyOpen);
            if (request.Coins > balance) return Result.Fail<Guid>(PayoutErrors.NotEnoughCoins);

            var number = await payouts.NextNumberAsync(ct).ConfigureAwait(false);
            var created = PayoutRequest.Create(number, affiliateId, request.Coins, coinValue, request.Method,
                request.BankName, request.AccountHolder, request.Rib, request.Note, now);
            if (!created.IsSuccess) return Result.Fail<Guid>(created.Error!);

            payouts.Add(created.Value);
            await ledger.AppendAsync(CoinEntry.PayoutLocked(created.Value), ct).ConfigureAwait(false);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            return Result.Ok(created.Value.Id);
        }, ct).ConfigureAwait(false);

        return result.IsSuccess ? await DtoAsync(result.Value, adminView: false, ct).ConfigureAwait(false) : Result.Fail<PayoutDto>(result.Error!);
    }

    /// <summary>The affiliate withdraws a request that is not scheduled yet; the coins come back.</summary>
    public async Task<Result<PayoutDto>> CancelAsync(Guid affiliateId, Guid id, CancellationToken ct = default)
    {
        var payout = await payouts.GetAsync(id, ct).ConfigureAwait(false);
        // Someone else's request is reported as missing.
        if (payout is null || payout.AffiliateId != affiliateId) return Result.Fail<PayoutDto>(PayoutErrors.NotFound);
        var now = clock.GetUtcNow().UtcDateTime;
        return await ApplyAsync(payout, p => p.Cancel(affiliateId, now), audited: null, adminView: false, ct).ConfigureAwait(false);
    }

    // -- Admin --------------------------------------------------------------------

    public Task<PagedResult<PayoutListItem>> ListAsync(PayoutQuery query, CancellationToken ct = default) => queries.ListAsync(query, ct);

    public Task<AdminPayoutCounts> CountsAsync(CancellationToken ct = default) => queries.CountsAsync(ct);

    public async Task<Result<AdminPayoutDetail>> DetailAsync(Guid id, CancellationToken ct = default) =>
        await queries.DetailAsync(id, ct).ConfigureAwait(false) is { } detail ? Result.Ok(detail) : Result.Fail<AdminPayoutDetail>(PayoutErrors.NotFound);

    public Task<Result<PayoutDto>> VerifyAsync(AdminActor actor, Guid id, VerifyPayoutRequest request, CancellationToken ct = default) =>
        AdminStepAsync(actor, id, AuditActions.PayoutVerify, (p, now) => p.Verify(actor.Id, request?.Note, now), request?.Note, ct);

    public Task<Result<PayoutDto>> RejectAsync(AdminActor actor, Guid id, RejectPayoutRequest request, CancellationToken ct = default) =>
        AdminStepAsync(actor, id, AuditActions.PayoutReject, (p, now) => p.Reject(actor.Id, request?.Reason, now), request?.Reason, ct);

    public Task<Result<PayoutDto>> ScheduleAsync(AdminActor actor, Guid id, SchedulePayoutRequest request, CancellationToken ct = default) =>
        AdminStepAsync(actor, id, AuditActions.PayoutSchedule, (p, now) => p.Schedule(actor.Id, request?.ScheduledFor, request?.Place, now), null, ct);

    public Task<Result<PayoutDto>> MarkPaidAsync(AdminActor actor, Guid id, MarkPayoutPaidRequest request, CancellationToken ct = default) =>
        AdminStepAsync(actor, id, AuditActions.PayoutPaid, (p, now) => p.MarkPaid(actor.Id, request?.PaidAt, request?.Reference, now), null, ct);

    private async Task<Result<PayoutDto>> AdminStepAsync(AdminActor actor, Guid id, string action, Func<PayoutRequest, DateTime, Result> step,
        string? reason, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var payout = await payouts.GetAsync(id, ct).ConfigureAwait(false);
        if (payout is null) return Result.Fail<PayoutDto>(PayoutErrors.NotFound);
        var now = clock.GetUtcNow().UtcDateTime;
        return await ApplyAsync(payout, p => step(p, now), (actor, action, reason), adminView: true, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs one step in a transaction: the status change, the release of the coins when it ends without payment, and
    /// the audit entry. A concurrent change of the same request (xmin) is reported as <c>payout.changed</c>.
    /// </summary>
    private async Task<Result<PayoutDto>> ApplyAsync(PayoutRequest payout, Func<PayoutRequest, Result> step,
        (AdminActor Actor, string Action, string? Reason)? audited, bool adminView, CancellationToken ct)
    {
        var before = Snapshot(payout);
        try
        {
            var result = await unitOfWork.InTransactionAsync(async () =>
            {
                var moved = step(payout);
                if (!moved.IsSuccess) return Result.Fail<bool>(moved.Error!);
                if (payout.Status is Domain.Enums.PayoutStatus.Rejected or Domain.Enums.PayoutStatus.Cancelled)
                    await ledger.AppendAsync(CoinEntry.PayoutReleased(payout), ct).ConfigureAwait(false);
                if (audited is { } a)
                {
                    var affiliate = await users.GetByIdAsync(payout.AffiliateId, ct).ConfigureAwait(false);
                    audit.Record(a.Actor.Email, a.Actor.Id, a.Action, AuditTargets.Payout, payout.Id,
                        $"{payout.Reference} · {affiliate?.FullName}", before, Snapshot(payout), a.Reason);
                }
                await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
                return Result.Ok(true);
            }, ct).ConfigureAwait(false);
            if (!result.IsSuccess) return Result.Fail<PayoutDto>(result.Error!);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Fail<PayoutDto>(PayoutErrors.Changed);
        }
        return await DtoAsync(payout.Id, adminView, ct).ConfigureAwait(false);
    }

    private static object Snapshot(PayoutRequest p) =>
        new { p.Status, p.Coins, p.AmountTnd, p.Method, p.ScheduledFor, p.Place, p.PaidAt, p.PaidReference };

    private async Task<Result<PayoutDto>> DtoAsync(Guid id, bool adminView, CancellationToken ct) =>
        await queries.GetAsync(id, adminView, ct).ConfigureAwait(false) is { } dto ? Result.Ok(dto) : Result.Fail<PayoutDto>(PayoutErrors.NotFound);

    private Task<decimal> MinimumAsync(CancellationToken ct) => settings.GetDecimalAsync(SettingKeys.PayoutMinimumCoins, ct);
    private Task<decimal> CoinValueAsync(CancellationToken ct) => settings.GetDecimalAsync(SettingKeys.CoinValueInTnd, ct);
}
