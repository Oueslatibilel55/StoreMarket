using WebApp.Application.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.Services;

/// <summary>
/// After checkout (016): the store confirms, ships and marks its parcel delivered (cash collected, 015) or not
/// delivered; the client can cancel before shipping; an admin can force a status (audited). The rules of who may
/// do what live in <see cref="Order.ChangeStatus"/>. A cancelled or returned parcel puts its stock back.
/// </summary>
public sealed class OrderLifecycleService(
    IOrderRepository orders,
    IOrderQueries queries,
    IStoreRepository stores,
    IProductRepository products,
    IEnumerable<IPaymentMethod> paymentMethods,
    CoinLedgerService coins,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    // -- Store owner ----------------------------------------------------------------

    public async Task<Result<PagedResult<StoreOrderListItem>>> ListForMyStoreAsync(Guid ownerId, StoreOrderQuery query, CancellationToken ct = default) =>
        await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false) is { } store
            ? Result.Ok(await queries.ListForStoreAsync(store.Id, query, ct).ConfigureAwait(false))
            : Result.Fail<PagedResult<StoreOrderListItem>>(StoreErrors.NotFound);

    public async Task<Result<StoreOrderCounts>> CountForMyStoreAsync(Guid ownerId, CancellationToken ct = default) =>
        await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false) is { } store
            ? Result.Ok(await queries.CountForStoreAsync(store.Id, ct).ConfigureAwait(false))
            : Result.Fail<StoreOrderCounts>(StoreErrors.NotFound);

    public async Task<Result<StoreOrderDetailDto>> GetForMyStoreAsync(Guid ownerId, Guid storeOrderId, CancellationToken ct = default)
    {
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<StoreOrderDetailDto>(StoreErrors.NotFound);
        return await queries.GetForStoreAsync(store.Id, storeOrderId, ct).ConfigureAwait(false) is { } order
            ? Result.Ok(order)
            : Result.Fail<StoreOrderDetailDto>(DomainErrors.Order.StoreOrderNotFound);
    }

    /// <summary>
    /// The owner moves one of their store's orders. A suspended or disabled store keeps processing the orders it
    /// already has (025); only a deleted store can't.
    /// </summary>
    public async Task<Result<StoreOrderDetailDto>> ChangeAsStoreAsync(Guid ownerId, Guid storeOrderId, ChangeStoreOrderStatusRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<StoreOrderDetailDto>(StoreErrors.NotFound);
        if (store.IsDeleted) return Result.Fail<StoreOrderDetailDto>(DomainErrors.Store.Deleted);

        var order = await orders.GetByStoreOrderIdAsync(storeOrderId, ct).ConfigureAwait(false);
        // Another store's order doesn't exist for this owner.
        if (order is null || order.StoreOrders.First(s => s.Id == storeOrderId).StoreId != store.Id)
            return Result.Fail<StoreOrderDetailDto>(DomainErrors.Order.StoreOrderNotFound);

        var changed = await ApplyAsync(order, storeOrderId, request.Status, OrderActorRole.Store, ownerId, request.Reason, request.NotDeliveredReason, ct)
            .ConfigureAwait(false);
        if (!changed.IsSuccess) return Result.Fail<StoreOrderDetailDto>(changed.Error!);
        return Result.Ok((await queries.GetForStoreAsync(store.Id, storeOrderId, ct).ConfigureAwait(false))!);
    }

    // -- Client ---------------------------------------------------------------------

    /// <summary>The client who placed the order (or the affiliate who placed it for them) cancels one store's part before it ships.</summary>
    public async Task<Result<OrderDto>> CancelAsClientAsync(Guid userId, string reference, Guid storeOrderId, CancelStoreOrderRequest request, CancellationToken ct = default)
    {
        var order = await orders.GetByStoreOrderIdAsync(storeOrderId, ct).ConfigureAwait(false);
        if (order is null || !string.Equals(order.Reference, reference, StringComparison.OrdinalIgnoreCase)
            || (order.ClientId != userId && order.PlacedByAffiliateId != userId))
            return Result.Fail<OrderDto>(CheckoutErrors.OrderNotFound);

        var changed = await ApplyAsync(order, storeOrderId, StoreOrderStatus.Cancelled, OrderActorRole.Client, userId, request?.Reason, null, ct)
            .ConfigureAwait(false);
        if (!changed.IsSuccess) return Result.Fail<OrderDto>(changed.Error!);
        return Result.Ok((await queries.GetForUserAsync(userId, order.Reference, ct).ConfigureAwait(false))!);
    }

    // -- Admin ----------------------------------------------------------------------

    public Task<PagedResult<AdminOrderListItem>> ListAsync(AdminOrderQuery query, CancellationToken ct = default) =>
        queries.ListForAdminAsync(query, ct);

    public async Task<Result<OrderDto>> GetAsync(string reference, CancellationToken ct = default) =>
        await queries.GetForAdminAsync(reference.Trim().ToUpperInvariant(), ct).ConfigureAwait(false) is { } order
            ? Result.Ok(order)
            : Result.Fail<OrderDto>(CheckoutErrors.OrderNotFound);

    /// <summary>An admin forces a status (dispute, store not answering…). Reason required; audited on the order. Delivered stays final.</summary>
    public async Task<Result<OrderDto>> ForceAsync(AdminActor actor, Guid storeOrderId, ChangeStoreOrderStatusRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var order = await orders.GetByStoreOrderIdAsync(storeOrderId, ct).ConfigureAwait(false);
        if (order is null) return Result.Fail<OrderDto>(DomainErrors.Order.StoreOrderNotFound);

        var storeOrder = order.StoreOrders.First(s => s.Id == storeOrderId);
        var before = new { storeOrder.StoreName, storeOrder.Status, storeOrder.PaymentStatus };
        var changed = await ApplyAsync(order, storeOrderId, request.Status, OrderActorRole.Admin, actor.Id, request.Reason, request.NotDeliveredReason, ct,
            beforeCommit: () => audit.Record(actor.Email, actor.Id, AuditActions.OrderForceStatus, AuditTargets.Order, order.Id, order.Reference,
                before, new { storeOrder.StoreName, storeOrder.Status, storeOrder.PaymentStatus }, request.Reason)).ConfigureAwait(false);
        if (!changed.IsSuccess) return Result.Fail<OrderDto>(changed.Error!);
        return Result.Ok((await queries.GetForAdminAsync(order.Reference, ct).ConfigureAwait(false))!);
    }

    // -- Shared ---------------------------------------------------------------------

    /// <summary>
    /// Changes the status and its side effects in one transaction: payment on delivery (015), stock back when a
    /// parcel won't be delivered, stock taken again when an admin re-opens one. A concurrent change → <c>order.changed</c>.
    /// </summary>
    private async Task<Result<StoreOrder>> ApplyAsync(Order order, Guid storeOrderId, StoreOrderStatus to, OrderActorRole role, Guid? actorId,
        string? reason, NotDeliveredReason? notDeliveredReason, CancellationToken ct, Action? beforeCommit = null)
    {
        var from = order.StoreOrders.First(s => s.Id == storeOrderId).Status;
        var now = clock.GetUtcNow().UtcDateTime;
        try
        {
            return await unitOfWork.InTransactionAsync(async () =>
            {
                var changed = order.ChangeStatus(storeOrderId, to, role, actorId, reason, notDeliveredReason, now);
                if (!changed.IsSuccess) return changed;
                var storeOrder = changed.Value;

                if (to == StoreOrderStatus.Delivered)
                {
                    paymentMethods.First(p => p.Method == storeOrder.PaymentMethod).OnDelivered(storeOrder, now);
                    // Same transaction as the status: a delivered affiliate line never misses its coins (019).
                    await coins.CreditDeliveredAsync(order, storeOrder, ct).ConfigureAwait(false);
                }

                var wasHeld = !IsReleased(from);
                if (wasHeld && IsReleased(to))
                {
                    foreach (var line in storeOrder.Lines)
                        await products.ReturnStockAsync(line.ProductId, line.VariantId, line.Quantity, ct).ConfigureAwait(false);
                }
                else if (!wasHeld && !IsReleased(to))
                {
                    foreach (var line in storeOrder.Lines)
                    {
                        if (!await products.TryTakeStockAsync(line.ProductId, line.VariantId, line.Quantity, ct).ConfigureAwait(false))
                            return Result.Fail<StoreOrder>(CartErrors.NotEnoughStock);
                    }
                }

                beforeCommit?.Invoke();
                await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
                return changed;
            }, ct).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Fail<StoreOrder>(OrderErrors.Changed);
        }
    }

    /// <summary>Statuses whose parcel no longer holds stock.</summary>
    private static bool IsReleased(StoreOrderStatus status) => status is StoreOrderStatus.Cancelled or StoreOrderStatus.NotDelivered;
}
