using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Repositories;

internal sealed class ProductRepository(AppDbContext db) : IProductRepository
{
    public Task<Product?> GetForStoreAsync(Guid id, Guid storeId, CancellationToken ct = default) =>
        db.Products.Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId && p.DeletedAt == null, ct);

    public async Task<IReadOnlyList<Product>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct).ConfigureAwait(false);

    public Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken ct = default) =>
        db.Products.AnyAsync(p => p.Slug == slug && p.Id != exceptId, ct);

    public Task<bool> AnyInCategoriesAsync(IReadOnlyCollection<Guid> categoryIds, CancellationToken ct = default) =>
        db.Products.AnyAsync(p => categoryIds.Contains(p.CategoryId), ct);

    public async Task<IReadOnlyList<Product>> ListInCategoriesAsync(IReadOnlyCollection<Guid> categoryIds, CancellationToken ct = default) =>
        await db.Products.AsNoTracking().Include(p => p.Variants)
            .Where(p => categoryIds.Contains(p.CategoryId) && p.DeletedAt == null)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<bool> TryTakeStockAsync(Guid productId, Guid? variantId, int quantity, CancellationToken ct = default)
    {
        var updated = variantId is { } vid
            ? await db.Set<ProductVariant>()
                .Where(v => v.Id == vid && EF.Property<Guid>(v, "ProductId") == productId && v.Stock >= quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, v => v.Stock - quantity), ct).ConfigureAwait(false)
            : await db.Products
                .Where(p => p.Id == productId && p.Stock >= quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - quantity), ct).ConfigureAwait(false);
        return updated == 1;
    }

    public async Task ReturnStockAsync(Guid productId, Guid? variantId, int quantity, CancellationToken ct = default)
    {
        if (variantId is { } vid)
            await db.Set<ProductVariant>()
                .Where(v => v.Id == vid && EF.Property<Guid>(v, "ProductId") == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, v => v.Stock + quantity), ct).ConfigureAwait(false);
        else
            await db.Products.Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + quantity), ct).ConfigureAwait(false);
    }

    public void Add(Product product) => db.Products.Add(product);
}
