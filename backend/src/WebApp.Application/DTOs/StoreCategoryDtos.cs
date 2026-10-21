using WebApp.Domain.Entities;

namespace WebApp.Application.DTOs;

/// <summary>Create or edit a category. Without a slug, one is made from the French name.</summary>
public sealed record SaveStoreCategoryRequest(string NameFr, string? NameAr, string? NameEn, string? Slug, string? Icon, Guid? ImageId = null);

public sealed record MoveStoreCategoryRequest(string Direction);

/// <summary>Names per language; the client falls back to <c>Fr</c> when the active language has none.</summary>
public sealed record LocalizedName(string Fr, string? Ar, string? En);

/// <summary>Public: active categories only, in display order.</summary>
public sealed record StoreCategoryDto(Guid Id, string Slug, LocalizedName Name, string? Icon, string? ImageUrl);

/// <summary>Admin view (also the audit snapshot).</summary>
public sealed record AdminStoreCategoryDto(
    Guid Id, string Slug, LocalizedName Name, string? Icon, Guid? ImageId, string? ImageUrl, int SortOrder, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt)
{
    public static AdminStoreCategoryDto From(StoreCategory category, string? imagePath)
    {
        ArgumentNullException.ThrowIfNull(category);
        return new(category.Id, category.Slug, new LocalizedName(category.NameFr, category.NameAr, category.NameEn),
            category.Icon, category.ImageId, imagePath is null ? null : ImageUrls.For(imagePath), category.SortOrder, category.IsActive,
            category.CreatedAt, category.UpdatedAt);
    }
}
