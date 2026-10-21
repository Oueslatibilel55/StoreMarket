using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.Services;

/// <summary>Admin actions on users. Every change is written to the audit log in the same transaction.</summary>
public sealed class AdminUserService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IStoreRepository stores,
    IAdminQueries queries,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public Task<PagedResult<AdminUserListItem>> ListAsync(UserListQuery query, CancellationToken ct = default) =>
        queries.ListUsersAsync(query, ct);

    public Task<UserCounts> CountsAsync(CancellationToken ct = default) => queries.CountUsersAsync(ct);

    public async Task<Result<AdminUserDto>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct).ConfigureAwait(false);
        return user is null ? Result.Fail<AdminUserDto>(AuthErrors.UserNotFound) : Result.Ok(await WithStoreAsync(AdminUserDto.From(user), ct).ConfigureAwait(false));
    }

    /// <summary>Adds the store the user owns, so the admin sees at once which store an owner runs.</summary>
    private async Task<AdminUserDto> WithStoreAsync(AdminUserDto user, CancellationToken ct) =>
        await stores.GetByOwnerAsync(user.Id, ct).ConfigureAwait(false) is { IsDeleted: false } store
            ? user with { Store = new AdminUserStore(store.Id, store.Name, store.Slug, store.Status) }
            : user;

    public Task<Result<AdminUserDto>> GrantRoleAsync(AdminActor actor, Guid userId, Role role, CancellationToken ct = default) =>
        ChangeAsync(actor, userId, AuditActions.UserRoleGrant, reason: role.ToString(),
            user => Enum.IsDefined(role) ? user.GrantRole(role) : Result.Fail(AdminUserErrors.UnknownRole), ct);

    /// <summary>
    /// Revoking StoreOwner also disables the user's store (010): nobody could manage it any more. Giving the
    /// role back doesn't re-enable it — the admin does that explicitly.
    /// </summary>
    public Task<Result<AdminUserDto>> RevokeRoleAsync(AdminActor actor, Guid userId, Role role, CancellationToken ct = default) =>
        ChangeAsync(actor, userId, AuditActions.UserRoleRevoke, reason: role.ToString(),
            user => Enum.IsDefined(role) ? user.RevokeRole(role) : Result.Fail(AdminUserErrors.UnknownRole), ct,
            async user =>
            {
                if (role != Role.StoreOwner) return;
                var store = await stores.GetByOwnerAsync(user.Id, ct).ConfigureAwait(false);
                if (store is null || store.IsDeleted || store.Status == StoreStatus.Disabled) return;

                var before = ModerationService.Snapshot(store);
                store.Disable(Store.OwnerRoleRevokedReason, clock.GetUtcNow().UtcDateTime);
                audit.Record(actor.Email, actor.Id, AuditActions.StoreDisable, AuditTargets.Store, store.Id, store.Name,
                    before, ModerationService.Snapshot(store), Store.OwnerRoleRevokedReason);
            });

    /// <summary>Blocks the user and signs them out everywhere (their access token dies within 15 min).</summary>
    public Task<Result<AdminUserDto>> BlockAsync(AdminActor actor, Guid userId, BlockUserRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.GetUtcNow().UtcDateTime;

        return ChangeAsync(actor, userId, AuditActions.UserBlock, request.Reason, user => user.Block(request.Reason, now), ct,
            async user =>
            {
                foreach (var token in await refreshTokens.GetActiveForUserAsync(user.Id, ct).ConfigureAwait(false))
                    token.Revoke(now);
            });
    }

    /// <summary>Deactivates the affiliate code (fraud): refused on every product until re-enabled. Partnerships stay.</summary>
    public Task<Result<AdminUserDto>> DisableAffiliateCodeAsync(AdminActor actor, Guid userId, DisableAffiliateCodeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.GetUtcNow().UtcDateTime;
        return ChangeAsync(actor, userId, AuditActions.UserAffiliateCodeDisable, request.Reason, user => user.DisableAffiliateCode(request.Reason, now), ct);
    }

    public Task<Result<AdminUserDto>> EnableAffiliateCodeAsync(AdminActor actor, Guid userId, CancellationToken ct = default) =>
        ChangeAsync(actor, userId, AuditActions.UserAffiliateCodeEnable, reason: null, user =>
        {
            user.EnableAffiliateCode();
            return Result.Ok();
        }, ct);

    public Task<Result<AdminUserDto>> UnblockAsync(AdminActor actor, Guid userId, CancellationToken ct = default) =>
        ChangeAsync(actor, userId, AuditActions.UserUnblock, reason: null, user =>
        {
            user.Unblock();
            return Result.Ok();
        }, ct);

    private async Task<Result<AdminUserDto>> ChangeAsync(
        AdminActor actor, Guid userId, string action, string? reason, Func<User, Result> change, CancellationToken ct,
        Func<User, Task>? afterChange = null)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // An admin can't lock themselves out (remove their own Admin role, block themselves).
        if (actor.Id == userId) return Result.Fail<AdminUserDto>(AdminUserErrors.CannotChangeOwnAccount);

        var user = await users.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user is null) return Result.Fail<AdminUserDto>(AuthErrors.UserNotFound);

        var before = AdminUserDto.From(user);
        var changed = change(user);
        if (!changed.IsSuccess) return Result.Fail<AdminUserDto>(changed.Error!);

        var after = AdminUserDto.From(user);
        if (before.Roles.SequenceEqual(after.Roles) && before.Status == after.Status
            && before.AffiliateCodeDisabledAt == after.AffiliateCodeDisabledAt)
            return Result.Ok(await WithStoreAsync(after, ct).ConfigureAwait(false)); // no-op (role already granted, already active…): nothing to save or audit

        if (afterChange is not null) await afterChange(user).ConfigureAwait(false);
        audit.Record(actor.Email, actor.Id, action, AuditTargets.User, user.Id, user.Email, before, after, reason);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(await WithStoreAsync(after, ct).ConfigureAwait(false));
    }
}

public sealed class AuditService(IAdminQueries queries)
{
    public Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, CancellationToken ct = default) =>
        queries.ListAuditAsync(query, ct);
}
