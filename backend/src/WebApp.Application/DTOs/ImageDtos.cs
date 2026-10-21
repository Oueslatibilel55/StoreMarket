using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

/// <summary>Public URL of stored files: the API serves the storage folder under this prefix.</summary>
public static class ImageUrls
{
    public const string RequestPath = "/uploads";

    public static string For(string relativePath) => $"{RequestPath}/{relativePath}";
}

public sealed record ImageDto(Guid Id, string Url, ImagePurpose Purpose, string ContentType, long SizeBytes, string? OriginalName)
{
    public static ImageDto From(StoredImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new(image.Id, ImageUrls.For(image.Path), image.Purpose, image.ContentType, image.SizeBytes, image.OriginalName);
    }
}

/// <summary>Who uploads, with the roles that decide which purposes they may use.</summary>
public sealed record Uploader(Guid Id, bool IsAdmin, bool IsStoreOwner);
