using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebApp.Application.Interfaces;
using WebApp.Domain.Settings;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Settings;

/// <summary>
/// Catalog defaults overlaid with the rows the admin changed, cached for the whole app.
/// Settings change rarely, so the cache lives until a write invalidates it (or 10 minutes,
/// in case another instance of the API made the change).
/// </summary>
internal sealed class PlatformSettingsStore(AppDbContext db, IMemoryCache cache) : IPlatformSettings
{
    private const string CacheKey = "platform-settings";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, string>? cached) && cached is not null) return cached;

        var stored = await db.PlatformSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, StringComparer.Ordinal, ct).ConfigureAwait(false);

        // A row whose key left the catalog is ignored; a key without a row uses its default.
        IReadOnlyDictionary<string, string> values = PlatformSettingCatalog.All.ToDictionary(
            d => d.Key, d => stored.GetValueOrDefault(d.Key) ?? d.DefaultValue, StringComparer.Ordinal);

        cache.Set(CacheKey, values, Lifetime);
        return values;
    }

    public async Task<int> GetIntAsync(string key, CancellationToken ct = default) =>
        int.Parse(await GetAsync(key, ct).ConfigureAwait(false), CultureInfo.InvariantCulture);

    public async Task<decimal> GetDecimalAsync(string key, CancellationToken ct = default) =>
        decimal.Parse(await GetAsync(key, ct).ConfigureAwait(false), CultureInfo.InvariantCulture);

    public void Invalidate() => cache.Remove(CacheKey);

    private async Task<string> GetAsync(string key, CancellationToken ct) =>
        (await GetAllAsync(ct).ConfigureAwait(false)).TryGetValue(key, out var value)
            ? value
            : throw new ArgumentException($"Unknown platform setting '{key}'.", nameof(key));
}
