using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Api.Endpoints;

/// <summary>Admin panel API: users and audit log. Every change is audited.</summary>
internal static class AdminUserEndpoints
{
    public static IEndpointRouteBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(AuthPolicies.Admin);

        var users = admin.MapGroup("/users").WithTags("Admin · Users");
        users.MapGet("/", List).Produces<PagedResult<AdminUserListItem>>()
            .WithSummary("Search users (email, name, phone, affiliate code), filter by role / status, paged");
        users.MapGet("/counts", Counts).Produces<UserCounts>().WithSummary("User totals by status and role");
        users.MapGet("/{id:guid}", Get).Produces<AdminUserDto>().WithSummary("User detail");
        users.MapPost("/{id:guid}/roles/{role}", Grant).Produces<AdminUserDto>().WithSummary("Grant a role (not Affiliate: it comes from sign-up or an approved role request)");
        users.MapDelete("/{id:guid}/roles/{role}", Revoke).Produces<AdminUserDto>().WithSummary("Revoke a role (not the last base role, Buyer or Affiliate)");
        users.MapPost("/{id:guid}/block", Block).Produces<AdminUserDto>().WithSummary("Block a user and sign them out everywhere");
        users.MapPost("/{id:guid}/unblock", Unblock).Produces<AdminUserDto>().WithSummary("Unblock a user");
        users.MapPost("/{id:guid}/affiliate-code/disable", DisableCode).Produces<AdminUserDto>().WithSummary("Deactivate the affiliate code (fraud): refused on every product");
        users.MapPost("/{id:guid}/affiliate-code/enable", EnableCode).Produces<AdminUserDto>().WithSummary("Re-enable a deactivated affiliate code");

        admin.MapGet("/audit", Audit).WithTags("Admin · Audit").Produces<PagedResult<AuditEntryDto>>()
            .WithSummary("Audit log of admin actions, newest first");

        return app;
    }

    private static Task<PagedResult<AdminUserListItem>> List(
        string? search, Role? role, UserStatus? status, int? page, int? pageSize, AdminUserService service, CancellationToken ct) =>
        service.ListAsync(new UserListQuery(search, role, status, page, pageSize), ct);

    private static Task<UserCounts> Counts(AdminUserService service, CancellationToken ct) => service.CountsAsync(ct);

    private static async Task<IResult> Get(Guid id, AdminUserService service, CancellationToken ct) =>
        Respond(await service.GetAsync(id, ct));

    private static async Task<IResult> Grant(Guid id, Role role, AdminUserService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.GrantRoleAsync(http.User.GetAdminActor(), id, role, ct));

    private static async Task<IResult> Revoke(Guid id, Role role, AdminUserService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.RevokeRoleAsync(http.User.GetAdminActor(), id, role, ct));

    private static async Task<IResult> Block(Guid id, BlockUserRequest request, AdminUserService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.BlockAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> Unblock(Guid id, AdminUserService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.UnblockAsync(http.User.GetAdminActor(), id, ct));

    private static async Task<IResult> DisableCode(Guid id, DisableAffiliateCodeRequest request, AdminUserService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.DisableAffiliateCodeAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> EnableCode(Guid id, AdminUserService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.EnableAffiliateCodeAsync(http.User.GetAdminActor(), id, ct));

    private static Task<PagedResult<AuditEntryDto>> Audit(
        string? targetType, Guid? targetId, Guid? actorId, string? action, int? page, int? pageSize, AuditService service, CancellationToken ct) =>
        service.ListAsync(new AuditQuery(targetType, targetId, actorId, action, page, pageSize), ct);

    private static IResult Respond(Result<AdminUserDto> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
