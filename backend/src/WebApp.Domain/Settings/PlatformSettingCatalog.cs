namespace WebApp.Domain.Settings;

/// <summary>Keys of the platform settings. Stable identifiers: the client translates <c>settings.&lt;key&gt;</c>.</summary>
public static class SettingKeys
{
    public const string Currency = "currency";
    public const string CoinValueInTnd = "coin.valueInTnd";
    public const string AffiliatePercentMin = "affiliatePercent.min";
    public const string AffiliatePercentMax = "affiliatePercent.max";
    public const string AffiliateCodeLetters = "affiliateCode.letters";
    public const string AffiliateCodeMinDigits = "affiliateCode.minDigits";
    public const string SubscriptionDefaultMonthlyPrice = "subscription.defaultMonthlyPriceTnd";
    public const string BillingGraceDays = "billing.graceDays";
    public const string CommissionInvoiceDueDay = "commissionInvoice.dueDayOfMonth";
    public const string PayoutMinimumCoins = "payout.minimumCoins";
    public const string RankingMinReviewsPrior = "ranking.minReviewsPrior";
    public const string UploadMaxImageSizeMb = "upload.maxImageSizeMb";
}

/// <summary>
/// Every business value the admin can tune without a redeploy, with its default and limits.
/// A value missing from the database means "default". Consumers that need a value to stay
/// fixed (an order line's %, a ledger entry's coin rate) copy it onto their own record —
/// changing a setting never rewrites history.
/// </summary>
public static class PlatformSettingCatalog
{
    public static readonly IReadOnlyList<SettingDefinition> All =
    [
        new(SettingKeys.Currency, "general", SettingType.Text, "TND", Editable: false),
        new(SettingKeys.UploadMaxImageSizeMb, "general", SettingType.WholeNumber, "5", Min: 1m, Max: 20m, Unit: "mb"),

        new(SettingKeys.CoinValueInTnd, "coins", SettingType.Number, "1", Min: 0.001m, Max: 1000m, Unit: "tnd"),
        new(SettingKeys.PayoutMinimumCoins, "coins", SettingType.Number, "80", Min: 0m, Max: 1_000_000m, Unit: "coins"),

        new(SettingKeys.AffiliatePercentMin, "affiliates", SettingType.WholeNumber, "0", Min: 0m, Max: 100m, Unit: "percent"),
        new(SettingKeys.AffiliatePercentMax, "affiliates", SettingType.WholeNumber, "50", Min: 0m, Max: 100m, Unit: "percent"),
        new(SettingKeys.AffiliateCodeLetters, "affiliates", SettingType.WholeNumber, "3",
            Min: Common.AffiliateCode.MinLetters, Max: Common.AffiliateCode.MaxLetters, Unit: "letters"),
        new(SettingKeys.AffiliateCodeMinDigits, "affiliates", SettingType.WholeNumber, "2",
            Min: Common.AffiliateCode.MinDigits, Max: Common.AffiliateCode.MaxDigits, Unit: "digits"),

        new(SettingKeys.SubscriptionDefaultMonthlyPrice, "stores", SettingType.Number, "30", Min: 0m, Max: 100_000m, Unit: "tnd"),
        new(SettingKeys.CommissionInvoiceDueDay, "stores", SettingType.WholeNumber, "10", Min: 1m, Max: 28m, Unit: "dayOfMonth"),
        new(SettingKeys.BillingGraceDays, "stores", SettingType.WholeNumber, "3", Min: 0m, Max: 60m, Unit: "days"),

        new(SettingKeys.RankingMinReviewsPrior, "search", SettingType.WholeNumber, "10", Min: 0m, Max: 1000m, Unit: "reviews"),
    ];

    private static readonly Dictionary<string, SettingDefinition> ByKey = All.ToDictionary(d => d.Key, StringComparer.Ordinal);

    public static SettingDefinition? Find(string? key) => key is not null && ByKey.TryGetValue(key, out var d) ? d : null;
}
