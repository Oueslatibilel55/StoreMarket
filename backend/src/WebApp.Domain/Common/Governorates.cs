namespace WebApp.Domain.Common;

/// <summary>
/// The 24 Tunisian governorates, as stable codes (the client translates <c>cities.&lt;code&gt;</c>).
/// Used for a store's city and the zones it delivers to.
/// </summary>
public static class Governorates
{
    public static readonly IReadOnlyList<string> All =
    [
        "tunis", "ariana", "ben-arous", "manouba", "nabeul", "zaghouan", "bizerte", "beja",
        "jendouba", "kef", "siliana", "sousse", "monastir", "mahdia", "sfax", "kairouan",
        "kasserine", "sidi-bouzid", "gabes", "medenine", "tataouine", "gafsa", "tozeur", "kebili",
    ];

    private static readonly HashSet<string> Codes = new(All, StringComparer.Ordinal);

    public static bool IsValid(string? code) => code is not null && Codes.Contains(code);
}
