using WebApp.Application.DTOs;

namespace WebApp.Application.Interfaces;

/// <summary>Read-only queries for the admin panel (no tracking, projections only).</summary>
public interface IAdminQueries
{
    Task<PagedResult<AdminUserListItem>> ListUsersAsync(UserListQuery query, CancellationToken ct = default);
    Task<UserCounts> CountUsersAsync(CancellationToken ct = default);
    Task<PagedResult<AuditEntryDto>> ListAuditAsync(AuditQuery query, CancellationToken ct = default);
    Task<PagedResult<AdminRoleRequestItem>> ListRoleRequestsAsync(RoleRequestListQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<StoredSetting>> ListStoredSettingsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminStoreCategoryDto>> ListStoreCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminProductCategoryDto>> ListProductCategoriesAsync(CancellationToken ct = default);
    Task<PagedResult<AdminStoreListItem>> ListStoresAsync(AdminStoreQuery query, CancellationToken ct = default);
    Task<AdminStoreDto?> GetStoreAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AdminProductListItem>> ListProductsAsync(AdminProductQuery query, CancellationToken ct = default);
    Task<PagedResult<AdminPartnershipItem>> ListPartnershipsAsync(AdminPartnershipQuery query, CancellationToken ct = default);
    Task<AdminPartnershipItem?> GetPartnershipAsync(Guid id, CancellationToken ct = default);
}
