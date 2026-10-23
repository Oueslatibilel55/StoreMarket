using WebApp.Application.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Domain.Settings;

namespace WebApp.Application.Services;

/// <summary>
/// Store billing (025). Stores pay the platform their subscription (months at their own price, an offer may apply) and
/// a monthly invoice of the affiliate commissions earned on their products. The owner declares a payment and an admin
/// approves or rejects it, or the admin records one received in person. An approved payment is applied in the same
/// transaction (paid-until extended / invoice paid) and the store is re-checked at once: suspended when something is
/// unpaid past the grace days, active again when nothing is. The hourly job (<c>BillingJob</c>) issues the invoices of
/// past months and runs the same check. Every admin step is audited.
/// </summary>
public sealed class BillingService(
    IBillingRepository billing,
    IBillingQueries queries,
    IStoreRepository stores,
    ImageService images,
    IPlatformSettings settings,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(Now);
    private Task<int> GraceDaysAsync(CancellationToken ct) => settings.GetIntAsync(SettingKeys.BillingGraceDays, ct);

    // -- Store owner ------------------------------------------------------------------

    public async Task<Result<StoreBillingDto>> MineAsync(Guid ownerId, CancellationToken ct = default) =>
        await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false) is { } store
            ? await StoreAsync(store.Id, ct).ConfigureAwait(false)
            : Result.Fail<StoreBillingDto>(StoreErrors.NotFound);

    public async Task<Result<BillingStatusDto>> MyStatusAsync(Guid ownerId, CancellationToken ct = default)
    {
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<BillingStatusDto>(StoreErrors.NotFound);
        var status = await queries.StatusAsync(store.Id, Today, await GraceDaysAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false);
        return status is null ? Result.Fail<BillingStatusDto>(StoreErrors.NotFound) : Result.Ok(status);
    }

    /// <summary>Another store's invoice is reported as missing.</summary>
    public async Task<Result<InvoiceDetailDto>> MyInvoiceAsync(Guid ownerId, Guid invoiceId, CancellationToken ct = default)
    {
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<InvoiceDetailDto>(BillingErrors.InvoiceNotFound);
        return await InvoiceDetailAsync(invoiceId, store.Id, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// "I paid": months of subscription (the best offer applies) or an amount of one of the store's invoices (all that is
    /// left by default). One declaration at a time for the subscription, and per invoice.
    /// </summary>
    public async Task<Result<StorePaymentDto>> DeclareAsync(Guid ownerId, DeclarePaymentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<StorePaymentDto>(StoreErrors.NotFound);
        if (store.IsDeleted) return Result.Fail<StorePaymentDto>(DomainErrors.Store.Deleted);

        if (request.ReceiptImageId is { } receiptId)
        {
            var receipt = await images.RequireOwnAsync(receiptId, ImagePurpose.PaymentReceipt, ownerId, ct).ConfigureAwait(false);
            if (!receipt.IsSuccess) return Result.Fail<StorePaymentDto>(receipt.Error!);
        }
        var details = new PaymentDetails(request.Method, request.Reference, request.Note, request.PaidOn ?? Today, request.ReceiptImageId);
        var declared = await billing.DeclaredPaymentsAsync(store.Id, ct).ConfigureAwait(false);
        var number = await billing.NextPaymentNumberAsync(ct).ConfigureAwait(false);

        Result<StorePayment> payment;
        switch (request.Kind)
        {
            case StorePaymentKind.Subscription:
                if (declared.Any(p => p.Kind == StorePaymentKind.Subscription)) return Result.Fail<StorePaymentDto>(BillingErrors.AlreadyDeclared);
                var months = request.Months ?? 0;
                var best = (await ApplicableOffersAsync(store, months, ct).ConfigureAwait(false)).FirstOrDefault();
                payment = StorePayment.ForSubscription(number, store, months, best, details, ownerId, adminId: null, Now);
                break;
            case StorePaymentKind.Commission:
                var invoice = request.InvoiceId is { } id ? await billing.GetInvoiceAsync(id, ct).ConfigureAwait(false) : null;
                if (invoice is null || invoice.StoreId != store.Id) return Result.Fail<StorePaymentDto>(BillingErrors.InvoiceNotFound);
                if (declared.Any(p => p.InvoiceId == invoice.Id)) return Result.Fail<StorePaymentDto>(BillingErrors.AlreadyDeclared);
                payment = StorePayment.ForInvoice(number, invoice, request.Amount ?? invoice.Remaining, details, ownerId, adminId: null, Now);
                break;
            default:
                return Result.Fail<StorePaymentDto>(DomainErrors.Billing.MethodInvalid);
        }
        if (!payment.IsSuccess) return Result.Fail<StorePaymentDto>(payment.Error!);

        billing.Add(payment.Value);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return await PaymentDtoAsync(payment.Value.Id, ct).ConfigureAwait(false);
    }

    // -- Admin: reads -----------------------------------------------------------------

    public async Task<Result<StoreBillingDto>> StoreAsync(Guid storeId, CancellationToken ct = default) =>
        await queries.StoreAsync(storeId, Today, await GraceDaysAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false) is { } dto
            ? Result.Ok(dto)
            : Result.Fail<StoreBillingDto>(StoreErrors.NotFound);

    public Task<Result<InvoiceDetailDto>> InvoiceAsync(Guid invoiceId, CancellationToken ct = default) =>
        InvoiceDetailAsync(invoiceId, storeId: null, ct);

    public Task<PagedResult<StorePaymentDto>> PaymentsAsync(StorePaymentQuery query, CancellationToken ct = default) => queries.PaymentsAsync(query, ct);

    public async Task<IReadOnlyList<OverdueStoreItem>> OverdueAsync(CancellationToken ct = default) =>
        await queries.OverdueAsync(Today, await GraceDaysAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    public Task<IReadOnlyList<OfferDto>> OffersAsync(CancellationToken ct = default) => queries.OffersAsync(ct);

    public Task<BillingRevenueDto> RevenueAsync(StatsPeriod period, CancellationToken ct = default) => queries.RevenueAsync(period, ct);

    /// <summary>What <paramref name="months"/> months cost the store today, with every offer that applies (biggest discount first).</summary>
    public async Task<Result<SubscriptionQuote>> QuoteAsync(Guid storeId, int months, CancellationToken ct = default)
    {
        var store = await stores.GetByIdAsync(storeId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<SubscriptionQuote>(StoreErrors.NotFound);
        if (months is < 1 or > StorePayment.MaxMonths) return Result.Fail<SubscriptionQuote>(DomainErrors.Billing.MonthsInvalid);
        var gross = months * store.SubscriptionMonthlyPrice;
        var offers = await ApplicableOffersAsync(store, months, ct).ConfigureAwait(false);
        return Result.Ok(new SubscriptionQuote(months, store.SubscriptionMonthlyPrice, gross,
            [.. offers.Select(o => new QuotedOffer(o.Id, new LocalizedName(o.NameFr, o.NameAr, o.NameEn), o.DiscountOn(gross), gross - o.DiscountOn(gross)))]));
    }

    // -- Admin: price and offers ------------------------------------------------------

    public async Task<Result<StoreBillingDto>> SetPriceAsync(AdminActor actor, Guid storeId, SetSubscriptionPriceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var store = await stores.GetByIdAsync(storeId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<StoreBillingDto>(StoreErrors.NotFound);
        var before = store.SubscriptionMonthlyPrice;
        var changed = store.SetSubscriptionPrice(request.MonthlyPrice);
        if (!changed.IsSuccess) return Result.Fail<StoreBillingDto>(changed.Error!);
        if (before != store.SubscriptionMonthlyPrice) // no-ops are not audited
        {
            audit.Record(actor.Email, actor.Id, AuditActions.BillingPrice, AuditTargets.Store, store.Id, store.Name,
                new { MonthlyPrice = before }, new { MonthlyPrice = store.SubscriptionMonthlyPrice }, Optional(request.Reason));
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        }
        return await StoreAsync(storeId, ct).ConfigureAwait(false);
    }

    public async Task<Result<OfferDto>> CreateOfferAsync(AdminActor actor, SaveOfferRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var created = BillingOffer.Create(request.ToDetails());
        if (!created.IsSuccess) return Result.Fail<OfferDto>(created.Error!);
        billing.Add(created.Value);
        audit.Record(actor.Email, actor.Id, AuditActions.BillingOfferCreate, AuditTargets.Offer, created.Value.Id, created.Value.NameFr,
            null, request);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return await OfferDtoAsync(created.Value.Id, ct).ConfigureAwait(false);
    }

    public async Task<Result<OfferDto>> UpdateOfferAsync(AdminActor actor, Guid id, SaveOfferRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var offer = await billing.GetOfferAsync(id, ct).ConfigureAwait(false);
        if (offer is null) return Result.Fail<OfferDto>(BillingErrors.OfferNotFound);
        var before = (await OfferDtoAsync(id, ct).ConfigureAwait(false)).Value;
        var updated = offer.Update(request.ToDetails());
        if (!updated.IsSuccess) return Result.Fail<OfferDto>(updated.Error!);
        audit.Record(actor.Email, actor.Id, AuditActions.BillingOfferUpdate, AuditTargets.Offer, offer.Id, offer.NameFr, before, request);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return await OfferDtoAsync(id, ct).ConfigureAwait(false);
    }

    // -- Admin: payments --------------------------------------------------------------

    /// <summary>Months of subscription paid in person: approved at once, paid-until extended. The offer must apply.</summary>
    public async Task<Result<StorePaymentDto>> RecordSubscriptionAsync(AdminActor actor, Guid storeId, RecordSubscriptionPaymentRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var store = await stores.GetByIdAsync(storeId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<StorePaymentDto>(StoreErrors.NotFound);
        if (store.IsDeleted) return Result.Fail<StorePaymentDto>(DomainErrors.Store.Deleted);

        BillingOffer? offer = null;
        if (request.OfferId is { } offerId)
        {
            offer = (await ApplicableOffersAsync(store, request.Months, ct).ConfigureAwait(false)).FirstOrDefault(o => o.Id == offerId);
            if (offer is null) return Result.Fail<StorePaymentDto>(DomainErrors.Billing.OfferNotApplicable);
        }
        var details = new PaymentDetails(request.Method, request.Reference, request.Note, request.PaidOn ?? Today, null);
        return await RecordAsync(actor, store, number =>
            StorePayment.ForSubscription(number, store, request.Months, offer, details, ownerId: null, actor.Id, Now), ct).ConfigureAwait(false);
    }

    /// <summary>All or part of an invoice paid in person: approved at once.</summary>
    public async Task<Result<StorePaymentDto>> RecordInvoicePaymentAsync(AdminActor actor, Guid invoiceId, RecordInvoicePaymentRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var invoice = await billing.GetInvoiceAsync(invoiceId, ct).ConfigureAwait(false);
        if (invoice is null) return Result.Fail<StorePaymentDto>(BillingErrors.InvoiceNotFound);
        var store = await stores.GetByIdAsync(invoice.StoreId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<StorePaymentDto>(StoreErrors.NotFound);

        var details = new PaymentDetails(request.Method, request.Reference, request.Note, request.PaidOn ?? Today, null);
        return await RecordAsync(actor, store, number =>
            StorePayment.ForInvoice(number, invoice, request.Amount, details, ownerId: null, actor.Id, Now), ct).ConfigureAwait(false);
    }

    private async Task<Result<StorePaymentDto>> RecordAsync(AdminActor actor, Store store, Func<long, Result<StorePayment>> create,
        CancellationToken ct)
    {
        var result = await GuardAsync(() => unitOfWork.InTransactionAsync(async () =>
        {
            var number = await billing.NextPaymentNumberAsync(ct).ConfigureAwait(false);
            var created = create(number);
            if (!created.IsSuccess) return Result.Fail<Guid>(created.Error!);
            var payment = created.Value;

            var applied = await ApplyAsync(payment, store, ct).ConfigureAwait(false);
            if (!applied.IsSuccess) return Result.Fail<Guid>(applied.Error!);
            billing.Add(payment);
            await RecheckAsync(store, Now, ct).ConfigureAwait(false);
            audit.Record(actor.Email, actor.Id, AuditActions.BillingPaymentRecord, AuditTargets.StorePayment, payment.Id,
                $"{payment.Number} · {store.Name}", null, Snapshot(payment));
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            return Result.Ok(payment.Id);
        }, ct)).ConfigureAwait(false);
        return result.IsSuccess ? await PaymentDtoAsync(result.Value, ct).ConfigureAwait(false) : Result.Fail<StorePaymentDto>(result.Error!);
    }

    public Task<Result<StorePaymentDto>> ApproveAsync(AdminActor actor, Guid paymentId, CancellationToken ct = default) =>
        DecideAsync(actor, paymentId, AuditActions.BillingPaymentApprove, null, async (payment, store) =>
        {
            var approved = payment.Approve(actor.Id, Now);
            return approved.IsSuccess ? await ApplyAsync(payment, store, ct).ConfigureAwait(false) : approved;
        }, ct);

    /// <summary>Refused: it no longer stops the countdown, so the store may be suspended at once if it is overdue.</summary>
    public Task<Result<StorePaymentDto>> RejectAsync(AdminActor actor, Guid paymentId, RejectStorePaymentRequest request, CancellationToken ct = default) =>
        DecideAsync(actor, paymentId, AuditActions.BillingPaymentReject, request?.Reason,
            (payment, _) => Task.FromResult(payment.Reject(actor.Id, request?.Reason, Now)), ct);

    private async Task<Result<StorePaymentDto>> DecideAsync(AdminActor actor, Guid paymentId, string action, string? reason,
        Func<StorePayment, Store, Task<Result>> decide, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var payment = await billing.GetPaymentAsync(paymentId, ct).ConfigureAwait(false);
        if (payment is null) return Result.Fail<StorePaymentDto>(BillingErrors.PaymentNotFound);
        var store = await stores.GetByIdAsync(payment.StoreId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<StorePaymentDto>(StoreErrors.NotFound);
        var before = Snapshot(payment);

        var result = await GuardAsync(() => unitOfWork.InTransactionAsync(async () =>
        {
            var decided = await decide(payment, store).ConfigureAwait(false);
            if (!decided.IsSuccess) return Result.Fail<bool>(decided.Error!);
            await RecheckAsync(store, Now, ct).ConfigureAwait(false);
            audit.Record(actor.Email, actor.Id, action, AuditTargets.StorePayment, payment.Id, $"{payment.Number} · {store.Name}",
                before, Snapshot(payment), reason);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            return Result.Ok(true);
        }, ct)).ConfigureAwait(false);
        return result.IsSuccess ? await PaymentDtoAsync(payment.Id, ct).ConfigureAwait(false) : Result.Fail<StorePaymentDto>(result.Error!);
    }

    /// <summary>What an approved payment changes: the paid-until date, or the invoice.</summary>
    private async Task<Result> ApplyAsync(StorePayment payment, Store store, CancellationToken ct)
    {
        if (payment.Kind == StorePaymentKind.Subscription)
        {
            var (from, to) = store.ExtendSubscription(payment.Months!.Value, Today);
            payment.Cover(from, to);
            return Result.Ok();
        }
        var invoice = await billing.GetInvoiceAsync(payment.InvoiceId!.Value, ct).ConfigureAwait(false);
        return invoice is null ? Result.Fail(BillingErrors.InvoiceNotFound) : invoice.ApplyPayment(payment.Amount, Now);
    }

    // -- Checks (the hourly job, and right after every payment decision) ---------------------

    /// <summary>Issues the invoices of past months and suspends / reactivates every store. Safe to run again.</summary>
    public async Task<BillingRunResult> RunAsync(CancellationToken ct = default)
    {
        var total = new BillingRunResult(0, 0, 0);
        var now = Now;
        foreach (var storeId in await billing.StoreIdsAsync(ct).ConfigureAwait(false))
        {
            var r = await RunForStoreAsync(storeId, now, ct).ConfigureAwait(false);
            total = new BillingRunResult(total.InvoicesIssued + r.InvoicesIssued, total.StoresSuspended + r.StoresSuspended,
                total.StoresReactivated + r.StoresReactivated);
        }
        return total;
    }

    /// <summary>One store, as of <paramref name="utcNow"/> (tests pass a later date to move through months).</summary>
    public async Task<BillingRunResult> RunForStoreAsync(Guid storeId, DateTime utcNow, CancellationToken ct = default)
    {
        var result = await GuardAsync(() => unitOfWork.InTransactionAsync(async () =>
        {
            var store = await stores.GetByIdAsync(storeId, ct).ConfigureAwait(false);
            if (store is null || store.IsDeleted) return Result.Ok(new BillingRunResult(0, 0, 0));
            var issued = await IssueInvoicesAsync(store, utcNow, ct).ConfigureAwait(false);
            var before = store.Status;
            await RecheckAsync(store, utcNow, ct).ConfigureAwait(false);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            return Result.Ok(new BillingRunResult(issued,
                before != StoreStatus.Suspended && store.IsSuspended ? 1 : 0,
                before == StoreStatus.Suspended && !store.IsSuspended ? 1 : 0));
        }, ct)).ConfigureAwait(false);
        // Someone changed the store meanwhile: the next run picks it up.
        return result.IsSuccess ? result.Value : new BillingRunResult(0, 0, 0);
    }

    /// <summary>
    /// One invoice per past month with something to bill, oldest first: a month gets every not-yet-billed entry credited
    /// before its end. A month whose net is not positive is skipped — its entries move on to the next month.
    /// </summary>
    private async Task<int> IssueInvoicesAsync(Store store, DateTime utcNow, CancellationToken ct)
    {
        var currentMonth = new DateOnly(utcNow.Year, utcNow.Month, 1);
        var start = currentMonth.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var pending = (await billing.UnbilledLinesAsync(store.Id, start, ct).ConfigureAwait(false)).ToList();
        if (pending.Count == 0) return 0;

        var dueDay = await settings.GetIntAsync(SettingKeys.CommissionInvoiceDueDay, ct).ConfigureAwait(false);
        var issued = 0;
        var first = pending.Min(l => l.CreditedAt);
        for (var month = new DateOnly(first.Year, first.Month, 1); month < currentMonth; month = month.AddMonths(1))
        {
            var end = month.AddMonths(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var lines = pending.Where(l => l.CreditedAt < end).ToList();
            if (lines.Count == 0 || await billing.InvoiceExistsAsync(store.Id, month, ct).ConfigureAwait(false)) continue;

            var invoice = CommissionInvoice.Issue(await billing.NextInvoiceNumberAsync(ct).ConfigureAwait(false), store.Id, store.Name, month, lines, dueDay);
            if (!invoice.IsSuccess) continue;
            billing.Add(invoice.Value);
            pending.RemoveAll(lines.Contains);
            issued++;
        }
        return issued;
    }

    /// <summary>Suspends the store when something is overdue (no payment waiting for it), reactivates it when nothing is.</summary>
    private async Task RecheckAsync(Store store, DateTime utcNow, CancellationToken ct)
    {
        var grace = await GraceDaysAsync(ct).ConfigureAwait(false);
        // Tracked entities come back with their in-memory state: a payment approved in this transaction no longer counts as declared.
        var declared = (await billing.DeclaredPaymentsAsync(store.Id, ct).ConfigureAwait(false)).Where(p => p.IsDeclared).ToList();
        var invoices = await billing.UnpaidInvoicesAsync(store.Id, ct).ConfigureAwait(false);
        var dues = invoices
            .Select(i => new BillingDue(i.DueOn, i.IsPaid, declared.Any(p => p.InvoiceId == i.Id)))
            .Prepend(new BillingDue(store.PaidUntil, false, declared.Any(p => p.Kind == StorePaymentKind.Subscription)));

        var state = BillingRules.Evaluate(dues, DateOnly.FromDateTime(utcNow), grace);
        if (state.Overdue) store.Suspend(utcNow);
        else store.Reactivate();
    }

    // -- Helpers ----------------------------------------------------------------------

    /// <summary>Offers this store can use for <paramref name="months"/> months today, biggest discount first.</summary>
    private async Task<List<BillingOffer>> ApplicableOffersAsync(Store store, int months, CancellationToken ct)
    {
        var first = !await billing.HasPaidSubscriptionAsync(store.Id, ct).ConfigureAwait(false);
        var gross = months * store.SubscriptionMonthlyPrice;
        return [.. (await billing.ActiveOffersAsync(ct).ConfigureAwait(false))
            .Where(o => o.AppliesTo(store.Id, store.CategoryId, months, first, Today))
            .OrderByDescending(o => o.DiscountOn(gross)).ThenBy(o => o.NameFr, StringComparer.Ordinal)];
    }

    private async Task<Result<InvoiceDetailDto>> InvoiceDetailAsync(Guid invoiceId, Guid? storeId, CancellationToken ct) =>
        await queries.InvoiceAsync(invoiceId, storeId, Today, await GraceDaysAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false) is { } dto
            ? Result.Ok(dto)
            : Result.Fail<InvoiceDetailDto>(BillingErrors.InvoiceNotFound);

    private async Task<Result<StorePaymentDto>> PaymentDtoAsync(Guid id, CancellationToken ct) =>
        await queries.PaymentAsync(id, ct).ConfigureAwait(false) is { } dto ? Result.Ok(dto) : Result.Fail<StorePaymentDto>(BillingErrors.PaymentNotFound);

    private async Task<Result<OfferDto>> OfferDtoAsync(Guid id, CancellationToken ct) =>
        (await queries.OffersAsync(ct).ConfigureAwait(false)).FirstOrDefault(o => o.Id == id) is { } dto
            ? Result.Ok(dto)
            : Result.Fail<OfferDto>(BillingErrors.OfferNotFound);

    /// <summary>A concurrent change of the same invoice / payment (xmin) is reported as <c>billing.changed</c>.</summary>
    private static async Task<Result<T>> GuardAsync<T>(Func<Task<Result<T>>> work)
    {
        try
        {
            return await work().ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Fail<T>(BillingErrors.Changed);
        }
    }

    internal static object Snapshot(StorePayment p) => new
    {
        p.Number, p.Kind, p.Status, p.Amount, p.Method, p.Reference, p.Months, p.MonthlyPrice, p.OfferName, p.Discount,
        p.CoversFrom, p.CoversTo, p.InvoiceId, p.RejectionReason,
    };

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
