using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

/// <summary>A field value as the owner sends it: fill the property matching the field type.</summary>
public sealed record AttributeValueRequest(string Key, string? Text, decimal? Number, bool? Boolean, IReadOnlyList<string>? Options);

/// <summary>A variant: an option key per axis (<c>{ "taille": "m", "couleur": "noir" }</c>). <c>Id</c> keeps an existing variant.</summary>
public sealed record VariantRequest(Guid? Id, IReadOnlyDictionary<string, string> Options, int Stock, decimal? Price, string? Sku);

/// <summary>Create or edit a product of my store. Without a slug, one is made from the name (made unique).</summary>
public sealed record SaveProductRequest(
    string Name,
    string? Slug,
    Guid CategoryId,
    string? Description,
    decimal Price,
    decimal? CompareAtPrice,
    string? Sku,
    int Stock,
    int? AffiliatePercent,
    ProductStatus Status,
    IReadOnlyList<Guid>? ImageIds,
    IReadOnlyList<AttributeValueRequest>? Values,
    IReadOnlyList<string>? VariantAxes,
    IReadOnlyList<VariantRequest>? Variants)
{
    public ProductDetails ToDetails(string? slug) => new(
        Name, slug, CategoryId, Description, Price, CompareAtPrice, Sku, Stock, AffiliatePercent, Status,
        ImageIds ?? [],
        [.. (Values ?? []).Select(v => new AttributeValueInput(v.Key, v.Text, v.Number, v.Boolean, v.Options))],
        VariantAxes ?? [],
        [.. (Variants ?? []).Select(v => new VariantInput(v.Id, v.Options ?? new Dictionary<string, string>(), v.Stock, v.Price, v.Sku))]);
}

public sealed record ProductListQuery(string? Search, ProductStatus? Status, int? Page, int? PageSize);

public sealed record ProductImageDto(Guid Id, string Url);

public sealed record ProductValueDto(string Key, string Value);

public sealed record ProductVariantDto(Guid Id, IReadOnlyDictionary<string, string> Options, int Stock, decimal? Price, string? Sku);

/// <summary>The owner's view of a product (raw values, as edited).</summary>
public sealed record ProductDto(
    Guid Id,
    string Slug,
    string Name,
    ProductCategoryRef Category,
    string? Description,
    decimal Price,
    decimal? CompareAtPrice,
    string? Sku,
    int Stock,
    int TotalStock,
    int? AffiliatePercent,
    int EffectiveAffiliatePercent,
    ProductStatus Status,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductValueDto> Values,
    IReadOnlyList<string> VariantAxes,
    IReadOnlyList<ProductVariantDto> Variants,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDisabled,
    string? ModerationReason);

public sealed record ProductCategoryRef(Guid Id, string Slug, LocalizedName Name);

/// <summary>A row of the owner's product list.</summary>
public sealed record ProductListItem(
    Guid Id, string Slug, string Name, string? MainImageUrl, decimal Price, decimal MinPrice, int TotalStock, int VariantCount,
    ProductStatus Status, LocalizedName CategoryName, DateTime UpdatedAt, bool IsDisabled, string? ModerationReason);

/// <summary>A field of a public product, with its labels resolved (choice names, unit).</summary>
public sealed record PublicAttributeDto(string Key, LocalizedName Name, AttributeType Type, string? Unit, string? Text, decimal? Number,
    bool? Boolean, IReadOnlyList<AttributeOptionDto> Options);

public sealed record PublicVariantDto(Guid Id, IReadOnlyDictionary<string, string> Options, decimal Price, bool InStock);

public sealed record PublicVariantAxisDto(string Key, LocalizedName Name, IReadOnlyList<AttributeOptionDto> Options);

public sealed record ProductStoreRef(Guid Id, string Name, string Slug, string? LogoUrl, bool IsOpen);

/// <summary>What the shop shows of a published product (011).</summary>
public sealed record PublicProductDto(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    decimal Price,
    decimal MinPrice,
    decimal? CompareAtPrice,
    int AffiliatePercent,
    bool InStock,
    IReadOnlyList<string> ImageUrls,
    IReadOnlyList<ProductCategoryNode> CategoryPath,
    IReadOnlyList<PublicAttributeDto> Attributes,
    IReadOnlyList<PublicVariantAxisDto> VariantAxes,
    IReadOnlyList<PublicVariantDto> Variants,
    ProductStoreRef Store);

public static class ProductMapping
{
    public static IReadOnlyDictionary<string, string> Options(ProductVariant v)
    {
        ArgumentNullException.ThrowIfNull(v);
        return v.Selection.ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);
    }

    /// <summary>Lowest price a client can pay: the product price, or the cheapest variant.</summary>
    public static decimal MinPrice(Product p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return p.HasVariants && p.Variants.Count > 0 ? p.Variants.Min(v => v.Price ?? p.Price) : p.Price;
    }
}
