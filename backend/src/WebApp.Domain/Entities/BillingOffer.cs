using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Domain.Entities;

/// <summary>Everything the admin sets on a subscription offer, validated together.</summary>
public sealed record OfferDetails(
    string? NameFr,
    string? NameAr,
    string? NameEn,
    int? Months,
    int? DiscountPercent,
    decimal? DiscountAmount,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    OfferTarget Target,
    IReadOnlyList<Guid>? TargetIds,
    bool FirstPaymentOnly,
    bool IsActive);

/// <summary>
/// A subscription discount rule (025): "pay 3 months → −20 %", "first month free for new stores", "−50 % for these
/// stores"… The discount is a % of the payment or a fixed TND amount off it; the one actually used is frozen on the
/// payment, so changing or ending an offer never rewrites past payments.
/// </summary>
public sealed class BillingOffer : AggregateRoot
{
    public const int MaxNameLength = 100;
    public const int MaxMonths = 36;
    public const int MaxTargets = 200;

    private readonly List<Guid> _targetIds = [];

    public string NameFr { get; private set; } = null!;
    public string? NameAr { get; private set; }
    public string? NameEn { get; private set; }
    /// <summary>The exact number of months the payment must cover; null = any.</summary>
    public int? Months { get; private set; }
    public int? DiscountPercent { get; private set; }
    /// <summary>TND off the whole payment.</summary>
    public decimal? DiscountAmount { get; private set; }
    public DateOnly? StartsOn { get; private set; }
    public DateOnly? EndsOn { get; private set; }
    public OfferTarget Target { get; private set; }
    /// <summary>Store category ids or store ids, depending on <see cref="Target"/>.</summary>
    public IReadOnlyList<Guid> TargetIds => _targetIds;
    /// <summary>Only a store's first subscription payment (a launch offer).</summary>
    public bool FirstPaymentOnly { get; private set; }
    public bool IsActive { get; private set; }

    private BillingOffer() { }

    public static Result<BillingOffer> Create(OfferDetails details)
    {
        var offer = new BillingOffer();
        var applied = offer.Apply(details);
        return applied.IsSuccess ? Result.Ok(offer) : Result.Fail<BillingOffer>(applied.Error!);
    }

    public Result Update(OfferDetails details)
    {
        var applied = Apply(details);
        if (applied.IsSuccess) MarkUpdated();
        return applied;
    }

    /// <summary>Can this offer be used for <paramref name="months"/> months of this store, paid on <paramref name="today"/>?</summary>
    public bool AppliesTo(Guid storeId, Guid storeCategoryId, int months, bool isFirstPayment, DateOnly today) =>
        IsActive
        && (Months is null || Months == months)
        && (StartsOn is null || today >= StartsOn)
        && (EndsOn is null || today <= EndsOn)
        && (!FirstPaymentOnly || isFirstPayment)
        && Target switch
        {
            OfferTarget.AllStores => true,
            OfferTarget.StoreCategories => _targetIds.Contains(storeCategoryId),
            OfferTarget.Stores => _targetIds.Contains(storeId),
            _ => false,
        };

    /// <summary>TND off a payment of <paramref name="gross"/> (never more than the payment itself), rounded to the millime.</summary>
    public decimal DiscountOn(decimal gross)
    {
        var off = DiscountPercent is { } percent ? gross * percent / 100m : DiscountAmount ?? 0m;
        return decimal.Round(Math.Min(gross, Math.Max(0m, off)), 3, MidpointRounding.AwayFromZero);
    }

    private Result Apply(OfferDetails d)
    {
        ArgumentNullException.ThrowIfNull(d);
        var fr = d.NameFr?.Trim();
        if (string.IsNullOrEmpty(fr)) return Result.Fail(DomainErrors.Billing.OfferNameRequired);
        var ar = Optional(d.NameAr);
        var en = Optional(d.NameEn);
        if (new[] { fr, ar, en }.Any(n => n?.Length > MaxNameLength)) return Result.Fail(DomainErrors.Billing.OfferNameTooLong);

        if (d.Months is { } m && (m < 1 || m > MaxMonths)) return Result.Fail(DomainErrors.Billing.OfferMonthsInvalid);
        // Exactly one kind of discount.
        var percentOk = d.DiscountPercent is >= 1 and <= 100;
        var amountOk = d.DiscountAmount is { } a && a > 0 && a <= Store.MaxSubscriptionPrice && decimal.Round(a, 3) == a;
        if ((d.DiscountPercent is null) == (d.DiscountAmount is null) || (d.DiscountPercent is not null && !percentOk)
            || (d.DiscountAmount is not null && !amountOk))
            return Result.Fail(DomainErrors.Billing.OfferDiscountInvalid);
        if (d.StartsOn is { } s && d.EndsOn is { } e && e < s) return Result.Fail(DomainErrors.Billing.OfferDatesInvalid);

        if (!Enum.IsDefined(d.Target)) return Result.Fail(DomainErrors.Billing.OfferTargetInvalid);
        var ids = d.Target == OfferTarget.AllStores ? [] : (d.TargetIds ?? []).Distinct().ToList();
        if (d.Target != OfferTarget.AllStores && (ids.Count == 0 || ids.Count > MaxTargets))
            return Result.Fail(DomainErrors.Billing.OfferTargetInvalid);

        NameFr = fr;
        NameAr = ar;
        NameEn = en;
        Months = d.Months;
        DiscountPercent = d.DiscountPercent;
        DiscountAmount = d.DiscountAmount;
        StartsOn = d.StartsOn;
        EndsOn = d.EndsOn;
        Target = d.Target;
        _targetIds.Clear();
        _targetIds.AddRange(ids);
        FirstPaymentOnly = d.FirstPaymentOnly;
        IsActive = d.IsActive;
        return Result.Ok();
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
