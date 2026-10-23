using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

/// <summary>
/// Store billing (025). The checks the hourly job runs are run here for one store at a chosen date
/// (<see cref="BillingService.RunForStoreAsync"/>), so a test can move through months without touching other tests' stores.
/// </summary>
public sealed class BillingEndpointsTests(ApiFactory factory)
{
    private static readonly string[] Tunis = ["tunis"];

    private static string Unique(string name) => $"{name} {Guid.NewGuid().ToString("N")[..10]}";

    private sealed record World(HttpClient Admin, HttpClient Owner, MyStoreDto Store) : IDisposable
    {
        public void Dispose()
        {
            Admin.Dispose();
            Owner.Dispose();
        }
    }

    /// <summary>A store selling a 50 TND product at 10 %; with <paramref name="sale"/>, 2 sold with an affiliate code and delivered (10 TND owed).</summary>
    private async Task<World> Arrange(bool sale = true)
    {
        var admin = factory.CreateClient();
        admin.WithToken((await admin.SignInAdmin()).AccessToken);
        var category = await (await admin.Post("/api/admin/product-categories", new { nameFr = Unique("Parfums") })).Read<AdminProductCategoryDto>();
        var storeCategory = await (await admin.Post("/api/admin/store-categories", new { nameFr = Unique("Beauté") })).Read<AdminStoreCategoryDto>();
        var owner = await factory.StoreOwnerClient();
        var store = await (await owner.Post("/api/me/store", new
        {
            name = Unique("Boutique"), categoryId = storeCategory.Id, phone = "22 145 678", city = "tunis",
            defaultAffiliatePercent = 10, deliveryFee = 7m, deliveryCities = Tunis, deliveryMinDays = 1, deliveryMaxDays = 2,
        })).Read<MyStoreDto>();
        if (!sale) return new World(admin, owner, store);

        var product = await (await owner.Post("/api/me/store/products", new
        {
            name = Unique("Musc"), categoryId = category.Id, price = 50m, stock = 20, status = "Published",
        })).Read<ProductDto>();
        using var affiliate = factory.CreateClient();
        var auth = await affiliate.RegisterAffiliate();
        affiliate.WithToken(auth.AccessToken);
        var request = await (await affiliate.Post("/api/me/partnerships", new { storeId = store.Id })).Read<AffiliatePartnershipDto>();
        (await owner.Post($"/api/me/store/partnerships/{request.Id}/accept")).EnsureSuccessStatusCode();

        using var client = factory.CreateClient();
        client.WithToken((await client.Register()).AccessToken);
        (await client.Post("/api/me/cart/lines", new { productId = product.Id, quantity = 2, affiliateCode = auth.User.AffiliateCode })).EnsureSuccessStatusCode();
        var order = await (await client.Post("/api/orders", new
        {
            contact = new { fullName = "Leila Gharbi", phone = "50 112 233", city = "tunis", address = "12 rue de Marseille" },
        })).Read<OrderDto>();
        var storeOrderId = order.StoreOrders.Single().Id;
        (await owner.Post($"/api/me/store/orders/{storeOrderId}/status", new { status = "Shipped" })).EnsureSuccessStatusCode();
        (await owner.Post($"/api/me/store/orders/{storeOrderId}/status", new { status = "Delivered" })).EnsureSuccessStatusCode();
        return new World(admin, owner, store);
    }

    /// <summary>The job's checks for this store only, as if it were <paramref name="at"/>.</summary>
    private async Task<BillingRunResult> RunAt(Guid storeId, DateTime at)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BillingService>().RunForStoreAsync(storeId, at, Ct);
    }

    private static DateTime NextMonth(int day) =>
        new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 8, 0, 0, DateTimeKind.Utc).AddMonths(1).AddDays(day - 1);

    private static async Task<T> Get<T>(HttpClient client, string url) =>
        await (await client.GetAsync(new Uri(url, UriKind.Relative), Ct)).Read<T>();

    private static Task<StoreBillingDto> Mine(World w) => Get<StoreBillingDto>(w.Owner, "/api/me/store/billing");

    [Fact]
    public async Task Commissions_InvoicedOncePerMonth_WithEveryLine()
    {
        using var w = await Arrange();

        var before = await Mine(w);
        Assert.Equal((1, 10m), (before.CurrentMonth.Lines, before.CurrentMonth.Amount)); // not invoiced yet
        Assert.Empty(before.Invoices);
        Assert.Equal(0, (await RunAt(w.Store.Id, DateTime.UtcNow)).InvoicesIssued); // the month isn't over

        Assert.Equal(1, (await RunAt(w.Store.Id, NextMonth(2))).InvoicesIssued);
        Assert.Equal(0, (await RunAt(w.Store.Id, NextMonth(3))).InvoicesIssued); // billed once

        var billing = await Mine(w);
        var invoice = Assert.Single(billing.Invoices);
        Assert.Equal((10m, 0m, CommissionInvoiceStatus.Issued), (invoice.Total, invoice.PaidAmount, invoice.Status));
        Assert.Equal(DateOnly.FromDateTime(NextMonth(10)), invoice.DueOn); // the 10th of the next month (setting)
        Assert.Equal(0, billing.CurrentMonth.Lines);
        var month = billing.Months.Single(m => m.InvoiceId == invoice.Id);
        Assert.Equal((10m, 0m), (month.CommissionsBilled, month.CommissionsPaid));

        var detail = await Get<InvoiceDetailDto>(w.Owner, $"/api/me/store/billing/invoices/{invoice.Id}");
        var line = Assert.Single(detail.Lines);
        Assert.Equal((2, 100m, 10, 10m), (line.Quantity!.Value, line.LineTotal!.Value, line.AffiliatePercent!.Value, line.Amount));
        Assert.NotNull(line.AffiliateCode);
        Assert.Equal(detail.Invoice.Id, (await Get<InvoiceDetailDto>(w.Admin, $"/api/admin/billing/invoices/{invoice.Id}")).Invoice.Id);
    }

    [Fact]
    public async Task UnpaidInvoice_SuspendsAfterGrace_DeclarationThenApprovalSettlesIt()
    {
        using var w = await Arrange();
        // Subscription paid far ahead: only the invoice matters here.
        (await w.Admin.Post($"/api/admin/billing/stores/{w.Store.Id}/subscription-payments", new { months = 12, method = "Cash" })).EnsureSuccessStatusCode();
        await RunAt(w.Store.Id, NextMonth(2));
        var invoice = Assert.Single((await Mine(w)).Invoices);

        Assert.Equal(0, (await RunAt(w.Store.Id, NextMonth(13))).StoresSuspended); // 10th + 3 grace days
        Assert.Equal(1, (await RunAt(w.Store.Id, NextMonth(14))).StoresSuspended);
        using (var visitor = factory.CreateClient())
            Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync(new Uri($"/api/stores/{w.Store.Slug}", UriKind.Relative), Ct)).StatusCode);

        var declared = await (await w.Owner.Post("/api/me/store/billing/payments", new
        {
            kind = "Commission", invoiceId = invoice.Id, method = "BankTransfer", reference = "VIR-123",
        })).Read<StorePaymentDto>();
        Assert.Equal((StorePaymentStatus.Declared, 10m), (declared.Status, declared.Amount)); // everything left by default
        Assert.Equal(HttpStatusCode.Conflict, (await w.Owner.Post("/api/me/store/billing/payments", new
        {
            kind = "Commission", invoiceId = invoice.Id, method = "Cash",
        })).StatusCode); // one declaration at a time
        Assert.True((await Get<BillingStatusDto>(w.Owner, "/api/me/store/billing/status")).WaitingForApproval);

        var queue = await Get<PagedResult<StorePaymentDto>>(w.Admin, $"/api/admin/billing/payments?status=Declared&storeId={w.Store.Id}");
        Assert.Equal(declared.Id, Assert.Single(queue.Items).Id);
        var approved = await (await w.Admin.Post($"/api/admin/billing/payments/{declared.Id}/approve")).Read<StorePaymentDto>();
        Assert.Equal(StorePaymentStatus.Approved, approved.Status);

        var after = await Mine(w);
        Assert.Equal(CommissionInvoiceStatus.Paid, Assert.Single(after.Invoices).Status);
        Assert.Equal(StoreStatus.Active, after.Status); // checked again right after the approval
        Assert.Equal(0m, after.State.CommissionsDue);
    }

    [Fact]
    public async Task RejectedDeclaration_ShowsTheReason_AndCanBePaidInParts()
    {
        using var w = await Arrange();
        await RunAt(w.Store.Id, NextMonth(2));
        var invoice = Assert.Single((await Mine(w)).Invoices);

        var declared = await (await w.Owner.Post("/api/me/store/billing/payments", new
        {
            kind = "Commission", invoiceId = invoice.Id, amount = 4m, method = "Cash",
        })).Read<StorePaymentDto>();
        Assert.Equal("billing.reasonRequired", await (await w.Admin.Post($"/api/admin/billing/payments/{declared.Id}/reject", new { reason = " " })).ProblemCode());
        (await w.Admin.Post($"/api/admin/billing/payments/{declared.Id}/reject", new { reason = "Aucun virement reçu" })).EnsureSuccessStatusCode();
        Assert.Equal("Aucun virement reçu", (await Get<BillingStatusDto>(w.Owner, "/api/me/store/billing/status")).LastRejectionReason);

        // The admin records cash received: 4 TND, then the rest; never more than what is left.
        var url = $"/api/admin/billing/invoices/{invoice.Id}/payments";
        (await w.Admin.Post(url, new { amount = 4m, method = "Cash" })).EnsureSuccessStatusCode();
        Assert.Equal(CommissionInvoiceStatus.PartiallyPaid, Assert.Single((await Mine(w)).Invoices).Status);
        Assert.Equal("billing.amountTooHigh", await (await w.Admin.Post(url, new { amount = 7m, method = "Cash" })).ProblemCode());
        (await w.Admin.Post(url, new { amount = 6m, method = "Cash" })).EnsureSuccessStatusCode();
        Assert.Equal(CommissionInvoiceStatus.Paid, Assert.Single((await Mine(w)).Invoices).Status);
        Assert.Equal("billing.invoicePaid", await (await w.Admin.Post(url, new { amount = 1m, method = "Cash" })).ProblemCode());
    }

    [Fact]
    public async Task Subscription_OfferApplies_PaidUntilExtended_LapseSuspends()
    {
        using var w = await Arrange(sale: false);
        var start = await Mine(w);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), start.State.PaidUntil); // trial days (setting)

        // An offer for this store only (other tests' stores are not concerned).
        var offer = await (await w.Admin.Post("/api/admin/billing/offers", new
        {
            nameFr = "Trimestriel", months = 3, discountPercent = 20, target = "Stores", targetIds = new[] { w.Store.Id }, isActive = true,
        })).Read<OfferDto>();
        var quote = await Get<SubscriptionQuote>(w.Admin, $"/api/admin/billing/stores/{w.Store.Id}/quote?months=3");
        Assert.Equal((90m, offer.Id, 72m), (quote.Gross, quote.Offers[0].Id, quote.Offers[0].Amount)); // 3 × 30 − 20 %

        var declared = await (await w.Owner.Post("/api/me/store/billing/payments", new { kind = "Subscription", months = 3, method = "Cash" }))
            .Read<StorePaymentDto>();
        Assert.Equal((72m, 18m, "Trimestriel"), (declared.Amount, declared.Discount, declared.OfferName)); // the best offer applies
        var approved = await (await w.Admin.Post($"/api/admin/billing/payments/{declared.Id}/approve")).Read<StorePaymentDto>();
        Assert.Equal(start.State.PaidUntil.AddDays(1), approved.CoversFrom);
        Assert.Equal(start.State.PaidUntil.AddDays(1).AddMonths(3).AddDays(-1), (await Mine(w)).State.PaidUntil);
        Assert.Equal(1, (await Get<IReadOnlyList<OfferDto>>(w.Admin, "/api/admin/billing/offers")).Single(o => o.Id == offer.Id).TimesUsed);

        // Months pass without paying: suspended after the grace days, back as soon as the admin records a payment.
        var paidUntil = (await Mine(w)).State.PaidUntil.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        Assert.Equal(0, (await RunAt(w.Store.Id, paidUntil.AddDays(3))).StoresSuspended);
        Assert.Equal(1, (await RunAt(w.Store.Id, paidUntil.AddDays(4))).StoresSuspended);
        Assert.Equal(StoreStatus.Suspended, (await Mine(w)).Status);
        Assert.Contains(await Get<IReadOnlyList<OverdueStoreItem>>(w.Admin, "/api/admin/billing/overdue"), i => i.StoreId == w.Store.Id);
        (await w.Admin.Post($"/api/admin/billing/stores/{w.Store.Id}/subscription-payments", new { months = 1, method = "Cash" })).EnsureSuccessStatusCode();
        Assert.Equal(StoreStatus.Active, (await Mine(w)).Status);
        Assert.DoesNotContain(await Get<IReadOnlyList<OverdueStoreItem>>(w.Admin, "/api/admin/billing/overdue"), i => i.StoreId == w.Store.Id);
    }

    [Fact]
    public async Task Price_Audited_OffersValidated_OwnerSeesOnlyTheirOwn()
    {
        using var w = await Arrange();
        var priced = await (await w.Admin.PutAsJsonAsync($"/api/admin/billing/stores/{w.Store.Id}/price", new { monthlyPrice = 45m, reason = "Grande boutique" }, Ct))
            .Read<StoreBillingDto>();
        Assert.Equal(45m, priced.MonthlyPrice);
        var audit = await Get<PagedResult<AuditEntryDto>>(w.Admin, $"/api/admin/audit?targetType=Store&targetId={w.Store.Id}");
        Assert.Contains(audit.Items, a => a.Action == "billing.price");
        Assert.Equal("billing.offerDiscountInvalid", await (await w.Admin.Post("/api/admin/billing/offers", new
        {
            nameFr = "Rien", target = "AllStores", isActive = true,
        })).ProblemCode());

        await RunAt(w.Store.Id, NextMonth(2));
        var invoice = Assert.Single((await Mine(w)).Invoices);
        // Another store owner: someone else's invoice is reported as missing.
        using var other = await factory.StoreOwnerClient();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(new Uri($"/api/me/store/billing/invoices/{invoice.Id}", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Owner.GetAsync(new Uri($"/api/admin/billing/stores/{w.Store.Id}", UriKind.Relative), Ct)).StatusCode);
    }
}
