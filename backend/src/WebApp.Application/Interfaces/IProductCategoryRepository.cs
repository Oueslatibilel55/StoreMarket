using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

public interface IProductCategoryRepository
{
    /// <summary>The whole tree with attributes, tracked (a few hundred rows at most).</summary>
    Task<IReadOnlyList<ProductCategory>> ListAllAsync(CancellationToken ct = default);
    void Add(ProductCategory category);
    void Remove(ProductCategory category);
}
