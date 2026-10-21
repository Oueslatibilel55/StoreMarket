using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Storage;

/// <summary>
/// One uploaded file kept in PostgreSQL (ADR 0006). Used where the server disk does not survive a restart
/// (Render free tier): the bytes live next to the data that points to them.
/// </summary>
public sealed class FileBlob
{
    public string Path { get; private set; } = null!;
    public byte[] Data { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private FileBlob() { }
}

/// <summary>
/// Files in the <c>file_blobs</c> table, served by <c>GET /uploads/{path}</c> (same URLs as the disk storage).
/// Written straight away, outside the unit of work — exactly like a file on disk — so <c>ImageService</c> keeps
/// its order: store the bytes, save the image row, remove the bytes if that save fails.
/// </summary>
public sealed class DatabaseFileStorage(AppDbContext db) : IFileStorage
{
    public async Task SaveAsync(string relativePath, Stream content, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct).ConfigureAwait(false);
        var data = buffer.ToArray();
        // Plain INSERT: names are unique ids, so an existing row means a bug — never overwrite.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO file_blobs (path, data, created_at) VALUES ({relativePath}, {data}, now())", ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM file_blobs WHERE path = {relativePath}", ct).ConfigureAwait(false);
    }

    /// <summary>The bytes of a stored file, or null.</summary>
    public Task<byte[]?> ReadAsync(string relativePath, CancellationToken ct = default) =>
        db.Set<FileBlob>().AsNoTracking().Where(f => f.Path == relativePath).Select(f => f.Data).FirstOrDefaultAsync(ct);
}
