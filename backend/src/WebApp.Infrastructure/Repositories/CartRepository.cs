using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Repositories;

internal sealed class CartRepository(AppDbContext db) : ICartRepository
{
    public Task<Cart?> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        db.Carts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.UserId == userId, ct);

    public void Add(Cart cart) => db.Carts.Add(cart);
}
