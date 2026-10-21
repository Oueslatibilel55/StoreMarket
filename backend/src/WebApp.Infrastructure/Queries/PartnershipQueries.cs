using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Queries;

/// <summary>Partnership lists of an affiliate ("My stores") and of a store ("My affiliates") — 012.</summary>
internal sealed class PartnershipQueries(AppDbContext db) : IPartnershipQueries
{
    public async Task<IReadOnlyList<AffiliatePartnershipDto>> ListForAffiliateAsync(Guid affiliateId, CancellationToken ct = default) =>
        await ForAffiliate(db.Partnerships.AsNoTracking().Where(p => p.AffiliateId == affiliateId)
                .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id))
            .ToListAsync(ct).ConfigureAwait(false);

    public Task<AffiliatePartnershipDto?> GetForAffiliateAsync(Guid affiliateId, Guid id, CancellationToken ct = default) =>
        ForAffiliate(db.Partnerships.AsNoTracking().Where(p => p.AffiliateId == affiliateId && p.Id == id))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<StorePartnershipDto>> ListForStoreAsync(Guid storeId, PartnershipStatus? status, CancellationToken ct = default)
    {
        var partnerships = db.Partnerships.AsNoTracking().Where(p => p.StoreId == storeId);
        if (status is { } s) partnerships = partnerships.Where(p => p.Status == s);

        // Waiting requests first, oldest first (first come, first served); then everything else, latest change first.
        var ordered = partnerships
            .OrderBy(p => p.Status == PartnershipStatus.Requested ? 0 : 1)
            .ThenBy(p => p.Status == PartnershipStatus.Requested ? p.CreatedAt : DateTime.MinValue)
            .ThenByDescending(p => p.UpdatedAt)
            .ThenBy(p => p.Id);
        return await ForStore(ordered).ToListAsync(ct).ConfigureAwait(false);
    }

    public Task<StorePartnershipDto?> GetForStoreAsync(Guid storeId, Guid id, CancellationToken ct = default) =>
        ForStore(db.Partnerships.AsNoTracking().Where(p => p.StoreId == storeId && p.Id == id)).FirstOrDefaultAsync(ct);

    private IQueryable<AffiliatePartnershipDto> ForAffiliate(IQueryable<Partnership> partnerships) =>
        partnerships.Join(db.Stores, p => p.StoreId, s => s.Id, (p, s) => new AffiliatePartnershipDto(
            p.Id, p.Status, p.Message, p.DecisionReason, p.EndReason, p.CreatedAt, p.DecidedAt, p.EndedAt,
            new PartnerStoreRef(s.Id, s.Name, s.Slug,
                db.Images.Where(i => i.Id == s.LogoImageId).Select(i => ImageUrls.RequestPath + "/" + i.Path).FirstOrDefault(),
                s.Status == StoreStatus.Active,
                s.Status == StoreStatus.Active || s.Status == StoreStatus.Closed,
                s.DefaultAffiliatePercent)));

    private IQueryable<StorePartnershipDto> ForStore(IQueryable<Partnership> partnerships) =>
        partnerships.Join(db.Users, p => p.AffiliateId, u => u.Id, (p, u) => new StorePartnershipDto(
            p.Id, p.Status, p.Message, p.DecisionReason, p.EndReason, p.CreatedAt, p.DecidedAt, p.EndedAt,
            new PartnerAffiliate(u.Id, u.FullName, u.AffiliateCode,
                u.Status == UserStatus.Active && u.Roles.Contains(Role.Affiliate) && u.AffiliateCodeDisabledAt == null,
                u.CreatedAt,
                db.Partnerships.Count(o => o.AffiliateId == u.Id && o.Status == PartnershipStatus.Accepted))));
}
