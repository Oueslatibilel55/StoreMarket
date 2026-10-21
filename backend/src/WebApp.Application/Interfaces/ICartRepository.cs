using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

public interface ICartRepository
{
    /// <summary>The user's cart with its lines (tracked), or null when they never added anything.</summary>
    Task<Cart?> GetByUserAsync(Guid userId, CancellationToken ct = default);
    void Add(Cart cart);
}
