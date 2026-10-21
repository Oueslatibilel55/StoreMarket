using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Application.Interfaces;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Queries;

internal sealed partial class AdminQueries(AppDbContext db) : IAdminQueries
{
    public async Task<PagedResult<AdminUserListItem>> ListUsersAsync(UserListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var users = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{EscapeLike(query.Search.Trim())}%";
            var digits = new string(query.Search.Where(char.IsDigit).ToArray());
            var phoneTerm = digits.Length >= 3 ? $"%{digits}%" : null;
            users = users.Where(u =>
                EF.Functions.ILike(u.Email, term, "\\")
                || EF.Functions.ILike(u.FullName, term, "\\")
                || (u.AffiliateCode != null && EF.Functions.ILike(u.AffiliateCode, term, "\\"))
                || (phoneTerm != null && EF.Functions.Like(u.Phone, phoneTerm))
                // A store owner is also found by the name of their store.
                || db.Stores.Any(s => s.OwnerId == u.Id && s.DeletedAt == null && EF.Functions.ILike(s.Name, term, "\\")));
        }
        if (query.Role is { } role) users = users.Where(u => u.Roles.Contains(role));
        if (query.Status is { } status) users = users.Where(u => u.Status == status);

        var total = await users.CountAsync(ct).ConfigureAwait(false);
        var items = await users
            .OrderByDescending(u => u.CreatedAt)
            .Skip(paging.Skip).Take(paging.PageSize)
            .Select(u => new AdminUserListItem(u.Id, u.Email, u.FullName, u.Phone, u.Roles, u.Status,
                u.AffiliateCode, u.LastLoginAt, u.CreatedAt,
                db.Stores.Where(s => s.OwnerId == u.Id && s.DeletedAt == null)
                    .Select(s => new AdminUserStore(s.Id, s.Name, s.Slug, s.Status)).FirstOrDefault()))
            .ToListAsync(ct).ConfigureAwait(false);

        return new PagedResult<AdminUserListItem>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<UserCounts> CountUsersAsync(CancellationToken ct = default)
    {
        var counts = await db.Users.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new UserCounts(
                g.Count(),
                g.Count(u => u.Status == UserStatus.Active),
                g.Count(u => u.Status == UserStatus.Blocked),
                g.Count(u => u.Roles.Contains(Role.Buyer)),
                g.Count(u => u.Roles.Contains(Role.Affiliate)),
                g.Count(u => u.Roles.Contains(Role.StoreOwner)),
                g.Count(u => u.Roles.Contains(Role.Admin))))
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return counts ?? new UserCounts(0, 0, 0, 0, 0, 0, 0);
    }

    public async Task<PagedResult<AuditEntryDto>> ListAuditAsync(AuditQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var entries = db.AuditEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.TargetType)) entries = entries.Where(a => a.TargetType == query.TargetType);
        if (query.TargetId is { } targetId) entries = entries.Where(a => a.TargetId == targetId);
        if (query.ActorId is { } actorId) entries = entries.Where(a => a.ActorId == actorId);
        if (!string.IsNullOrWhiteSpace(query.Action)) entries = entries.Where(a => a.Action == query.Action);

        var total = await entries.CountAsync(ct).ConfigureAwait(false);
        var items = await entries
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip(paging.Skip).Take(paging.PageSize)
            .Select(a => new AuditEntryDto(a.Id, a.CreatedAt, a.ActorId, a.ActorEmail, a.Action, a.TargetType,
                a.TargetId, a.TargetLabel, a.Reason, a.Before, a.After))
            .ToListAsync(ct).ConfigureAwait(false);

        return new PagedResult<AuditEntryDto>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<PagedResult<AdminRoleRequestItem>> ListRoleRequestsAsync(RoleRequestListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var paging = Paging.From(query.Page, query.PageSize);
        var requests = db.RoleRequests.AsNoTracking();

        if (query.Status is { } status) requests = requests.Where(r => r.Status == status);
        if (query.Role is { } role) requests = requests.Where(r => r.Role == role);

        var total = await requests.CountAsync(ct).ConfigureAwait(false);
        // Pending ones oldest first (first come, first served); decided ones newest first.
        var ordered = query.Status == RoleRequestStatus.Pending
            ? requests.OrderBy(r => r.CreatedAt)
            : requests.OrderByDescending(r => r.CreatedAt);
        var items = await ordered.ThenBy(r => r.Id)
            .Join(db.Users, r => r.UserId, u => u.Id, (r, u) => new AdminRoleRequestItem(
                r.Id, u.Id, u.Email, u.FullName, u.Roles, r.Role, r.Status, r.Message, r.DecisionReason,
                r.DecidedAt, r.CreatedAt))
            .Skip(paging.Skip).Take(paging.PageSize)
            .ToListAsync(ct).ConfigureAwait(false);

        return new PagedResult<AdminRoleRequestItem>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<IReadOnlyList<StoredSetting>> ListStoredSettingsAsync(CancellationToken ct = default) =>
        await db.PlatformSettings.AsNoTracking()
            .GroupJoin(db.Users, s => s.UpdatedById, u => u.Id, (s, admins) => new { s, admins })
            .SelectMany(x => x.admins.DefaultIfEmpty(), (x, u) => new StoredSetting(x.s.Key, x.s.Value, x.s.UpdatedAt, u != null ? u.Email : null))
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<AdminStoreCategoryDto>> ListStoreCategoriesAsync(CancellationToken ct = default) =>
        await db.StoreCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
            .Select(c => new AdminStoreCategoryDto(c.Id, c.Slug, new LocalizedName(c.NameFr, c.NameAr, c.NameEn), c.Icon, c.ImageId,
                db.Images.Where(i => i.Id == c.ImageId).Select(i => ImageUrls.RequestPath + "/" + i.Path).FirstOrDefault(),
                c.SortOrder, c.IsActive, c.CreatedAt, c.UpdatedAt))
            .ToListAsync(ct).ConfigureAwait(false);

    /// <summary>The whole tree, depth-first with sisters in order, each category with its own attributes.</summary>
    public async Task<IReadOnlyList<AdminProductCategoryDto>> ListProductCategoriesAsync(CancellationToken ct = default)
    {
        var tree = new CategoryTree(await db.ProductCategories.AsNoTracking().Include(c => c.Attributes).ToListAsync(ct).ConfigureAwait(false));
        return [.. CatalogQueries.Walk(tree, parentId: null).Select(ProductCategoryMapping.ToAdminDto)];
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);
}
