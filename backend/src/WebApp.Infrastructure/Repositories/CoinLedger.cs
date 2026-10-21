using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Repositories;

internal sealed class CoinLedger(AppDbContext db) : ICoinLedger
{
    public async Task<IReadOnlySet<Guid>> EarnedLineIdsAsync(IReadOnlyCollection<Guid> orderLineIds, CancellationToken ct = default) =>
        (await db.CoinEntries
            .Where(e => e.Type == CoinEntryType.Earned && e.OrderLineId != null && orderLineIds.Contains(e.OrderLineId.Value))
            .Select(e => e.OrderLineId!.Value)
            .ToListAsync(ct).ConfigureAwait(false)).ToHashSet();

    public Task<CoinEntry?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.CoinEntries.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task AppendAsync(CoinEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        // The account increment runs now, the entry is inserted at commit: both must share one transaction,
        // or a failed save would leave coins on the account without their entry.
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Coin entries are appended inside IUnitOfWork.InTransactionAsync.");

        db.CoinEntries.Add(entry);
        var earned = entry.Type == CoinEntryType.Earned ? entry.Coins : 0m;
        // Atomic increment: two stores delivering for the same affiliate at once never conflict.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coin_accounts (affiliate_id, balance, total_earned, updated_at)
            VALUES ({entry.AffiliateId}, {entry.Coins}, {earned}, now())
            ON CONFLICT (affiliate_id) DO UPDATE
            SET balance = coin_accounts.balance + EXCLUDED.balance,
                total_earned = coin_accounts.total_earned + EXCLUDED.total_earned,
                updated_at = now()
            """, ct).ConfigureAwait(false);
    }

    public async Task<decimal> BalanceAsync(Guid affiliateId, CancellationToken ct = default) =>
        await db.CoinAccounts.AsNoTracking().Where(a => a.AffiliateId == affiliateId).Select(a => (decimal?)a.Balance)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false) ?? 0;

    public async Task<decimal> LockBalanceAsync(Guid affiliateId, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("The coin account is locked inside IUnitOfWork.InTransactionAsync.");
        var balances = await db.Database
            .SqlQuery<decimal>($"SELECT balance AS \"Value\" FROM coin_accounts WHERE affiliate_id = {affiliateId} FOR UPDATE")
            .ToListAsync(ct).ConfigureAwait(false);
        return balances is [var balance] ? balance : 0;
    }
}
