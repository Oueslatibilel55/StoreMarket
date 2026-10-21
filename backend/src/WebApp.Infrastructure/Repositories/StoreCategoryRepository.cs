using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Repositories;

internal sealed class StoreCategoryRepository(AppDbContext db) : IStoreCategoryRepository
{
    public Task<StoreCategory?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.StoreCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<StoreCategory>> ListOrderedAsync(CancellationToken ct = default) =>
        await db.StoreCategories.OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt).ToListAsync(ct).ConfigureAwait(false);

    public Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken ct = default) =>
        db.StoreCategories.AnyAsync(c => c.Slug == slug && c.Id != exceptId, ct);

    public Task<bool> NameFrExistsAsync(string nameFr, Guid? exceptId, CancellationToken ct = default)
    {
        // ILIKE without wildcards = case-insensitive equality; escape the LIKE special characters.
        var pattern = nameFr.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return db.StoreCategories.AnyAsync(c => EF.Functions.ILike(c.NameFr, pattern, "\\") && c.Id != exceptId, ct);
    }

    public async Task<int> NextSortOrderAsync(CancellationToken ct = default) =>
        (await db.StoreCategories.MaxAsync(c => (int?)c.SortOrder, ct).ConfigureAwait(false) ?? 0) + 1;

    public void Add(StoreCategory category) => db.StoreCategories.Add(category);

    public void Remove(StoreCategory category) => db.StoreCategories.Remove(category);
}
