using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

public interface IImageRepository
{
    Task<StoredImage?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>True when an entity (store category, later stores and products) still points to the image.</summary>
    Task<bool> IsInUseAsync(Guid id, CancellationToken ct = default);
    void Add(StoredImage image);
    void Remove(StoredImage image);
}
