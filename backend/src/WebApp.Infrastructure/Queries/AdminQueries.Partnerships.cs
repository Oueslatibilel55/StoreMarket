using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Domain.Entities;

namespace WebApp.Infrastructure.Queries;

/// <summary>Every affiliate ↔ store partnership (012), for the admin.</summary>
internal sealed partial class AdminQueries
{
    public async Task<PagedResult<AdminPartnershipItem>> ListPartnershipsAsync(AdminPartnershipQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var partnerships = db.Partnerships.AsNoTracking();

        if (query.Status is { } status) partnerships = partnerships.Where(p => p.Status == status);
        if (query.StoreId is { } storeId) partnerships = partnerships.Where(p => p.StoreId == storeId);
        if (query.AffiliateId is { } affiliateId) partnerships = partnerships.Where(p => p.AffiliateId == affiliateId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{EscapeLike(query.Search.Trim().TrimStart('#'))}%";
            partnerships = partnerships.Where(p =>
                db.Users.Any(u => u.Id == p.AffiliateId && (EF.Functions.ILike(u.Email, term, "\\")
                    || EF.Functions.ILike(u.FullName, term, "\\") || EF.Functions.ILike(u.AffiliateCode!, term, "\\")))
                || db.Stores.Any(s => s.Id == p.StoreId && (EF.Functions.ILike(s.Name, term, "\\") || EF.Functions.ILike(s.Slug, term, "\\")
                    || db.Users.Any(o => o.Id == s.OwnerId && (EF.Functions.ILike(o.Email, term, "\\") || EF.Functions.ILike(o.FullName, term, "\\"))))));
        }

        var total = await partnerships.CountAsync(ct).ConfigureAwait(false);
        var items = await Project(partnerships
                .OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id)
                .Skip(paging.Skip).Take(paging.PageSize))
            .ToListAsync(ct).ConfigureAwait(false);

        return new PagedResult<AdminPartnershipItem>(items, paging.Page, paging.PageSize, total);
    }

    public Task<AdminPartnershipItem?> GetPartnershipAsync(Guid id, CancellationToken ct = default) =>
        Project(db.Partnerships.AsNoTracking().Where(p => p.Id == id)).FirstOrDefaultAsync(ct);

    private IQueryable<AdminPartnershipItem> Project(IQueryable<Partnership> partnerships) =>
        partnerships
            .Join(db.Users, p => p.AffiliateId, u => u.Id, (p, u) => new { p, u })
            .Join(db.Stores, x => x.p.StoreId, s => s.Id, (x, s) => new { x.p, x.u, s })
            .Join(db.Users, x => x.s.OwnerId, o => o.Id, (x, o) => new AdminPartnershipItem(
                x.p.Id, x.p.Status, x.u.Id, x.u.FullName, x.u.Email, x.u.AffiliateCode, x.s.Id, x.s.Name, x.s.Slug,
                o.Id, o.FullName, o.Email,
                x.p.Message, x.p.DecisionReason, x.p.EndReason, x.p.CreatedAt, x.p.DecidedAt, x.p.EndedAt));
}
