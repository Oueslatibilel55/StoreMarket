using Microsoft.Extensions.Options;
using WebApp.Application.Interfaces;

namespace WebApp.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Where uploads go: <c>Disk</c> (default, local dev) or <c>Database</c> (hosted, ADR 0006).</summary>
    public string Provider { get; set; } = "Disk";

    /// <summary>Folder holding the uploads (Disk). Relative paths are resolved against the API content root.</summary>
    public string RootPath { get; set; } = "uploads";

    public bool UseDatabase => string.Equals(Provider, "Database", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Files in a local folder, served by the API as static files under <c>/uploads</c>.
/// Every path is resolved inside the root: anything pointing outside it is refused.
/// </summary>
internal sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task SaveAsync(string relativePath, Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var path = Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // CreateNew: names are unique ids, so an existing file means a bug — never overwrite.
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, ct).ConfigureAwait(false);
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        var path = Resolve(relativePath);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException($"Storage path '{relativePath}' leaves the storage root.");
        return full;
    }
}
