using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

public interface IStoreRepository
{
    Task<Store?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task<Store?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken ct = default);
    Task<bool> AnyInCategoryAsync(Guid categoryId, CancellationToken ct = default);
    void Add(Store store);
}
