namespace WebApp.Application.Interfaces;

/// <summary>
/// Current platform settings (stored value, or the catalog default), cached in memory.
/// Writers call <see cref="Invalidate"/> after committing a change.
/// </summary>
public interface IPlatformSettings
{
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default);
    Task<int> GetIntAsync(string key, CancellationToken ct = default);
    Task<decimal> GetDecimalAsync(string key, CancellationToken ct = default);
    void Invalidate();
}
