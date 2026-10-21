using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default);
    Task<bool> AffiliateCodeExistsAsync(string code, CancellationToken ct = default);
    /// <summary>The user holding that code (canonical form), whatever their status or roles.</summary>
    Task<User?> GetByAffiliateCodeAsync(string code, CancellationToken ct = default);
    void Add(User user);
}
