using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.Services;

/// <summary>
/// Affiliate ↔ store partnerships (012). The affiliate asks, only the owner of that store accepts,
/// refuses or ends (owner id from the token, never from the body). Owner decisions are not admin
/// actions, so they are not audited; an admin ending a partnership for fraud is.
/// </summary>
public sealed class PartnershipService(
    IPartnershipRepository partnerships,
    IPartnershipQueries queries,
    IAdminQueries adminQueries,
    IUserRepository users,
    IStoreRepository stores,
    ICatalogQueries catalog,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    // -- Affiliate ----------------------------------------------------------------

    public Task<IReadOnlyList<AffiliatePartnershipDto>> ListMineAsync(Guid affiliateId, CancellationToken ct = default) =>
        queries.ListForAffiliateAsync(affiliateId, ct);

    public async Task<Result<AffiliatePartnershipDto>> RequestAsync(Guid affiliateId, CreatePartnershipRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var affiliate = await users.GetByIdAsync(affiliateId, ct).ConfigureAwait(false);
        if (affiliate is null) return Result.Fail<AffiliatePartnershipDto>(AuthErrors.UserNotFound);
        if (!affiliate.CanEarnAsAffiliate) return Result.Fail<AffiliatePartnershipDto>(PartnershipErrors.AffiliateInactive);

        var store = await stores.GetByIdAsync(request.StoreId, ct).ConfigureAwait(false);
        if (store is null || !await catalog.IsStoreVisibleAsync(store.Id, ct).ConfigureAwait(false))
            return Result.Fail<AffiliatePartnershipDto>(PartnershipErrors.StoreUnavailable);
        // No self-affiliation: an affiliate never earns on their own store.
        if (store.OwnerId == affiliateId) return Result.Fail<AffiliatePartnershipDto>(PartnershipErrors.OwnStore);

        if (await partnerships.GetOpenAsync(affiliateId, store.Id, ct).ConfigureAwait(false) is not null)
            return Result.Fail<AffiliatePartnershipDto>(PartnershipErrors.AlreadyOpen);

        var created = Partnership.Request(affiliateId, store.Id, request.Message);
        if (!created.IsSuccess) return Result.Fail<AffiliatePartnershipDto>(created.Error!);

        partnerships.Add(created.Value);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return await MineAsync(affiliateId, created.Value.Id, ct).ConfigureAwait(false);
    }

    public async Task<Result<AffiliatePartnershipDto>> CancelAsync(Guid affiliateId, Guid id, CancellationToken ct = default)
    {
        var partnership = await partnerships.GetByIdAsync(id, ct).ConfigureAwait(false);
        // Someone else's partnership is reported as missing: never reveal that it exists.
        if (partnership is null || partnership.AffiliateId != affiliateId)
            return Result.Fail<AffiliatePartnershipDto>(PartnershipErrors.NotFound);

        var cancelled = partnership.Cancel(Now);
        if (!cancelled.IsSuccess) return Result.Fail<AffiliatePartnershipDto>(cancelled.Error!);

        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return await MineAsync(affiliateId, id, ct).ConfigureAwait(false);
    }

    // -- Store owner --------------------------------------------------------------

    public async Task<Result<IReadOnlyList<StorePartnershipDto>>> ListForMyStoreAsync(Guid ownerId, PartnershipStatus? status, CancellationToken ct = default)
    {
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        return store is null
            ? Result.Fail<IReadOnlyList<StorePartnershipDto>>(StoreErrors.NotFound)
            : Result.Ok(await queries.ListForStoreAsync(store.Id, status, ct).ConfigureAwait(false));
    }

    public Task<Result<StorePartnershipDto>> AcceptAsync(Guid ownerId, Guid id, CancellationToken ct = default) =>
        DecideAsync(ownerId, id, (p, _) => p.Accept(Now), ct);

    public Task<Result<StorePartnershipDto>> RefuseAsync(Guid ownerId, Guid id, PartnershipReasonRequest body, CancellationToken ct = default) =>
        DecideAsync(ownerId, id, (p, _) => p.Refuse(body?.Reason, Now), ct);

    public Task<Result<StorePartnershipDto>> EndAsync(Guid ownerId, Guid id, PartnershipReasonRequest body, CancellationToken ct = default) =>
        DecideAsync(ownerId, id, (p, owner) => p.End(owner, body?.Reason, Now), ct);

    private async Task<Result<StorePartnershipDto>> DecideAsync(Guid ownerId, Guid id, Func<Partnership, Guid, Result> decide, CancellationToken ct)
    {
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<StorePartnershipDto>(StoreErrors.NotFound);
        if (store.IsDeleted) return Result.Fail<StorePartnershipDto>(DomainErrors.Store.Deleted);

        var partnership = await partnerships.GetByIdAsync(id, ct).ConfigureAwait(false);
        // Only the owner of that store decides; another store's partnership doesn't exist for them.
        if (partnership is null || partnership.StoreId != store.Id) return Result.Fail<StorePartnershipDto>(PartnershipErrors.NotFound);

        var decided = decide(partnership, ownerId);
        if (!decided.IsSuccess) return Result.Fail<StorePartnershipDto>(decided.Error!);

        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return await queries.GetForStoreAsync(store.Id, id, ct).ConfigureAwait(false) is { } dto
            ? Result.Ok(dto)
            : Result.Fail<StorePartnershipDto>(PartnershipErrors.NotFound);
    }

    // -- Admin --------------------------------------------------------------------

    public Task<PagedResult<AdminPartnershipItem>> ListAsync(AdminPartnershipQuery query, CancellationToken ct = default) =>
        adminQueries.ListPartnershipsAsync(query, ct);

    /// <summary>An admin ends a partnership (fraud). The reason is required and the action is audited on the store.</summary>
    public async Task<Result<AdminPartnershipItem>> AdminEndAsync(AdminActor actor, Guid id, PartnershipReasonRequest body, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var reason = body?.Reason?.Trim();
        if (string.IsNullOrEmpty(reason)) return Result.Fail<AdminPartnershipItem>(DomainErrors.Moderation.ReasonRequired);

        var partnership = await partnerships.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (partnership is null) return Result.Fail<AdminPartnershipItem>(PartnershipErrors.NotFound);
        var store = await stores.GetByIdAsync(partnership.StoreId, ct).ConfigureAwait(false);
        var affiliate = await users.GetByIdAsync(partnership.AffiliateId, ct).ConfigureAwait(false);
        if (store is null || affiliate is null) return Result.Fail<AdminPartnershipItem>(PartnershipErrors.NotFound);

        var before = Snapshot(partnership, affiliate);
        var ended = partnership.End(actor.Id, reason, Now);
        if (!ended.IsSuccess) return Result.Fail<AdminPartnershipItem>(ended.Error!);

        audit.Record(actor.Email, actor.Id, AuditActions.PartnershipEnd, AuditTargets.Store, store.Id, store.Name,
            before, Snapshot(partnership, affiliate), reason);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return await adminQueries.GetPartnershipAsync(id, ct).ConfigureAwait(false) is { } dto
            ? Result.Ok(dto)
            : Result.Fail<AdminPartnershipItem>(PartnershipErrors.NotFound);
    }

    private static object Snapshot(Partnership p, User affiliate) =>
        new { Affiliate = affiliate.Email, affiliate.AffiliateCode, p.Status };

    private async Task<Result<AffiliatePartnershipDto>> MineAsync(Guid affiliateId, Guid id, CancellationToken ct) =>
        await queries.GetForAffiliateAsync(affiliateId, id, ct).ConfigureAwait(false) is { } dto
            ? Result.Ok(dto)
            : Result.Fail<AffiliatePartnershipDto>(PartnershipErrors.NotFound);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;
}
