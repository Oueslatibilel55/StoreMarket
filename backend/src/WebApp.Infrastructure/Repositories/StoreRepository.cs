using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Repositories;

internal sealed class StoreRepository(AppDbContext db) : IStoreRepository
{
    public Task<Store?> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default) =>
        db.Stores.FirstOrDefaultAsync(s => s.OwnerId == ownerId, ct);

    public Task<Store?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Stores.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken ct = default) =>
        db.Stores.AnyAsync(s => s.Slug == slug && s.Id != exceptId, ct);

    public Task<bool> AnyInCategoryAsync(Guid categoryId, CancellationToken ct = default) =>
        db.Stores.AnyAsync(s => s.CategoryId == categoryId, ct);

    public void Add(Store store) => db.Stores.Add(store);
}
