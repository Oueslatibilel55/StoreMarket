using WebApp.Application.DTOs;

namespace WebApp.Application.Interfaces;

/// <summary>Numbers of the admin dashboard and the store owner's dashboard (023). Read only, aggregated in SQL.</summary>
public interface IStatsQueries
{
    Task<AdminOverviewDto> OverviewAsync(StatsPeriod period, CancellationToken ct = default);
    /// <summary>Null when the user is not (and never was) an affiliate.</summary>
    Task<AffiliateStatsDto?> AffiliateAsync(Guid affiliateId, StatsPeriod period, CancellationToken ct = default);
    /// <summary>Null when the store doesn't exist.</summary>
    Task<StoreStatsDto?> StoreAsync(Guid storeId, StatsPeriod period, CancellationToken ct = default);
    /// <summary>Null when the product doesn't exist (deleted products keep their stats).</summary>
    Task<ProductStatsDto?> ProductAsync(Guid productId, StatsPeriod period, CancellationToken ct = default);
}
