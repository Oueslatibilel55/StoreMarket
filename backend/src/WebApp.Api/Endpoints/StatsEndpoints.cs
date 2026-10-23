using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;

namespace WebApp.Api.Endpoints;

/// <summary>
/// Dashboard numbers (023): the admin's overview and per affiliate / store / product stats, and the store owner's own
/// dashboard. Every endpoint takes <c>?from=&amp;to=</c> (inclusive dates, default the last 30 days, see <see cref="StatsPeriod"/>).
/// </summary>
internal static class StatsEndpoints
{
    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/stats").WithTags("Admin · Stats").RequireAuthorization(AuthPolicies.Admin);
        admin.MapGet("/overview", Overview).Produces<AdminOverviewDto>()
            .WithSummary("Platform KPIs of the period: orders, sales, coins, payouts, stores, daily series, top stores / products / affiliates");
        admin.MapGet("/affiliates/{id:guid}", Affiliate).Produces<AffiliateStatsDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("An affiliate's orders with their code, sales, coins per store / product, partnerships");
        admin.MapGet("/stores/{id:guid}", Store).Produces<StoreStatsDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("A store's orders, sales, coins generated for affiliates, top products and affiliates");
        admin.MapGet("/products/{id:guid}", Product).Produces<ProductStatsDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("A product's orders, sales, coins earned by affiliates on it");

        app.MapGet("/api/me/store/stats", MyStore).WithTags("Store").RequireAuthorization(AuthPolicies.StoreOwner)
            .Produces<StoreStatsDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("My store's dashboard: sales, sales via affiliates, coins given to affiliates per product");

        return app;
    }

    private static StatsPeriod Period(DateOnly? from, DateOnly? to, TimeProvider clock) =>
        StatsPeriod.Of(from, to, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));

    private static Task<AdminOverviewDto> Overview(DateOnly? from, DateOnly? to, IStatsQueries stats, TimeProvider clock, CancellationToken ct) =>
        stats.OverviewAsync(Period(from, to, clock), ct);

    private static async Task<IResult> Affiliate(Guid id, DateOnly? from, DateOnly? to, IStatsQueries stats, TimeProvider clock, CancellationToken ct) =>
        await stats.AffiliateAsync(id, Period(from, to, clock), ct) is { } result
            ? TypedResults.Ok(result)
            : Result.Fail(AuthErrors.UserNotFound).ToProblem();

    private static async Task<IResult> Store(Guid id, DateOnly? from, DateOnly? to, IStatsQueries stats, TimeProvider clock, CancellationToken ct) =>
        await stats.StoreAsync(id, Period(from, to, clock), ct) is { } result
            ? TypedResults.Ok(result)
            : Result.Fail(StoreErrors.NotFound).ToProblem();

    private static async Task<IResult> Product(Guid id, DateOnly? from, DateOnly? to, IStatsQueries stats, TimeProvider clock, CancellationToken ct) =>
        await stats.ProductAsync(id, Period(from, to, clock), ct) is { } result
            ? TypedResults.Ok(result)
            : Result.Fail(ProductErrors.NotFound).ToProblem();

    /// <summary>The owner's store comes from the token, never from the request.</summary>
    private static async Task<IResult> MyStore(DateOnly? from, DateOnly? to, IStatsQueries stats, ICatalogQueries catalog, TimeProvider clock,
        HttpContext http, CancellationToken ct)
    {
        var store = await catalog.GetStoreOfOwnerAsync(http.User.GetUserId()!.Value, ct);
        return store is not null && await stats.StoreAsync(store.Id, Period(from, to, clock), ct) is { } result
            ? TypedResults.Ok(result)
            : Result.Fail(StoreErrors.NotFound).ToProblem();
    }
}
