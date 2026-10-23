namespace WebApp.Application.DTOs;

/// <summary>
/// Days covered by a stats screen (023), both inclusive, in UTC: <c>Start</c> = first day 00:00, <c>End</c> = the day after
/// the last one (exclusive). Default: the last <see cref="DefaultDays"/> days up to today; at most <see cref="MaxDays"/>.
/// </summary>
public sealed record StatsPeriod(DateOnly From, DateOnly To)
{
    public const int DefaultDays = 30;
    public const int MaxDays = 366;

    public DateTime Start => From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    public DateTime End => To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    public int Days => To.DayNumber - From.DayNumber + 1;

    /// <summary>Missing ends get defaults, reversed ends are swapped, a too long period keeps its last <see cref="MaxDays"/> days.</summary>
    public static StatsPeriod Of(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var end = to ?? today;
        var start = from ?? end.AddDays(1 - DefaultDays);
        if (start > end) (start, end) = (end, start);
        if (end.DayNumber - start.DayNumber + 1 > MaxDays) start = end.AddDays(1 - MaxDays);
        return new StatsPeriod(start, end);
    }
}

/// <summary>Store orders <b>placed</b> in the period, by their current status.</summary>
public sealed record ConversionStats(int Placed, int Open, int Delivered, int Cancelled, int NotDelivered);

/// <summary>
/// What was <b>delivered</b> in the period: store orders, items sold (TND, delivery fees excluded) and quantity — and the
/// part that came with an affiliate code. For an affiliate or a product, only their own lines count.
/// </summary>
public sealed record SalesStats(int Orders, decimal Amount, int Quantity, int OrdersViaAffiliates, decimal AmountViaAffiliates);

/// <summary>A row of a ranking or a breakdown: <c>Count</c> = delivered orders or lines, <c>Amount</c> = TND, <c>Coins</c> earned.</summary>
public sealed record StatsRow(Guid Id, string Name, string? Detail, int Count, int Quantity, decimal Amount, decimal Coins);

public sealed record DailyStats(DateOnly Date, int Placed, int Delivered, decimal Sales);

/// <summary>Coins: <c>Earned</c> / <c>Adjusted</c> in the period, <c>Expected</c> now (parcels on their way), paid out in the period.</summary>
public sealed record CoinStats(decimal Earned, decimal Adjusted, decimal Expected, decimal PaidOut, decimal PaidOutTnd);

/// <summary>Payout requests open now, and the ones paid in the period.</summary>
public sealed record PayoutStats(int Pending, decimal PendingCoins, decimal PendingTnd, int Paid, decimal PaidTnd);

/// <summary>Stores right now (deleted ones left out).</summary>
public sealed record StoreCounts(int Active, int Closed, int Disabled, int Suspended);

public sealed record PartnershipCounts(int Requested, int Accepted, int Refused, int Ended);

/// <summary>
/// Store billing (025) in the period: subscriptions and commissions collected (approved payments by payment date),
/// commissions billed (invoices of the months in the period); <c>CommissionsDue</c> and <c>DeclaredPayments</c> = now.
/// </summary>
public sealed record BillingStats(decimal SubscriptionsCollected, decimal CommissionsBilled, decimal CommissionsCollected, decimal CommissionsDue,
    int DeclaredPayments);

public sealed record AdminOverviewDto(
    StatsPeriod Period,
    ConversionStats Orders,
    SalesStats Sales,
    CoinStats Coins,
    PayoutStats Payouts,
    StoreCounts Stores,
    BillingStats Billing,
    int ActiveAffiliates,
    int NewUsers,
    IReadOnlyList<DailyStats> Daily,
    IReadOnlyList<StatsRow> TopStores,
    IReadOnlyList<StatsRow> TopProducts,
    IReadOnlyList<StatsRow> TopAffiliates);

/// <summary>An affiliate's results: orders with their code, coins per store / per product (ledger, <c>Earned</c> only).</summary>
public sealed record AffiliateStatsDto(
    StatsPeriod Period,
    ConversionStats Orders,
    SalesStats Sales,
    decimal CoinsEarned,
    decimal CoinsExpected,
    PartnershipCounts Partnerships,
    IReadOnlyList<StatsRow> ByStore,
    IReadOnlyList<StatsRow> ByProduct);

/// <summary>A store's results (the admin's view and the owner's dashboard): coins it generated for affiliates, per product too.</summary>
public sealed record StoreStatsDto(
    StatsPeriod Period,
    ConversionStats Orders,
    SalesStats Sales,
    decimal CoinsGenerated,
    PartnershipCounts Partnerships,
    RatingDto Rating,
    IReadOnlyList<StatsRow> TopProducts,
    IReadOnlyList<StatsRow> TopAffiliates);

public sealed record ProductStatsDto(
    StatsPeriod Period,
    Guid Id,
    string Name,
    string Slug,
    Guid StoreId,
    string StoreName,
    decimal Price,
    int AffiliatePercent,
    bool IsVisible,
    RatingDto Rating,
    ConversionStats Orders,
    SalesStats Sales,
    decimal CoinsEarned,
    IReadOnlyList<StatsRow> TopAffiliates);
