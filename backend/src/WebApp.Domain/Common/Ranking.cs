namespace WebApp.Domain.Common;

/// <summary>
/// Rating-first ranking (017 / 018): a Bayesian average, so "4.8 from 200 reviews" ranks above "5.0 from 1 review".
/// <c>score = v / (v + m) × R + m / (v + m) × C</c> — <c>R</c> the item's average, <c>v</c> its number of ratings,
/// <c>C</c> the average of every visible rating of the same kind, <c>m</c> the <c>ranking.minReviewsPrior</c> setting.
/// </summary>
public static class Ranking
{
    public const int MinStars = 1;
    public const int MaxStars = 5;

    /// <summary>Null when the item has no rating: it is sorted after every rated one.</summary>
    public static decimal? Score(decimal? average, int count, decimal globalAverage, int prior)
    {
        if (average is null || count <= 0) return null;
        var m = Math.Max(0, prior);
        var score = (count * average.Value + m * globalAverage) / (count + m);
        return Math.Round(score, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>Average shown to visitors: two decimals (4.67).</summary>
    public static decimal? Average(int sum, int count) =>
        count <= 0 ? null : Math.Round((decimal)sum / count, 2, MidpointRounding.AwayFromZero);
}
