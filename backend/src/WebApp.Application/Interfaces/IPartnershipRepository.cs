using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

public interface IPartnershipRepository
{
    Task<Partnership?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>The requested or accepted partnership between the affiliate and the store, if any.</summary>
    Task<Partnership?> GetOpenAsync(Guid affiliateId, Guid storeId, CancellationToken ct = default);
    /// <summary>The accepted partnership between the affiliate and the store, if any.</summary>
    Task<Guid?> GetActiveIdAsync(Guid affiliateId, Guid storeId, CancellationToken ct = default);
    void Add(Partnership partnership);
}
