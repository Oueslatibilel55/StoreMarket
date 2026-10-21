using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

public interface IStoreCategoryRepository
{
    Task<StoreCategory?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Every category in display order (tracked: used to move one next to its neighbour).</summary>
    Task<IReadOnlyList<StoreCategory>> ListOrderedAsync(CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken ct = default);
    /// <summary>Case-insensitive French name check.</summary>
    Task<bool> NameFrExistsAsync(string nameFr, Guid? exceptId, CancellationToken ct = default);
    Task<int> NextSortOrderAsync(CancellationToken ct = default);
    void Add(StoreCategory category);
    void Remove(StoreCategory category);
}
