using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Repositories;

internal sealed class PartnershipRepository(AppDbContext db) : IPartnershipRepository
{
    public Task<Partnership?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Partnerships.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Partnership?> GetOpenAsync(Guid affiliateId, Guid storeId, CancellationToken ct = default) =>
        db.Partnerships.FirstOrDefaultAsync(p => p.AffiliateId == affiliateId && p.StoreId == storeId
            && (p.Status == PartnershipStatus.Requested || p.Status == PartnershipStatus.Accepted), ct);

    public async Task<Guid?> GetActiveIdAsync(Guid affiliateId, Guid storeId, CancellationToken ct = default) =>
        await db.Partnerships
            .Where(p => p.AffiliateId == affiliateId && p.StoreId == storeId && p.Status == PartnershipStatus.Accepted)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

    public void Add(Partnership partnership) => db.Partnerships.Add(partnership);
}
