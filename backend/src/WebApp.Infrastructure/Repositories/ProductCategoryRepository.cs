using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Repositories;

internal sealed class ProductCategoryRepository(AppDbContext db) : IProductCategoryRepository
{
    public async Task<IReadOnlyList<ProductCategory>> ListAllAsync(CancellationToken ct = default) =>
        await db.ProductCategories.Include(c => c.Attributes).ToListAsync(ct).ConfigureAwait(false);

    public void Add(ProductCategory category) => db.ProductCategories.Add(category);

    public void Remove(ProductCategory category) => db.ProductCategories.Remove(category);
}
