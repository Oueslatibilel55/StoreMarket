namespace WebApp.Application.Common;

/// <summary>
/// One shop filter on a category field (018), from <c>?attr=couleur:noir,rouge</c>: the product must hold one of the
/// choices (its own value or a variant). Field and choice keys are slugs, so <c>:</c> and <c>,</c> never occur in them.
/// Whether the field exists and is filterable is checked against the category schema by the query.
/// </summary>
public sealed record AttributeFilter(string Key, IReadOnlyList<string> Values)
{
    public const int MaxFilters = 10;
    public const int MaxValues = 20;

    /// <summary>Malformed entries are skipped (a stale link still opens); the same field twice keeps the first.</summary>
    public static IReadOnlyList<AttributeFilter> Parse(IEnumerable<string>? raw)
    {
        var filters = new List<AttributeFilter>();
        foreach (var entry in raw ?? [])
        {
            if (filters.Count == MaxFilters) break;
            var colon = entry?.IndexOf(':', StringComparison.Ordinal) ?? -1;
            if (colon <= 0) continue;
            var key = entry![..colon].Trim();
            var values = entry[(colon + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal).Take(MaxValues).ToList();
            if (values.Count == 0 || filters.Any(f => f.Key == key)) continue;
            filters.Add(new AttributeFilter(key, values));
        }
        return filters;
    }
}
