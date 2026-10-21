using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

public interface IOrderRepository
{
    /// <summary>Next order number (database sequence: unique and increasing, gaps allowed).</summary>
    Task<long> NextNumberAsync(CancellationToken ct = default);
    /// <summary>The order holding this store order, with all its store orders, lines and history (tracked).</summary>
    Task<Order?> GetByStoreOrderIdAsync(Guid storeOrderId, CancellationToken ct = default);
    void Add(Order order);
}
