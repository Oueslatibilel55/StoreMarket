using System.Globalization;
using System.Text;

namespace WebApp.Application.Common;

/// <summary>
/// Turns what a visitor typed into a PostgreSQL <c>tsquery</c> for the shop search (018): every word must match, each as
/// a prefix (<c>parf</c> finds <c>parfum</c>). Only letters and digits are kept, so the result is always a valid
/// query and never carries tsquery operators typed by the user. Accents are removed by the database (<c>unaccent</c>).
/// </summary>
public static class SearchText
{
    public const int MaxWords = 8;
    public const int MaxWordLength = 40;

    /// <summary><c>"Crème  hydratante!"</c> → <c>"crème:* &amp; hydratante:*"</c>; null when nothing searchable was typed.</summary>
    public static string? ToTsQuery(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var c in input.Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                word.Append(char.ToLowerInvariant(c));
                continue;
            }
            Flush();
        }
        Flush();

        // French elisions (l', d', qu'…) leave one-letter words that would only add noise — unless that is all there is.
        var kept = words.Where(w => w.Length > 1 || char.IsDigit(w[0])).ToList();
        if (kept.Count == 0) kept = words;
        return kept.Count == 0 ? null : string.Join(" & ", kept.Distinct(StringComparer.Ordinal).Select(w => w + ":*"));

        void Flush()
        {
            // A lone combining mark (a stray Arabic haraka) is not a word.
            if (words.Count < MaxWords && word.ToString().Any(char.IsLetterOrDigit))
                words.Add(word.Length > MaxWordLength ? word.ToString(0, MaxWordLength) : word.ToString());
            word.Clear();
        }
    }
}
