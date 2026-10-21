using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Api.Endpoints;

/// <summary>
/// The coin ledger (019): the affiliate reads their coins (<c>/api/me/coins</c>), the admin reads the whole ledger and
/// corrects it with audited adjustments. Coins are never written here otherwise — they come from delivered orders.
/// </summary>
internal static class CoinEndpoints
{
    public static IEndpointRouteBuilder MapCoinEndpoints(this IEndpointRouteBuilder app)
    {
        var mine = app.MapGroup("/api/me/coins").WithTags("Affiliate").RequireAuthorization(AuthPolicies.Affiliate);
        mine.MapGet("/", Summary).Produces<CoinSummaryDto>().WithSummary("My coins: balance, total earned, expected (not delivered yet)");
        mine.MapGet("/entries", MyEntries).Produces<PagedResult<CoinEntryDto>>().WithSummary("My coin history, newest first (client phones masked)");
        mine.MapGet("/export", MyExport).Produces<IReadOnlyList<CoinEntryDto>>().WithSummary("Every entry matching the filters (max 5000), for a CSV export");
        mine.MapGet("/breakdown", MyBreakdown).Produces<IReadOnlyList<CoinBreakdownRow>>().WithSummary("Earned coins per product or per store over a period");

        var admin = app.MapGroup("/api/admin/coins").WithTags("Admin · Coins").RequireAuthorization(AuthPolicies.Admin);
        admin.MapGet("/entries", AdminEntries).Produces<PagedResult<CoinEntryDto>>()
            .WithSummary("The whole ledger; filter by affiliate, store, type, dates, search");
        admin.MapGet("/totals", Totals).Produces<AdminCoinTotals>().WithSummary("Coins on accounts, earned, expected");
        admin.MapGet("/affiliates/{id:guid}", AffiliateSummary).Produces<CoinSummaryDto>().WithSummary("One affiliate's coins");
        admin.MapPost("/adjustments", Adjust).Produces<CoinSummaryDto>()
            .WithSummary("Correct an affiliate's coins: signed amount, reason required, audited");

        return app;
    }

    private static Task<CoinSummaryDto> Summary(CoinLedgerService service, HttpContext http, CancellationToken ct) =>
        service.SummaryAsync(http.User.GetUserId()!.Value, ct);

    private static Task<PagedResult<CoinEntryDto>> MyEntries(CoinEntryType? type, Guid? storeId, Guid? productId, string? search, DateTime? from, DateTime? to,
        int? page, int? pageSize, CoinLedgerService service, HttpContext http, CancellationToken ct) =>
        service.ListMineAsync(http.User.GetUserId()!.Value, new CoinEntryQuery(null, storeId, type, search, from, to, page, pageSize, productId), ct);

    private static Task<IReadOnlyList<CoinEntryDto>> MyExport(CoinEntryType? type, Guid? storeId, Guid? productId, string? search, DateTime? from, DateTime? to,
        CoinLedgerService service, HttpContext http, CancellationToken ct) =>
        service.ExportMineAsync(http.User.GetUserId()!.Value, new CoinEntryQuery(null, storeId, type, search, from, to, null, null, productId), ct);

    private static Task<IReadOnlyList<CoinBreakdownRow>> MyBreakdown(CoinBreakdownBy? by, DateTime? from, DateTime? to,
        CoinLedgerService service, HttpContext http, CancellationToken ct) =>
        service.BreakdownAsync(http.User.GetUserId()!.Value, by ?? CoinBreakdownBy.Product, from, to, ct);

    private static Task<PagedResult<CoinEntryDto>> AdminEntries(Guid? affiliateId, Guid? storeId, CoinEntryType? type, string? search,
        DateTime? from, DateTime? to, int? page, int? pageSize, CoinLedgerService service, CancellationToken ct) =>
        service.ListAsync(new CoinEntryQuery(affiliateId, storeId, type, search, from, to, page, pageSize), ct);

    private static Task<AdminCoinTotals> Totals(CoinLedgerService service, CancellationToken ct) => service.TotalsAsync(ct);

    private static Task<CoinSummaryDto> AffiliateSummary(Guid id, CoinLedgerService service, CancellationToken ct) =>
        service.SummaryAsync(id, ct);

    private static async Task<IResult> Adjust(CreateCoinAdjustmentRequest request, CoinLedgerService service, HttpContext http, CancellationToken ct)
    {
        Result<CoinSummaryDto> result = await service.AdjustAsync(http.User.GetAdminActor(), request, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }
}
