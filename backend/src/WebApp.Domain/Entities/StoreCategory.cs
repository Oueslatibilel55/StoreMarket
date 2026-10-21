using WebApp.Domain.Common;

namespace WebApp.Domain.Entities;

/// <summary>
/// Admin-managed type of store (cosmetics, clothes, perfumes…). Named in French (required)
/// with optional Arabic / English names — the client falls back to French. Inactive categories
/// are hidden from the shop but kept for the stores that use them.
/// </summary>
public sealed class StoreCategory : BaseEntity
{
    public const int MaxNameLength = 60;
    public const int MaxSlugLength = Common.Slug.MaxLength;
    public const int MaxIconLength = 16;

    public string NameFr { get; private set; } = null!;
    public string? NameAr { get; private set; }
    public string? NameEn { get; private set; }
    /// <summary>URL name (<c>parfums</c>): lower-case letters, digits and dashes. Unique.</summary>
    public string Slug { get; private set; } = null!;
    /// <summary>Emoji shown with the name when the category has no image.</summary>
    public string? Icon { get; private set; }
    /// <summary>Optional picture (<see cref="StoredImage"/>, purpose CategoryImage); shown instead of the icon.</summary>
    public Guid? ImageId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    private StoreCategory() { }

    /// <summary>
    /// New active category placed at <paramref name="sortOrder"/> (the caller puts it last).
    /// Without a slug, one is made from the French name. Uniqueness is checked by the caller.
    /// </summary>
    public static Result<StoreCategory> Create(string nameFr, string? nameAr, string? nameEn, string? slug, string? icon, int sortOrder,
        Guid? imageId = null)
    {
        var category = new StoreCategory { IsActive = true, SortOrder = sortOrder };
        var applied = category.Apply(nameFr, nameAr, nameEn, slug, icon, imageId);
        return applied.IsSuccess ? Result.Ok(category) : Result.Fail<StoreCategory>(applied.Error!);
    }

    public Result Update(string nameFr, string? nameAr, string? nameEn, string? slug, string? icon, Guid? imageId = null)
    {
        var applied = Apply(nameFr, nameAr, nameEn, slug, icon, imageId);
        if (applied.IsSuccess) MarkUpdated();
        return applied;
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        MarkUpdated();
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        MarkUpdated();
    }

    /// <summary>Swaps the display position with a neighbour (move up / down in the admin list).</summary>
    public void SwapOrderWith(StoreCategory other)
    {
        ArgumentNullException.ThrowIfNull(other);
        (SortOrder, other.SortOrder) = (other.SortOrder, SortOrder);
        MarkUpdated();
        other.MarkUpdated();
    }

    private Result Apply(string nameFr, string? nameAr, string? nameEn, string? slug, string? icon, Guid? imageId)
    {
        var fr = nameFr?.Trim();
        if (string.IsNullOrEmpty(fr)) return Result.Fail(DomainErrors.StoreCategory.NameRequired);
        var ar = Optional(nameAr);
        var en = Optional(nameEn);
        if (fr.Length > MaxNameLength || ar?.Length > MaxNameLength || en?.Length > MaxNameLength)
            return Result.Fail(DomainErrors.StoreCategory.NameTooLong);

        var finalSlug = Common.Slug.FromInputOrName(slug, fr);
        if (!Common.Slug.IsValid(finalSlug)) return Result.Fail(DomainErrors.StoreCategory.SlugInvalid);

        var finalIcon = Optional(icon);
        if (finalIcon?.Length > MaxIconLength) return Result.Fail(DomainErrors.StoreCategory.IconTooLong);

        NameFr = fr;
        NameAr = ar;
        NameEn = en;
        Slug = finalSlug;
        Icon = finalIcon;
        ImageId = imageId;
        return Result.Ok();
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}
