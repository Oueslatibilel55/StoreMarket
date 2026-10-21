using WebApp.Application.DTOs;

namespace WebApp.Application.Interfaces;

/// <summary>Read-only order views (no tracking, projections only).</summary>
public interface IOrderQueries
{
    /// <summary>Orders the user placed — as a client, or for clients in affiliate mode — newest first.</summary>
    Task<PagedResult<OrderListItem>> ListForUserAsync(Guid userId, OrderListQuery query, CancellationToken ct = default);
    /// <summary>An order the user placed, by reference, or null.</summary>
    Task<OrderDto?> GetForUserAsync(Guid userId, string reference, CancellationToken ct = default);
    /// <summary>A guest finds their order with its reference and the phone number given at checkout.</summary>
    Task<OrderDto?> GetByReferenceAndPhoneAsync(string reference, string normalizedPhone, CancellationToken ct = default);
    /// <summary>Lines carrying the affiliate's code, newest first, with totals.</summary>
    Task<AffiliateOrdersDto> ListForAffiliateAsync(Guid affiliateId, OrderListQuery query, CancellationToken ct = default);

    // -- Store owner (016)
    Task<PagedResult<StoreOrderListItem>> ListForStoreAsync(Guid storeId, StoreOrderQuery query, CancellationToken ct = default);
    Task<StoreOrderCounts> CountForStoreAsync(Guid storeId, CancellationToken ct = default);
    Task<StoreOrderDetailDto?> GetForStoreAsync(Guid storeId, Guid storeOrderId, CancellationToken ct = default);

    // -- Admin (016)
    Task<PagedResult<AdminOrderListItem>> ListForAdminAsync(AdminOrderQuery query, CancellationToken ct = default);
    Task<OrderDto?> GetForAdminAsync(string reference, CancellationToken ct = default);
}
