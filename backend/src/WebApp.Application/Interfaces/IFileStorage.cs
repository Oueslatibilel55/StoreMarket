namespace WebApp.Application.Interfaces;

/// <summary>
/// Where uploaded files live. Paths are relative (<c>product-photos/2026/10/{id}.webp</c>) and
/// built by the domain — never from user input. Today: a folder served as static files; a cloud
/// bucket later only needs another implementation.
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(string relativePath, Stream content, CancellationToken ct = default);
    Task DeleteAsync(string relativePath, CancellationToken ct = default);
}
