using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.Services;

/// <summary>
/// Admin moderation of stores and products (010): there is no approval step, the admin disables or
/// soft-deletes fakes afterwards, always with a reason. Every change is audited in the same transaction.
/// </summary>
public sealed class ModerationService(
    IStoreRepository stores,
    IProductRepository products,
    IUserRepository users,
    IAdminQueries queries,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public Task<PagedResult<AdminStoreListItem>> ListStoresAsync(AdminStoreQuery query, CancellationToken ct = default) =>
        queries.ListStoresAsync(query, ct);

    public async Task<Result<AdminStoreDto>> GetStoreAsync(Guid id, CancellationToken ct = default) =>
        await queries.GetStoreAsync(id, ct).ConfigureAwait(false) is { } store
            ? Result.Ok(store)
            : Result.Fail<AdminStoreDto>(StoreErrors.NotFound);

    public Task<PagedResult<AdminProductListItem>> ListProductsAsync(AdminProductQuery query, CancellationToken ct = default) =>
        queries.ListProductsAsync(query, ct);

    public Task<Result<AdminStoreDto>> DisableStoreAsync(AdminActor actor, Guid id, ModerationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.GetUtcNow().UtcDateTime;
        return ChangeStoreAsync(actor, id, AuditActions.StoreDisable, request.Reason, s => Task.FromResult(s.Disable(request.Reason, now)), ct);
    }

    /// <summary>Re-enabling needs an owner who still has the StoreOwner role (a revoked owner's store stays off).</summary>
    public Task<Result<AdminStoreDto>> EnableStoreAsync(AdminActor actor, Guid id, CancellationToken ct = default) =>
        ChangeStoreAsync(actor, id, AuditActions.StoreEnable, reason: null, async s =>
        {
            if (s.Status == StoreStatus.Disabled && !s.IsDeleted)
            {
                var owner = await users.GetByIdAsync(s.OwnerId, ct).ConfigureAwait(false);
                if (owner is null || !owner.HasRole(Role.StoreOwner)) return Result.Fail(StoreErrors.OwnerNotStoreOwner);
            }
            return s.Enable();
        }, ct);

    public Task<Result<AdminStoreDto>> DeleteStoreAsync(AdminActor actor, Guid id, ModerationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.GetUtcNow().UtcDateTime;
        return ChangeStoreAsync(actor, id, AuditActions.StoreDelete, request.Reason, s => Task.FromResult(s.Delete(request.Reason, now)), ct);
    }

    public Task<Result> DisableProductAsync(AdminActor actor, Guid id, ModerationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.GetUtcNow().UtcDateTime;
        return ChangeProductAsync(actor, id, AuditActions.ProductDisable, request.Reason, p => p.Disable(request.Reason, now), ct);
    }

    public Task<Result> EnableProductAsync(AdminActor actor, Guid id, CancellationToken ct = default) =>
        ChangeProductAsync(actor, id, AuditActions.ProductEnable, reason: null, p => p.Enable(), ct);

    public Task<Result> DeleteProductAsync(AdminActor actor, Guid id, ModerationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.GetUtcNow().UtcDateTime;
        return ChangeProductAsync(actor, id, AuditActions.ProductDelete, request.Reason, p => p.DeleteByAdmin(request.Reason, now), ct);
    }

    /// <summary>Disables every listed product that isn't already disabled or deleted; one audit line each, one transaction.</summary>
    public async Task<Result<BulkModerationResult>> DisableProductsAsync(AdminActor actor, BulkModerationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var ids = (request.Ids ?? []).Distinct().ToList();
        if (ids.Count is 0 or > BulkModerationRequest.MaxIds) return Result.Fail<BulkModerationResult>(ModerationErrors.SelectionInvalid);
        // Checked once up front, so a bad reason never leaves half of the selection disabled.
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > Store.MaxModerationReasonLength)
            return Result.Fail<BulkModerationResult>(DomainErrors.Moderation.ReasonRequired);

        var now = clock.GetUtcNow().UtcDateTime;
        var changed = 0;
        foreach (var product in await products.ListByIdsAsync(ids, ct).ConfigureAwait(false))
        {
            if (product.IsDeleted || product.IsDisabled) continue;
            var before = Snapshot(product);
            var disabled = product.Disable(request.Reason, now);
            if (!disabled.IsSuccess) return Result.Fail<BulkModerationResult>(disabled.Error!);
            audit.Record(actor.Email, actor.Id, AuditActions.ProductDisable, AuditTargets.Product, product.Id, product.Name,
                before, Snapshot(product), request.Reason);
            changed++;
        }

        if (changed > 0) await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(new BulkModerationResult(changed));
    }

    private async Task<Result<AdminStoreDto>> ChangeStoreAsync(
        AdminActor actor, Guid id, string action, string? reason, Func<Store, Task<Result>> change, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var store = await stores.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<AdminStoreDto>(StoreErrors.NotFound);

        var before = Snapshot(store);
        var changed = await change(store).ConfigureAwait(false);
        if (!changed.IsSuccess) return Result.Fail<AdminStoreDto>(changed.Error!);

        var after = Snapshot(store);
        if (before != after) // no-op (already disabled…): nothing to save or audit
        {
            audit.Record(actor.Email, actor.Id, action, AuditTargets.Store, store.Id, store.Name, before, after, reason);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        }
        return await GetStoreAsync(id, ct).ConfigureAwait(false);
    }

    private async Task<Result> ChangeProductAsync(
        AdminActor actor, Guid id, string action, string? reason, Func<Product, Result> change, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var found = await products.ListByIdsAsync([id], ct).ConfigureAwait(false);
        if (found.Count == 0) return Result.Fail(ProductErrors.NotFound);
        var product = found[0];

        var before = Snapshot(product);
        var changed = change(product);
        if (!changed.IsSuccess) return changed;

        var after = Snapshot(product);
        if (before == after) return Result.Ok();
        audit.Record(actor.Email, actor.Id, action, AuditTargets.Product, product.Id, product.Name, before, after, reason);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok();
    }

    internal static StoreModerationSnapshot Snapshot(Store s) => new(s.Status, s.IsDeleted, s.ModerationReason);

    private static ProductModerationSnapshot Snapshot(Product p) => new(p.Name, p.Price, p.Status, p.IsDisabled, p.IsDeleted, p.ModerationReason);
}
