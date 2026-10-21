using System.Security.Cryptography;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Settings;

namespace WebApp.Application.Services;

/// <summary>
/// Random, unique codes like <c>DRF-45</c>. The number of letters and the starting number of
/// digits come from the platform settings; the number widens when collisions become frequent.
/// Every candidate is checked against the database before it is used, and the unique index on
/// the column is the final guard against a race between two sign-ups.
/// </summary>
public sealed class AffiliateCodeGenerator(IUserRepository users, IPlatformSettings settings)
{
    private const int AttemptsPerLength = 8;

    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        var letters = await settings.GetIntAsync(SettingKeys.AffiliateCodeLetters, ct).ConfigureAwait(false);
        var minDigits = await settings.GetIntAsync(SettingKeys.AffiliateCodeMinDigits, ct).ConfigureAwait(false);

        for (var digits = minDigits; digits <= AffiliateCode.MaxDigits; digits++)
        {
            for (var attempt = 0; attempt < AttemptsPerLength; attempt++)
            {
                var code = Create(digits, letters);
                if (!await users.AffiliateCodeExistsAsync(code, ct).ConfigureAwait(false)) return code;
            }
        }
        throw new InvalidOperationException("Could not generate a unique affiliate code.");
    }

    public static string Create(int digits, int letterCount = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(letterCount, AffiliateCode.MinLetters);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(letterCount, AffiliateCode.MaxLetters);
        ArgumentOutOfRangeException.ThrowIfLessThan(digits, AffiliateCode.MinDigits);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digits, AffiliateCode.MaxDigits);

        Span<char> letters = stackalloc char[letterCount];
        for (var i = 0; i < letters.Length; i++)
            letters[i] = AffiliateCode.Letters[RandomNumberGenerator.GetInt32(AffiliateCode.Letters.Length)];

        var min = (int)Math.Pow(10, digits - 1);
        var number = RandomNumberGenerator.GetInt32(min, min * 10);
        return $"{letters}-{number}";
    }
}
