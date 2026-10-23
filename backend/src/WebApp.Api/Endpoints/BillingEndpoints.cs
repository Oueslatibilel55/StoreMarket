using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Api.Endpoints;

/// <summary>
/// Store billing (025). The admin manages prices, offers and payments (<c>/api/admin/billing</c>, audited); the store
/// owner reads their statement and declares payments (<c>/api/me/store/billing</c>, store from the token).
/// </summary>
internal static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/billing").WithTags("Admin · Billing").RequireAuthorization(AuthPolicies.Admin);
        admin.MapGet("/stores/{id:guid}", Store).Produces<StoreBillingDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("A store's billing: price, paid-until, state, month-by-month statement, invoices, payments, offers");
        admin.MapPut("/stores/{id:guid}/price", SetPrice).Produces<StoreBillingDto>().WithSummary("Set the store's monthly price (audited)");
        admin.MapGet("/stores/{id:guid}/quote", Quote).Produces<SubscriptionQuote>()
            .WithSummary("What ?months= months cost the store today, with the offers that apply (best first)");
        admin.MapPost("/stores/{id:guid}/subscription-payments", RecordSubscription).Produces<StorePaymentDto>()
            .WithSummary("Record months of subscription paid in person (approved at once, paid-until extended, audited)");
        admin.MapGet("/invoices/{id:guid}", Invoice).Produces<InvoiceDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("A commission invoice with every line and its payments");
        admin.MapPost("/invoices/{id:guid}/payments", RecordInvoicePayment).Produces<StorePaymentDto>()
            .WithSummary("Record a payment of (part of) an invoice (approved at once, audited)");
        admin.MapGet("/payments", Payments).Produces<PagedResult<StorePaymentDto>>()
            .WithSummary("Store payments; ?status=Declared is the queue to check (oldest first)");
        admin.MapPost("/payments/{id:guid}/approve", Approve).Produces<StorePaymentDto>().WithSummary("Approve a declared payment (audited)");
        admin.MapPost("/payments/{id:guid}/reject", Reject).Produces<StorePaymentDto>().WithSummary("Reject a declared payment with a reason (audited)");
        admin.MapGet("/overdue", Overdue).Produces<IReadOnlyList<OverdueStoreItem>>()
            .WithSummary("Stores late (in grace or overdue), waiting for approval or suspended");
        admin.MapGet("/offers", Offers).Produces<IReadOnlyList<OfferDto>>().WithSummary("Subscription offers with how often they were used");
        admin.MapPost("/offers", CreateOffer).Produces<OfferDto>().WithSummary("Create an offer (audited)");
        admin.MapPut("/offers/{id:guid}", UpdateOffer).Produces<OfferDto>().WithSummary("Edit or (de)activate an offer (audited)");
        admin.MapGet("/revenue", Revenue).Produces<BillingRevenueDto>()
            .WithSummary("Per month: subscriptions collected, commissions billed and collected; offers usage (?from=&to=)");
        admin.MapPost("/run", Run).Produces<BillingRunResult>()
            .WithSummary("Run the billing checks now: issue past months' invoices, suspend / reactivate stores");

        var mine = app.MapGroup("/api/me/store/billing").WithTags("Store").RequireAuthorization(AuthPolicies.StoreOwner);
        mine.MapGet("/", Mine).Produces<StoreBillingDto>().WithSummary("My store's billing: what I owe, paid-until, statement, invoices, payments");
        mine.MapGet("/status", MyStatus).Produces<BillingStatusDto>().WithSummary("The banner: late, days before suspension, waiting for approval");
        mine.MapGet("/invoices/{id:guid}", MyInvoice).Produces<InvoiceDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("One of my invoices with every line");
        mine.MapPost("/payments", Declare).Produces<StorePaymentDto>()
            .WithSummary("\"I paid\": months of subscription or an amount of an invoice, optional receipt photo; an admin checks it");

        return app;
    }

    // -- Admin --------------------------------------------------------------------

    private static async Task<IResult> Store(Guid id, BillingService service, CancellationToken ct) => Respond(await service.StoreAsync(id, ct));

    private static async Task<IResult> SetPrice(Guid id, SetSubscriptionPriceRequest request, BillingService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.SetPriceAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> Quote(Guid id, int months, BillingService service, CancellationToken ct) =>
        Respond(await service.QuoteAsync(id, months, ct));

    private static async Task<IResult> RecordSubscription(Guid id, RecordSubscriptionPaymentRequest request, BillingService service, HttpContext http,
        CancellationToken ct) =>
        Respond(await service.RecordSubscriptionAsync(http.User.GetAdminActor(), id, request, ct));

    private static async Task<IResult> Invoice(Guid id, BillingService service, CancellationToken ct) => Respond(await service.InvoiceAsync(id, ct));

    private static async Task<IResult> RecordInvoicePayment(Guid id, RecordInvoicePaymentRequest request, BillingService service, HttpContext http,
        CancellationToken ct) =>
        Respond(await service.RecordInvoicePaymentAsync(http.User.GetAdminActor(), id, request, ct));

    private static Task<PagedResult<StorePaymentDto>> Payments(StorePaymentStatus? status, StorePaymentKind? kind, Guid? storeId, int? page, int? pageSize,
        BillingService service, CancellationToken ct) =>
        service.PaymentsAsync(new StorePaymentQuery(status, kind, storeId, page, pageSize), ct);

    private static async Task<IResult> Approve(Guid id, BillingService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.ApproveAsync(http.User.GetAdminActor(), id, ct));

    private static async Task<IResult> Reject(Guid id, RejectStorePaymentRequest request, BillingService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.RejectAsync(http.User.GetAdminActor(), id, request, ct));

    private static Task<IReadOnlyList<OverdueStoreItem>> Overdue(BillingService service, CancellationToken ct) => service.OverdueAsync(ct);

    private static Task<IReadOnlyList<OfferDto>> Offers(BillingService service, CancellationToken ct) => service.OffersAsync(ct);

    private static async Task<IResult> CreateOffer(SaveOfferRequest request, BillingService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.CreateOfferAsync(http.User.GetAdminActor(), request, ct));

    private static async Task<IResult> UpdateOffer(Guid id, SaveOfferRequest request, BillingService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.UpdateOfferAsync(http.User.GetAdminActor(), id, request, ct));

    private static Task<BillingRevenueDto> Revenue(DateOnly? from, DateOnly? to, BillingService service, TimeProvider clock, CancellationToken ct) =>
        service.RevenueAsync(StatsPeriod.Of(from, to, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)), ct);

    private static Task<BillingRunResult> Run(BillingService service, CancellationToken ct) => service.RunAsync(ct);

    // -- Store owner ----------------------------------------------------------------

    private static async Task<IResult> Mine(BillingService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.MineAsync(http.User.GetUserId()!.Value, ct));

    private static async Task<IResult> MyStatus(BillingService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.MyStatusAsync(http.User.GetUserId()!.Value, ct));

    private static async Task<IResult> MyInvoice(Guid id, BillingService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.MyInvoiceAsync(http.User.GetUserId()!.Value, id, ct));

    private static async Task<IResult> Declare(DeclarePaymentRequest request, BillingService service, HttpContext http, CancellationToken ct) =>
        Respond(await service.DeclareAsync(http.User.GetUserId()!.Value, request, ct));

    private static IResult Respond<T>(Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
