using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Repositories;

internal sealed class RoleRequestRepository(AppDbContext db) : IRoleRequestRepository
{
    public Task<RoleRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.RoleRequests.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> HasPendingAsync(Guid userId, Role role, CancellationToken ct = default) =>
        db.RoleRequests.AnyAsync(r => r.UserId == userId && r.Role == role && r.Status == RoleRequestStatus.Pending, ct);

    public async Task<IReadOnlyList<RoleRequest>> ListForUserAsync(Guid userId, CancellationToken ct = default) =>
        await db.RoleRequests.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);

    public void Add(RoleRequest request) => db.RoleRequests.Add(request);
}
