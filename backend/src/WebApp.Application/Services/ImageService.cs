using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Domain.Settings;

namespace WebApp.Application.Services;

/// <summary>
/// Image upload: checks who may upload what, the size (setting <c>upload.maxImageSizeMb</c>) and
/// the real format (magic bytes), writes the file, then records it. The returned id is what
/// stores, products and categories reference.
/// </summary>
public sealed class ImageService(
    IImageRepository images,
    IFileStorage storage,
    IPlatformSettings settings,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<ImageDto>> UploadAsync(Uploader uploader, ImagePurpose purpose, Stream? content, long length,
        string? fileName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(uploader);
        if (!Enum.IsDefined(purpose) || !MayUpload(uploader, purpose)) return Result.Fail<ImageDto>(ImageErrors.PurposeNotAllowed);
        if (content is null || length <= 0) return Result.Fail<ImageDto>(DomainErrors.Image.Required);

        var maxBytes = await settings.GetIntAsync(SettingKeys.UploadMaxImageSizeMb, ct).ConfigureAwait(false) * 1024L * 1024L;
        if (length > maxBytes) return Result.Fail<ImageDto>(DomainErrors.Image.TooLarge);

        // Read the whole upload (≤ the limit) so the format check and the size are those of what is saved.
        using var buffer = new MemoryStream(capacity: (int)length);
        await content.CopyToAsync(buffer, ct).ConfigureAwait(false);
        if (buffer.Length > maxBytes) return Result.Fail<ImageDto>(DomainErrors.Image.TooLarge);

        var format = ImageSignature.Detect(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, ImageSignature.HeaderLength)));
        if (format is null) return Result.Fail<ImageDto>(DomainErrors.Image.UnsupportedType);

        var image = StoredImage.Create(uploader.Id, purpose, format, buffer.Length, fileName, clock.GetUtcNow().UtcDateTime);
        buffer.Position = 0;
        await storage.SaveAsync(image.Path, buffer, ct).ConfigureAwait(false);

        images.Add(image);
        try
        {
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // No row → no reference to the file: remove it rather than leave an orphan.
            await storage.DeleteAsync(image.Path, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        return Result.Ok(ImageDto.From(image));
    }

    /// <summary>The uploader (or an admin) deletes an image nothing uses any more.</summary>
    public async Task<Result> DeleteAsync(Uploader uploader, Guid id, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(uploader);
        var image = await images.GetByIdAsync(id, ct).ConfigureAwait(false);
        // Someone else's image is reported as missing: never reveal that it exists.
        if (image is null || (image.UploadedById != uploader.Id && !uploader.IsAdmin)) return Result.Fail(ImageErrors.NotFound);
        if (await images.IsInUseAsync(id, ct).ConfigureAwait(false)) return Result.Fail(ImageErrors.InUse);

        images.Remove(image);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        await storage.DeleteAsync(image.Path, ct).ConfigureAwait(false);
        return Result.Ok();
    }

    /// <summary>Checks that an id given for <paramref name="purpose"/> is an image of that purpose.</summary>
    public async Task<Result<StoredImage>> RequireAsync(Guid id, ImagePurpose purpose, CancellationToken ct = default)
    {
        var image = await images.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (image is null) return Result.Fail<StoredImage>(ImageErrors.NotFound);
        return image.Purpose == purpose ? Result.Ok(image) : Result.Fail<StoredImage>(ImageErrors.WrongPurpose);
    }

    /// <summary>
    /// Like <see cref="RequireAsync"/>, and the image must have been uploaded by <paramref name="userId"/>:
    /// nobody attaches someone else's photo to their store or products.
    /// </summary>
    public async Task<Result<StoredImage>> RequireOwnAsync(Guid id, ImagePurpose purpose, Guid userId, CancellationToken ct = default)
    {
        var image = await RequireAsync(id, purpose, ct).ConfigureAwait(false);
        if (!image.IsSuccess) return image;
        return image.Value.UploadedById == userId ? image : Result.Fail<StoredImage>(ImageErrors.NotYours);
    }

    private static bool MayUpload(Uploader uploader, ImagePurpose purpose) => purpose switch
    {
        ImagePurpose.CategoryImage => uploader.IsAdmin,
        ImagePurpose.StoreLogo or ImagePurpose.StoreBanner or ImagePurpose.ProductPhoto => uploader.IsStoreOwner || uploader.IsAdmin,
        _ => false,
    };
}
