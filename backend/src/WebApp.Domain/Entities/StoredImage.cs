using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Domain.Entities;

/// <summary>
/// An uploaded image. The file lives on disk (served as a static file); this row keeps its
/// relative <see cref="Path"/>, which entities reference through the image id.
/// </summary>
public sealed class StoredImage : BaseEntity
{
    public const int MaxPathLength = 200;
    public const int MaxOriginalNameLength = 200;

    public Guid UploadedById { get; private set; }
    public ImagePurpose Purpose { get; private set; }
    /// <summary>Relative to the storage root, forward slashes: <c>product-photos/2026/10/{id}.webp</c>.</summary>
    public string Path { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    /// <summary>The uploader's file name, for display only — never used to build the path.</summary>
    public string? OriginalName { get; private set; }

    private StoredImage() { }

    /// <summary>The path is built here from the id: nothing the uploader sends ends up in it.</summary>
    public static StoredImage Create(Guid uploadedById, ImagePurpose purpose, ImageSignature.Format format, long sizeBytes,
        string? originalName, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(format);
        var image = new StoredImage
        {
            UploadedById = uploadedById,
            Purpose = purpose,
            ContentType = format.ContentType,
            SizeBytes = sizeBytes,
            OriginalName = Trim(originalName),
        };
        image.Path = $"{Folder(purpose)}/{utcNow:yyyy}/{utcNow:MM}/{image.Id:N}{format.Extension}";
        return image;
    }

    public static string Folder(ImagePurpose purpose) => purpose switch
    {
        ImagePurpose.StoreLogo => "store-logos",
        ImagePurpose.StoreBanner => "store-banners",
        ImagePurpose.ProductPhoto => "product-photos",
        ImagePurpose.CategoryImage => "category-images",
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null),
    };

    private static string? Trim(string? name)
    {
        var value = System.IO.Path.GetFileName(name?.Trim());
        if (string.IsNullOrEmpty(value)) return null;
        return value.Length > MaxOriginalNameLength ? value[..MaxOriginalNameLength] : value;
    }
}
