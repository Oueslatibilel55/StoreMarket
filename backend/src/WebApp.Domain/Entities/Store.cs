using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Domain.Entities;

/// <summary>Everything the owner edits about their store, validated together.</summary>
public sealed record StoreDetails(
    string Name,
    string? Slug,
    Guid CategoryId,
    string? Description,
    string Phone,
    string City,
    string? Address,
    Guid? LogoImageId,
    Guid? BannerImageId,
    int DefaultAffiliatePercent,
    decimal DeliveryFee,
    IReadOnlyList<string> DeliveryCities,
    int DeliveryMinDays,
    int DeliveryMaxDays,
    string? FacebookUrl,
    string? InstagramUrl,
    string? WebsiteUrl);

/// <summary>
/// A business owner's shop: one per owner in v1, live as soon as it is created (no approval).
/// The default affiliate % and the subscription price are copied onto orders / payments when
/// they happen, so changing them never rewrites history.
/// </summary>
public sealed class Store : AggregateRoot
{
    public const int MinNameLength = 2;
    public const int MaxNameLength = 80;
    public const int MaxDescriptionLength = 2000;
    public const int MaxAddressLength = 200;
    public const int MaxUrlLength = 200;
    public const decimal MaxDeliveryFee = 1000m;
    public const int MaxDeliveryDays = 30;
    public const int MaxModerationReasonLength = 500;

    /// <summary>Reason stored when the store is disabled because its owner lost the StoreOwner role (translated by the client).</summary>
    public const string OwnerRoleRevokedReason = "store.ownerRoleRevoked";

    private readonly List<string> _deliveryCities = [];

    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = null!;
    /// <summary>Public URL name: <c>/stores/{slug}</c>. Unique.</summary>
    public string Slug { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public string? Description { get; private set; }
    public string Phone { get; private set; } = null!;
    /// <summary>Governorate code (<see cref="Governorates"/>).</summary>
    public string City { get; private set; } = null!;
    public string? Address { get; private set; }
    public Guid? LogoImageId { get; private set; }
    public Guid? BannerImageId { get; private set; }
    public StoreStatus Status { get; private set; }

    /// <summary>Commission given to affiliates on products without their own % (009). Within the platform limits.</summary>
    public int DefaultAffiliatePercent { get; private set; }
    public decimal DeliveryFee { get; private set; }
    public IReadOnlyList<string> DeliveryCities => _deliveryCities;
    public int DeliveryMinDays { get; private set; }
    public int DeliveryMaxDays { get; private set; }

    public string? FacebookUrl { get; private set; }
    public string? InstagramUrl { get; private set; }
    public string? WebsiteUrl { get; private set; }

    /// <summary>Monthly price for this store, copied from the default setting at creation; the admin changes it per store (025).</summary>
    public decimal SubscriptionMonthlyPrice { get; private set; }
    /// <summary>Last day the subscription covers (025): the trial days for a new store, extended by every approved payment.</summary>
    public DateOnly PaidUntil { get; private set; }
    /// <summary>When billing suspended the store for an unpaid subscription or invoice (025); null otherwise.</summary>
    public DateTime? SuspendedAt { get; private set; }

    /// <summary>Why an admin disabled or deleted the store — shown to the owner (010).</summary>
    public string? ModerationReason { get; private set; }
    public DateTime? DisabledAt { get; private set; }
    /// <summary>Soft delete by an admin: hidden everywhere, kept for order and coin history.</summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>Average of the visible store reviews (017), two decimals; null when none.</summary>
    /// <remarks>The three rating values are maintained by the review repository, in the review's transaction.</remarks>
    public decimal? AverageRating { get; private set; }
    public int RatingCount { get; private set; }
    /// <summary>Bayesian average used to sort the shop (<see cref="Ranking"/>); null when not rated.</summary>
    public decimal? RankingScore { get; private set; }

    private Store() { }

    /// <summary>
    /// New active store. <paramref name="percentMin"/> / <paramref name="percentMax"/> are the platform
    /// limits (settings). Category, images and slug uniqueness are checked by the caller.
    /// </summary>
    public static Result<Store> Create(Guid ownerId, StoreDetails details, int percentMin, int percentMax, decimal subscriptionMonthlyPrice,
        DateOnly paidUntil)
    {
        var store = new Store
        {
            OwnerId = ownerId, Status = StoreStatus.Active, SubscriptionMonthlyPrice = subscriptionMonthlyPrice, PaidUntil = paidUntil,
        };
        var applied = store.Apply(details, percentMin, percentMax);
        return applied.IsSuccess ? Result.Ok(store) : Result.Fail<Store>(applied.Error!);
    }

    public Result Update(StoreDetails details, int percentMin, int percentMax)
    {
        if (IsDeleted) return Result.Fail(DomainErrors.Store.Deleted);
        var applied = Apply(details, percentMin, percentMax);
        if (applied.IsSuccess) MarkUpdated();
        return applied;
    }

    public bool IsOpen => Status == StoreStatus.Active;
    public bool IsDeleted => DeletedAt is not null;

    /// <summary>
    /// An admin disables the store (fake, revoked owner…): it and its products leave the shop, the owner
    /// sees the reason and can't reopen it. Disabling an already disabled store changes nothing.
    /// </summary>
    public Result Disable(string? reason, DateTime utcNow)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxModerationReasonLength)
            return Result.Fail(DomainErrors.Moderation.ReasonRequired);
        if (IsDeleted) return Result.Fail(DomainErrors.Store.Deleted);
        if (Status == StoreStatus.Disabled) return Result.Ok();

        Status = StoreStatus.Disabled;
        SuspendedAt = null; // the admin decision replaces a billing suspension
        ModerationReason = trimmed;
        DisabledAt = utcNow;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>The admin lifts a disable: the store is active again. A deleted store stays deleted.</summary>
    public Result Enable()
    {
        if (IsDeleted) return Result.Fail(DomainErrors.Store.Deleted);
        if (Status != StoreStatus.Disabled) return Result.Ok();

        Status = StoreStatus.Active;
        ModerationReason = null;
        DisabledAt = null;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>Soft delete by an admin (final): hidden everywhere, the owner can't manage it any more.</summary>
    public Result Delete(string? reason, DateTime utcNow)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxModerationReasonLength)
            return Result.Fail(DomainErrors.Moderation.ReasonRequired);
        if (IsDeleted) return Result.Ok();

        Status = StoreStatus.Disabled;
        SuspendedAt = null; // the admin decision replaces a billing suspension
        ModerationReason = trimmed;
        DisabledAt ??= utcNow;
        DeletedAt = utcNow;
        MarkUpdated();
        return Result.Ok();
    }

    // -- Billing (025) -----------------------------------------------------------

    public const decimal MaxSubscriptionPrice = 100_000m;

    /// <summary>The admin sets this store's monthly price. Past payments keep the price they were made at.</summary>
    public Result SetSubscriptionPrice(decimal monthlyPrice)
    {
        if (IsDeleted) return Result.Fail(DomainErrors.Store.Deleted);
        if (monthlyPrice < 0 || monthlyPrice > MaxSubscriptionPrice || decimal.Round(monthlyPrice, 3) != monthlyPrice)
            return Result.Fail(DomainErrors.Billing.PriceInvalid);
        if (monthlyPrice == SubscriptionMonthlyPrice) return Result.Ok();
        SubscriptionMonthlyPrice = monthlyPrice;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>
    /// An approved payment of <paramref name="months"/> months: counted from the day after paid-until, or from today when
    /// paid-until is already past (the months a store could not sell are not charged afterwards). Returns the days covered.
    /// </summary>
    public (DateOnly From, DateOnly To) ExtendSubscription(int months, DateOnly today)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(months, 1);
        var from = PaidUntil >= today ? PaidUntil.AddDays(1) : today;
        var to = from.AddMonths(months).AddDays(-1);
        PaidUntil = to;
        MarkUpdated();
        return (from, to);
    }

    public bool IsSuspended => Status == StoreStatus.Suspended;

    /// <summary>
    /// Billing suspends an active or closed store (unpaid subscription or invoice): out of the shop, no new orders, the
    /// owner still processes the orders already placed. A disabled or deleted store is left alone. True when it changed.
    /// </summary>
    public bool Suspend(DateTime utcNow)
    {
        if (IsDeleted || Status is not (StoreStatus.Active or StoreStatus.Closed)) return false;
        Status = StoreStatus.Suspended;
        SuspendedAt = utcNow;
        MarkUpdated();
        return true;
    }

    /// <summary>Everything overdue is paid: a suspended store is active again. True when it changed.</summary>
    public bool Reactivate()
    {
        if (Status != StoreStatus.Suspended) return false;
        Status = StoreStatus.Active;
        SuspendedAt = null;
        MarkUpdated();
        return true;
    }

    /// <summary>The owner closes their store for a while. Only an active store can be closed.</summary>
    public Result Close()
    {
        if (Status == StoreStatus.Closed) return Result.Ok();
        if (Status != StoreStatus.Active) return Result.Fail(DomainErrors.Store.NotOwnerControlled);
        Status = StoreStatus.Closed;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>The owner reopens. A store disabled by an admin or suspended for payment can't be reopened this way.</summary>
    public Result Reopen()
    {
        if (Status == StoreStatus.Active) return Result.Ok();
        if (Status != StoreStatus.Closed) return Result.Fail(DomainErrors.Store.NotOwnerControlled);
        Status = StoreStatus.Active;
        MarkUpdated();
        return Result.Ok();
    }

    private Result Apply(StoreDetails d, int percentMin, int percentMax)
    {
        ArgumentNullException.ThrowIfNull(d);

        var name = d.Name?.Trim();
        if (string.IsNullOrEmpty(name)) return Result.Fail(DomainErrors.Store.NameRequired);
        if (name.Length is < MinNameLength or > MaxNameLength) return Result.Fail(DomainErrors.Store.NameLength);

        var slug = Common.Slug.FromInputOrName(d.Slug, name);
        if (!Common.Slug.IsValid(slug)) return Result.Fail(DomainErrors.Store.SlugInvalid);

        var description = Optional(d.Description);
        if (description?.Length > MaxDescriptionLength) return Result.Fail(DomainErrors.Store.DescriptionTooLong);

        var phone = User.NormalizePhone(d.Phone);
        if (phone is null) return Result.Fail(DomainErrors.Store.PhoneInvalid);

        if (!Governorates.IsValid(d.City)) return Result.Fail(DomainErrors.Store.CityInvalid);
        var address = Optional(d.Address);
        if (address?.Length > MaxAddressLength) return Result.Fail(DomainErrors.Store.AddressTooLong);

        if (d.DefaultAffiliatePercent < percentMin || d.DefaultAffiliatePercent > percentMax)
            return Result.Fail(DomainErrors.Store.PercentOutOfRange);

        if (d.DeliveryFee < 0 || d.DeliveryFee > MaxDeliveryFee || decimal.Round(d.DeliveryFee, 3) != d.DeliveryFee)
            return Result.Fail(DomainErrors.Store.DeliveryFeeInvalid);

        var cities = (d.DeliveryCities ?? []).Distinct(StringComparer.Ordinal).ToList();
        if (cities.Count == 0) return Result.Fail(DomainErrors.Store.DeliveryCitiesRequired);
        if (!cities.All(Governorates.IsValid)) return Result.Fail(DomainErrors.Store.CityInvalid);

        if (d.DeliveryMinDays < 0 || d.DeliveryMaxDays > MaxDeliveryDays || d.DeliveryMinDays > d.DeliveryMaxDays)
            return Result.Fail(DomainErrors.Store.DeliveryDelayInvalid);

        var facebook = Link(d.FacebookUrl, "facebook.com", "fb.com");
        var instagram = Link(d.InstagramUrl, "instagram.com");
        var website = Link(d.WebsiteUrl);
        if (!facebook.IsSuccess || !instagram.IsSuccess || !website.IsSuccess) return Result.Fail(DomainErrors.Store.LinkInvalid);

        Name = name;
        Slug = slug;
        CategoryId = d.CategoryId;
        Description = description;
        Phone = phone;
        City = d.City;
        Address = address;
        LogoImageId = d.LogoImageId;
        BannerImageId = d.BannerImageId;
        DefaultAffiliatePercent = d.DefaultAffiliatePercent;
        DeliveryFee = d.DeliveryFee;
        _deliveryCities.Clear();
        _deliveryCities.AddRange(Governorates.All.Where(cities.Contains)); // canonical order
        DeliveryMinDays = d.DeliveryMinDays;
        DeliveryMaxDays = d.DeliveryMaxDays;
        FacebookUrl = facebook.Value;
        InstagramUrl = instagram.Value;
        WebsiteUrl = website.Value;
        return Result.Ok();
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Optional http(s) link; when <paramref name="hosts"/> are given, it must point to one of them.</summary>
    private static Result<string?> Link(string? raw, params string[] hosts)
    {
        var value = Optional(raw);
        if (value is null) return Result.Ok<string?>(null);
        if (value.Length > MaxUrlLength
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return Result.Fail<string?>(DomainErrors.Store.LinkInvalid);
        if (hosts.Length > 0 && !hosts.Any(h => uri.Host == h || uri.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase)))
            return Result.Fail<string?>(DomainErrors.Store.LinkInvalid);
        return Result.Ok<string?>(value);
    }
}
