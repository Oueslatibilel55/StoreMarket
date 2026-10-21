using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

public interface IProductRepository
{
    /// <summary>A product of the store (with variants), soft-deleted ones excluded.</summary>
    Task<Product?> GetForStoreAsync(Guid id, Guid storeId, CancellationToken ct = default);
    /// <summary>Products by id, deleted ones included (admin moderation).</summary>
    Task<IReadOnlyList<Product>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken ct = default);
    /// <summary>Any product — deleted ones included, past orders point to them — in one of these categories.</summary>
    Task<bool> AnyInCategoriesAsync(IReadOnlyCollection<Guid> categoryIds, CancellationToken ct = default);
    /// <summary>Products (with variants, not deleted) of these categories, to check which field values they use.</summary>
    Task<IReadOnlyList<Product>> ListInCategoriesAsync(IReadOnlyCollection<Guid> categoryIds, CancellationToken ct = default);
    /// <summary>
    /// Takes <paramref name="quantity"/> units in one conditional update (<c>stock &gt;= quantity</c>), so two checkouts
    /// can never sell the same last unit. False when there is not enough stock left.
    /// </summary>
    Task<bool> TryTakeStockAsync(Guid productId, Guid? variantId, int quantity, CancellationToken ct = default);
    /// <summary>Puts units back (cancelled or returned parcel). A variant removed since is skipped.</summary>
    Task ReturnStockAsync(Guid productId, Guid? variantId, int quantity, CancellationToken ct = default);
    void Add(Product product);
}
