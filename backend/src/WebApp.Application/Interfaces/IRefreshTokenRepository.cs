using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveInFamilyAsync(Guid familyId, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(Guid userId, CancellationToken ct = default);
    void Add(RefreshToken token);
}
