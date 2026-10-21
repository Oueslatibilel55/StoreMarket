using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.Services;

/// <summary>
/// Users ask for a role (Buyer, Affiliate, StoreOwner); an admin approves or refuses. Decisions are
/// audited on the user, so they show in the user's admin history. A granted role reaches the
/// client after its next token refresh.
/// </summary>
public sealed class RoleRequestService(
    IRoleRequestRepository requests,
    IUserRepository users,
    AffiliateCodeGenerator codes,
    IAdminQueries queries,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<RoleRequestDto>> CreateAsync(Guid userId, CreateRoleRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user is null) return Result.Fail<RoleRequestDto>(AuthErrors.UserNotFound);
        if (!user.IsActive) return Result.Fail<RoleRequestDto>(DomainErrors.User.Blocked);
        if (user.HasRole(request.Role)) return Result.Fail<RoleRequestDto>(RoleRequestErrors.AlreadyHasRole);

        var created = RoleRequest.Create(userId, request.Role, request.Message, request.AcceptTerms, Now);
        if (!created.IsSuccess) return Result.Fail<RoleRequestDto>(created.Error!);

        if (await requests.HasPendingAsync(userId, request.Role, ct).ConfigureAwait(false))
            return Result.Fail<RoleRequestDto>(RoleRequestErrors.AlreadyPending);

        requests.Add(created.Value);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(RoleRequestDto.From(created.Value));
    }

    public async Task<IReadOnlyList<RoleRequestDto>> ListMineAsync(Guid userId, CancellationToken ct = default) =>
        [.. (await requests.ListForUserAsync(userId, ct).ConfigureAwait(false)).Select(RoleRequestDto.From)];

    public async Task<Result<RoleRequestDto>> CancelAsync(Guid userId, Guid requestId, CancellationToken ct = default)
    {
        var request = await requests.GetByIdAsync(requestId, ct).ConfigureAwait(false);
        // Someone else's request is reported as missing: never reveal that it exists.
        if (request is null || request.UserId != userId) return Result.Fail<RoleRequestDto>(RoleRequestErrors.NotFound);

        var cancelled = request.Cancel(Now);
        if (!cancelled.IsSuccess) return Result.Fail<RoleRequestDto>(cancelled.Error!);

        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(RoleRequestDto.From(request));
    }

    public Task<PagedResult<AdminRoleRequestItem>> ListAsync(RoleRequestListQuery query, CancellationToken ct = default) =>
        queries.ListRoleRequestsAsync(query, ct);

    /// <summary>Approves the request and grants the role. An affiliate gets their code (or their old one back).</summary>
    public async Task<Result<RoleRequestDto>> ApproveAsync(AdminActor actor, Guid requestId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var loaded = await LoadForDecisionAsync(actor, requestId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess) return Result.Fail<RoleRequestDto>(loaded.Error!);
        var (request, user) = loaded.Value;
        if (!user.IsActive) return Result.Fail<RoleRequestDto>(DomainErrors.User.Blocked);

        var before = AdminUserDto.From(user);
        var approved = request.Approve(actor.Id, Now);
        if (!approved.IsSuccess) return Result.Fail<RoleRequestDto>(approved.Error!);

        var granted = request.Role == Role.Affiliate
            ? user.ActivateAffiliate(
                user.AffiliateCode ?? await codes.GenerateAsync(ct).ConfigureAwait(false),
                termsAccepted: request.TermsAcceptedAt is not null,
                request.TermsAcceptedAt ?? Now)
            : user.GrantRole(request.Role);
        if (!granted.IsSuccess) return Result.Fail<RoleRequestDto>(granted.Error!);

        audit.Record(actor.Email, actor.Id, AuditActions.RoleRequestApprove, AuditTargets.User, user.Id, user.Email,
            before, AdminUserDto.From(user), reason: request.Role.ToString());
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(RoleRequestDto.From(request));
    }

    public async Task<Result<RoleRequestDto>> RefuseAsync(AdminActor actor, Guid requestId, RefuseRoleRequest body, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(body);

        var loaded = await LoadForDecisionAsync(actor, requestId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess) return Result.Fail<RoleRequestDto>(loaded.Error!);
        var (request, user) = loaded.Value;

        var refused = request.Refuse(actor.Id, body.Reason, Now);
        if (!refused.IsSuccess) return Result.Fail<RoleRequestDto>(refused.Error!);

        // The user doesn't change: the snapshot is the request itself (role + status).
        audit.Record(actor.Email, actor.Id, AuditActions.RoleRequestRefuse, AuditTargets.User, user.Id, user.Email,
            before: new { request.Role, Status = RoleRequestStatus.Pending },
            after: new { request.Role, request.Status }, reason: request.DecisionReason);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(RoleRequestDto.From(request));
    }

    private async Task<Result<(RoleRequest Request, User User)>> LoadForDecisionAsync(AdminActor actor, Guid requestId, CancellationToken ct)
    {
        var request = await requests.GetByIdAsync(requestId, ct).ConfigureAwait(false);
        if (request is null) return Result.Fail<(RoleRequest, User)>(RoleRequestErrors.NotFound);

        // Same rule as every admin action: an admin never decides about their own account.
        if (request.UserId == actor.Id) return Result.Fail<(RoleRequest, User)>(AdminUserErrors.CannotChangeOwnAccount);

        var user = await users.GetByIdAsync(request.UserId, ct).ConfigureAwait(false);
        return user is null
            ? Result.Fail<(RoleRequest, User)>(AuthErrors.UserNotFound)
            : Result.Ok((request, user));
    }

    private DateTime Now => clock.GetUtcNow().UtcDateTime;
}
