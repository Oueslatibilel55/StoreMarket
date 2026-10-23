using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Repositories;

internal sealed class ImageRepository(AppDbContext db) : IImageRepository
{
    public Task<StoredImage?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Images.FirstOrDefaultAsync(i => i.Id == id, ct);

    // Add a check here for every new entity that references images.
    public async Task<bool> IsInUseAsync(Guid id, CancellationToken ct = default) =>
        await db.StoreCategories.AnyAsync(c => c.ImageId == id, ct).ConfigureAwait(false)
        || await db.Stores.AnyAsync(s => s.LogoImageId == id || s.BannerImageId == id, ct).ConfigureAwait(false)
        || await db.Products.AnyAsync(p => p.ImageIds.Contains(id), ct).ConfigureAwait(false)
        || await db.StorePayments.AnyAsync(p => p.ReceiptImageId == id, ct).ConfigureAwait(false);

    public void Add(StoredImage image) => db.Images.Add(image);

    public void Remove(StoredImage image) => db.Images.Remove(image);
}
