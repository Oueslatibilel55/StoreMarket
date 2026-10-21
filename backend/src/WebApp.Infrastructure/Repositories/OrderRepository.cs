using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Infrastructure.Persistence;
using WebApp.Infrastructure.Persistence.Configurations;

namespace WebApp.Infrastructure.Repositories;

internal sealed class OrderRepository(AppDbContext db) : IOrderRepository
{
    public async Task<long> NextNumberAsync(CancellationToken ct = default) =>
        await db.Database
            .SqlQueryRaw<long>($"SELECT nextval('{OrderConfiguration.NumberSequence}') AS \"Value\"")
            .SingleAsync(ct).ConfigureAwait(false);

    public Task<Order?> GetByStoreOrderIdAsync(Guid storeOrderId, CancellationToken ct = default) =>
        db.Orders
            .Include(o => o.StoreOrders).ThenInclude(s => s.Lines)
            .Include(o => o.StoreOrders).ThenInclude(s => s.History)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.StoreOrders.Any(s => s.Id == storeOrderId), ct);

    public void Add(Order order) => db.Orders.Add(order);
}
