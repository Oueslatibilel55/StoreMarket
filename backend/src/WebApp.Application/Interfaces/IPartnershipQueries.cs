using WebApp.Application.DTOs;
using WebApp.Domain.Enums;

namespace WebApp.Application.Interfaces;

/// <summary>Read-only partnership lists for affiliates and store owners (no tracking, projections only).</summary>
public interface IPartnershipQueries
{
    /// <summary>Every request and partnership of the affiliate, newest first.</summary>
    Task<IReadOnlyList<AffiliatePartnershipDto>> ListForAffiliateAsync(Guid affiliateId, CancellationToken ct = default);
    Task<AffiliatePartnershipDto?> GetForAffiliateAsync(Guid affiliateId, Guid id, CancellationToken ct = default);
    /// <summary>The store's partnerships: waiting requests oldest first, the others newest first.</summary>
    Task<IReadOnlyList<StorePartnershipDto>> ListForStoreAsync(Guid storeId, PartnershipStatus? status, CancellationToken ct = default);
    Task<StorePartnershipDto?> GetForStoreAsync(Guid storeId, Guid id, CancellationToken ct = default);
}
