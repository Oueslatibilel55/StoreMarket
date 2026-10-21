using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Api.Endpoints;

/// <summary>
/// Role requests: a user asks for Buyer, Affiliate or StoreOwner, an admin approves or refuses
/// (audited). The new role reaches the client after its next token refresh.
/// </summary>
internal static class RoleRequestEndpoints
{
    public static IEndpointRouteBuilder MapRoleRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var mine = app.MapGroup("/api/me/role-requests").WithTags("Account").RequireAuthorization();
        mine.MapGet("/", ListMine).Produces<IReadOnlyList<RoleRequestDto>>().WithSummary("My role requests, newest first");
        mine.MapPost("/", Create).Produces<RoleRequestDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Ask for a role (Buyer, Affiliate, StoreOwner). Affiliate needs acceptTerms.");
        mine.MapPost("/{id:guid}/cancel", Cancel).Produces<RoleRequestDto>().WithSummary("Withdraw my pending request");

        var admin = app.MapGroup("/api/admin/role-requests").WithTags("Admin · Role requests")
            .RequireAuthorization(AuthPolicies.Admin);
        admin.MapGet("/", List).Produces<PagedResult<AdminRoleRequestItem>>()
            .WithSummary("Role requests, filter by status / role, paged (pending: oldest first)");
        admin.MapPost("/{id:guid}/approve", Approve).Produces<RoleRequestDto>().WithSummary("Approve and grant the role");
        admin.MapPost("/{id:guid}/refuse", Refuse).Produces<RoleRequestDto>().WithSummary("Refuse, with a reason shown to the user");

        return app;
    }

    private static async Task<IResult> ListMine(RoleRequestService service, HttpContext http, CancellationToken ct) =>
        http.User.GetUserId() is { } userId ? TypedResults.Ok(await service.ListMineAsync(userId, ct)) : TypedResults.Unauthorized();

    private static async Task<IResult> Create(CreateRoleRequest request, RoleRequestService service, HttpContext http, CancellationToken ct)
    {
        if (http.User.GetUserId() is not { } userId) return TypedResults.Unauthorized();

        var result = await service.CreateAsync(userId, request, ct);
        return result.IsSuccess ? TypedResults.Created($"/api/me/role-requests/{result.Value.Id}", result.Value) : result.ToProblem();
    }

    private static async Task<IResult> Cancel(Guid id, RoleRequestService service, HttpContext http, CancellationToken ct) =>
        http.User.GetUserId() is { } userId ? Respond(await service.CancelAsync(userId, id, ct)) : TypedResults.Unauthorized();

    private static Task<PagedResult<AdminRoleRequestItem>> List(
        RoleRequestStatus? status, Role? role, int? page, int? pageSize, RoleRequestService service, CancellationToken ct) =>
        service.ListAsync(new RoleRequestListQuery(status, role, page, pageSize), ct);

    private static async Task<IResult> Approve(Guid id, RoleRequestService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.ApproveAsync(http.User.GetAdminActor(), id, ct));

    private static async Task<IResult> Refuse(Guid id, RefuseRoleRequest body, RoleRequestService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.RefuseAsync(http.User.GetAdminActor(), id, body, ct));

    private static IResult Respond(Result<RoleRequestDto> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
