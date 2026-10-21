using System.Text.RegularExpressions;

namespace WebApp.Domain.Common;

/// <summary>
/// Affiliate code like <c>DRF-45</c>: letters, a dash, digits. Shown to users as <c>#DRF-45</c>.
/// Letters exclude I and O so codes can be read out over the phone without confusion with 1 and 0.
/// The shape of <b>new</b> codes is a platform setting (letters count, minimum digits); any code
/// within the bounds below stays valid, so changing the setting never breaks codes already given.
/// </summary>
public static partial class AffiliateCode
{
    public const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    public const int MinLetters = 2;
    public const int MaxLetters = 5;
    public const int MinDigits = 2;
    public const int MaxDigits = 6;

    /// <summary>Canonical form <c>DRF-45</c> from any user input (<c>#drf-45</c>, <c>DRF45</c>, <c> drf 45 </c>), or null.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var compact = new string(input.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var match = CompactPattern().Match(compact);
        return match.Success ? $"{match.Groups[1].Value}-{match.Groups[2].Value}" : null;
    }

    public static bool IsValid(string? code) => code is not null && Normalize(code) == code;

    // Bounds: MinLetters..MaxLetters letters, MinDigits..MaxDigits digits.
    [GeneratedRegex("^([A-Z]{2,5})([0-9]{2,6})$")]
    private static partial Regex CompactPattern();
}
