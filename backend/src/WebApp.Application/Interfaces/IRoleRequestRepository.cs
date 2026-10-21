using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.Interfaces;

public interface IRoleRequestRepository
{
    Task<RoleRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasPendingAsync(Guid userId, Role role, CancellationToken ct = default);
    /// <summary>The user's own requests, newest first.</summary>
    Task<IReadOnlyList<RoleRequest>> ListForUserAsync(Guid userId, CancellationToken ct = default);
    void Add(RoleRequest request);
}
