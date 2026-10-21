using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;
using WebApp.Infrastructure.Persistence.Configurations;

namespace WebApp.Infrastructure.Repositories;

internal sealed class PayoutRepository(AppDbContext db) : IPayoutRepository
{
    private static readonly PayoutStatus[] Open = [PayoutStatus.Requested, PayoutStatus.Verified, PayoutStatus.Scheduled];

    public async Task<long> NextNumberAsync(CancellationToken ct = default) =>
        await db.Database
            .SqlQueryRaw<long>($"SELECT nextval('{PayoutRequestConfiguration.NumberSequence}') AS \"Value\"")
            .SingleAsync(ct).ConfigureAwait(false);

    public Task<PayoutRequest?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.PayoutRequests.Include(p => p.History).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> HasOpenAsync(Guid affiliateId, CancellationToken ct = default) =>
        db.PayoutRequests.AnyAsync(p => p.AffiliateId == affiliateId && Open.Contains(p.Status), ct);

    public void Add(PayoutRequest request) => db.PayoutRequests.Add(request);
}
