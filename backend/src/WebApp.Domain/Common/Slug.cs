using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WebApp.Domain.Common;

/// <summary>URL names (<c>parfums</c>, <c>pret-a-porter</c>): lower-case Latin letters, digits, single dashes.</summary>
public static partial class Slug
{
    public const int MinLength = 2;
    public const int MaxLength = 60;

    public static bool IsValid(string? slug) =>
        slug is { Length: >= MinLength and <= MaxLength } && Pattern().IsMatch(slug);

    /// <summary><c>"Parfums & Beauté"</c> → <c>"parfums-beaute"</c>. Empty when nothing Latin is left.</summary>
    public static string From(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(c);
            builder.Append(lower is >= 'a' and <= 'z' or >= '0' and <= '9' ? lower : '-');
        }
        var slug = DashRun().Replace(builder.ToString(), "-").Trim('-');
        return slug.Length > MaxLength ? slug[..MaxLength].TrimEnd('-') : slug;
    }

    /// <summary>Slug given by the user (trimmed, lower-cased), or made from <paramref name="name"/> when empty.</summary>
    public static string FromInputOrName(string? input, string name) =>
        string.IsNullOrWhiteSpace(input) ? From(name) : input.Trim().ToLowerInvariant();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Pattern();

    [GeneratedRegex("-{2,}")]
    private static partial Regex DashRun();
}
