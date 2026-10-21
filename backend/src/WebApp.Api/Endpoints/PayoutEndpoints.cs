using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Api.Endpoints;

/// <summary>
/// Payout requests (021): the affiliate asks and follows their requests (<c>/api/me/payouts</c>), the admin verifies,
/// rejects, schedules and marks them paid (<c>/api/admin/payouts</c>, audited). Affiliates never withdraw directly.
/// </summary>
internal static class PayoutEndpoints
{
    public static IEndpointRouteBuilder MapPayoutEndpoints(this IEndpointRouteBuilder app)
    {
        var mine = app.MapGroup("/api/me/payouts").WithTags("Affiliate").RequireAuthorization(AuthPolicies.Affiliate);
        mine.MapGet("/", Mine).Produces<MyPayoutsDto>().WithSummary("My payout requests, available coins and the minimum");
        mine.MapPost("/", Request).Produces<PayoutDto>(StatusCodes.Status201Created)
            .WithSummary("Ask for a payout (≥ minimum, ≤ available, one open at a time); the coins are locked");
        mine.MapPost("/{id:guid}/cancel", Cancel).Produces<PayoutDto>().WithSummary("Withdraw a request not scheduled yet; the coins come back");

        var admin = app.MapGroup("/api/admin/payouts").WithTags("Admin · Payouts").RequireAuthorization(AuthPolicies.Admin);
        admin.MapGet("/", List).Produces<PagedResult<PayoutListItem>>().WithSummary("Payout requests; filter by status, method, scheduled dates, search");
        admin.MapGet("/counts", Counts).Produces<AdminPayoutCounts>().WithSummary("Open requests per step and the coins they lock");
        admin.MapGet("/{id:guid}", Detail).Produces<AdminPayoutDetail>().WithSummary("Verification screen: the request, the affiliate, the covered coins");
        admin.MapPost("/{id:guid}/verify", Verify).Produces<PayoutDto>().WithSummary("Mark the request as verified");
        admin.MapPost("/{id:guid}/reject", Reject).Produces<PayoutDto>().WithSummary("Refuse it (reason required); the coins come back");
        admin.MapPost("/{id:guid}/schedule", Schedule).Produces<PayoutDto>().WithSummary("Set or move the date (cash: place required)");
        admin.MapPost("/{id:guid}/paid", Paid).Produces<PayoutDto>().WithSummary("Mark as paid with the bank reference or who handed the cash");

        return app;
    }

    private static Task<MyPayoutsDto> Mine(PayoutService service, HttpContext http, CancellationToken ct) =>
        service.MineAsync(http.User.GetUserId()!.Value, ct);

    private static async Task<IResult> Request(CreatePayoutRequest request, PayoutService service, HttpContext http, CancellationToken ct)
    {
        Result<PayoutDto> result = await service.RequestAsync(http.User.GetUserId()!.Value, request, ct);
        return result.IsSuccess ? TypedResults.Created($"/api/me/payouts/{result.Value.Id}", result.Value) : result.ToProblem();
    }

    private static async Task<IResult> Cancel(Guid id, PayoutService service, HttpContext http, CancellationToken ct) =>
        Ok(await service.CancelAsync(http.User.GetUserId()!.Value, id, ct));

    private static Task<PagedResult<PayoutListItem>> List(PayoutStatus? status, PayoutMethod? method, DateTime? from, DateTime? to, string? search,
        Guid? affiliateId, int? page, int? pageSize, PayoutService service, CancellationToken ct) =>
        service.ListAsync(new PayoutQuery(status, method, from, to, search, affiliateId, page, pageSize), ct);

    private static Task<AdminPayoutCounts> Counts(PayoutService service, CancellationToken ct) => service.CountsAsync(ct);

    private static async Task<IResult> Detail(Guid id, PayoutService service, CancellationToken ct)
    {
        Result<AdminPayoutDetail> result = await service.DetailAsync(id, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }

    private static async Task<IResult> Verify(Guid id, VerifyPayoutRequest? request, PayoutService service, HttpContext http, CancellationToken ct) =>
        Ok(await service.VerifyAsync(http.User.GetAdminActor(), id, request ?? new VerifyPayoutRequest(), ct));

    private static async Task<IResult> Reject(Guid id, RejectPayoutRequest request, PayoutService service, HttpContext http, CancellationToken ct) =>
        Ok(await service.RejectAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> Schedule(Guid id, SchedulePayoutRequest request, PayoutService service, HttpContext http, CancellationToken ct) =>
        Ok(await service.ScheduleAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> Paid(Guid id, MarkPayoutPaidRequest request, PayoutService service, HttpContext http, CancellationToken ct) =>
        Ok(await service.MarkPaidAsync(http.User.GetAdminActor(), id, request, ct));

    private static IResult Ok(Result<PayoutDto> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
