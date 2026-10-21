using System.Globalization;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Domain.Entities;

/// <summary>A value typed by the owner for one category field, before validation.</summary>
public sealed record AttributeValueInput(string Key, string? Text, decimal? Number, bool? Boolean, IReadOnlyList<string>? Options);

/// <summary>One variant: an option per variant axis (<c>taille → m</c>, <c>couleur → noir</c>), its stock, optional price / SKU.</summary>
public sealed record VariantInput(Guid? Id, IReadOnlyDictionary<string, string> Options, int Stock, decimal? Price, string? Sku);

/// <summary>Everything the owner edits about a product, validated together against the category schema.</summary>
public sealed record ProductDetails(
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
    IReadOnlyList<Guid> ImageIds,
    IReadOnlyList<AttributeValueInput> Values,
    IReadOnlyList<string> VariantAxes,
    IReadOnlyList<VariantInput> Variants);

/// <summary>
/// A store's product. Field values follow the schema of its category (own + inherited fields), stored
/// by key. With variant axes (≤ 2 choice fields), each combination is a <see cref="ProductVariant"/>
/// with its own stock and optional price. Never hard-deleted: past orders and coins point to it.
/// Price and affiliate % are copied onto order lines (014), so changing them never rewrites history.
/// </summary>
public sealed class Product : AggregateRoot
{
    public const int MinNameLength = 2;
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 5000;
    public const int MaxSkuLength = 64;
    public const int MaxImages = 8;
    public const int MaxVariantAxes = 2;
    public const int MaxVariants = 60;
    public const int MaxStock = 1_000_000;
    public const decimal MaxPrice = 1_000_000m;
    public const int MaxTextValueLength = 500;

    private readonly List<Guid> _imageIds = [];
    private readonly List<AttributeValue> _values = [];
    private readonly List<string> _variantAxes = [];
    private readonly List<ProductVariant> _variants = [];

    public Guid StoreId { get; private set; }
    public string Name { get; private set; } = null!;
    /// <summary>Public URL name: <c>/products/{slug}</c>. Unique across the platform (the caller makes it unique).</summary>
    public string Slug { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    /// <summary>Old price shown crossed out; above <see cref="Price"/>.</summary>
    public decimal? CompareAtPrice { get; private set; }
    public string? Sku { get; private set; }
    /// <summary>Stock of a product without variants (variants carry their own).</summary>
    public int Stock { get; private set; }
    /// <summary>Commission for affiliates; null = the store's default %.</summary>
    public int? AffiliatePercent { get; private set; }
    public ProductStatus Status { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// Set when an admin disables the product (010): out of the shop whatever its status. The owner
    /// still sees and edits it, with the reason, but only an admin can enable it again.
    /// </summary>
    public DateTime? DisabledAt { get; private set; }
    /// <summary>Why an admin disabled or deleted the product — shown to the owner.</summary>
    public string? ModerationReason { get; private set; }

    /// <summary>Product photos in display order; the first one is the main image.</summary>
    public IReadOnlyList<Guid> ImageIds => _imageIds;
    public IReadOnlyList<AttributeValue> Values => _values;
    public IReadOnlyList<string> VariantAxes => _variantAxes;
    public IReadOnlyList<ProductVariant> Variants => _variants;

    private Product() { }

    public bool IsDeleted => DeletedAt is not null;
    public bool IsDisabled => DisabledAt is not null;
    public bool HasVariants => _variantAxes.Count > 0;
    public int TotalStock => HasVariants ? _variants.Sum(v => v.Stock) : Stock;

    /// <summary>
    /// <paramref name="schema"/> = the fields of the category and its ancestors; <paramref name="percentMin"/> /
    /// <paramref name="percentMax"/> = the platform limits. Category, images and slug uniqueness are checked by the caller.
    /// </summary>
    public static Result<Product> Create(Guid storeId, ProductDetails details, IReadOnlyList<AttributeDefinition> schema, int percentMin, int percentMax)
    {
        var product = new Product { StoreId = storeId };
        var applied = product.Apply(details, schema, percentMin, percentMax);
        return applied.IsSuccess ? Result.Ok(product) : Result.Fail<Product>(applied.Error!);
    }

    public Result Update(ProductDetails details, IReadOnlyList<AttributeDefinition> schema, int percentMin, int percentMax)
    {
        if (IsDeleted) return Result.Fail(DomainErrors.Product.Deleted);
        var applied = Apply(details, schema, percentMin, percentMax);
        if (applied.IsSuccess) MarkUpdated();
        return applied;
    }

    /// <summary>Soft delete: gone from the store and the shop, kept for past orders.</summary>
    public void Delete(DateTime utcNow)
    {
        if (IsDeleted) return;
        DeletedAt = utcNow;
        Status = ProductStatus.Archived;
        MarkUpdated();
    }

    /// <summary>An admin takes the product out of the shop (fake, absurd price…). Already disabled: nothing changes.</summary>
    public Result Disable(string? reason, DateTime utcNow)
    {
        var trimmed = ModerationReasonOf(reason);
        if (trimmed is null) return Result.Fail(DomainErrors.Moderation.ReasonRequired);
        if (IsDeleted) return Result.Fail(DomainErrors.Product.Deleted);
        if (IsDisabled) return Result.Ok();

        DisabledAt = utcNow;
        ModerationReason = trimmed;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>The admin lifts a disable; the product is back in the shop if its owner published it.</summary>
    public Result Enable()
    {
        if (IsDeleted) return Result.Fail(DomainErrors.Product.Deleted);
        if (!IsDisabled) return Result.Ok();

        DisabledAt = null;
        ModerationReason = null;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>Soft delete by an admin, with the reason (final).</summary>
    public Result DeleteByAdmin(string? reason, DateTime utcNow)
    {
        var trimmed = ModerationReasonOf(reason);
        if (trimmed is null) return Result.Fail(DomainErrors.Moderation.ReasonRequired);
        if (IsDeleted) return Result.Ok();

        ModerationReason = trimmed;
        Delete(utcNow);
        return Result.Ok();
    }

    private static string? ModerationReasonOf(string? reason)
    {
        var trimmed = reason?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > Store.MaxModerationReasonLength ? null : trimmed;
    }

    /// <summary>True when this product (or one of its variants) stores a value for <paramref name="key"/>.</summary>
    public bool UsesAttribute(string key) => _values.Any(v => v.Key == key) || _variantAxes.Contains(key);

    /// <summary>True when the choice <paramref name="optionKey"/> of field <paramref name="key"/> is used by the product or a variant.</summary>
    public bool UsesOption(string key, string optionKey) =>
        _values.Any(v => v.Key == key && v.OptionKeys.Contains(optionKey))
        || _variants.Any(variant => variant.Selection.Any(s => s.Key == key && s.Value == optionKey));

    private Result Apply(ProductDetails d, IReadOnlyList<AttributeDefinition> schema, int percentMin, int percentMax)
    {
        ArgumentNullException.ThrowIfNull(d);
        ArgumentNullException.ThrowIfNull(schema);

        var name = d.Name?.Trim();
        if (string.IsNullOrEmpty(name)) return Result.Fail(DomainErrors.Product.NameRequired);
        if (name.Length is < MinNameLength or > MaxNameLength) return Result.Fail(DomainErrors.Product.NameLength);

        var slug = Common.Slug.FromInputOrName(d.Slug, name);
        if (!Common.Slug.IsValid(slug)) return Result.Fail(DomainErrors.Product.SlugInvalid);

        var description = Optional(d.Description);
        if (description?.Length > MaxDescriptionLength) return Result.Fail(DomainErrors.Product.DescriptionTooLong);

        if (!ValidPrice(d.Price)) return Result.Fail(DomainErrors.Product.PriceInvalid);
        if (d.CompareAtPrice is { } compare && (!ValidPrice(compare) || compare <= d.Price))
            return Result.Fail(DomainErrors.Product.CompareAtPriceInvalid);

        var sku = Optional(d.Sku);
        if (sku?.Length > MaxSkuLength) return Result.Fail(DomainErrors.Product.SkuTooLong);
        if (d.Stock is < 0 or > MaxStock) return Result.Fail(DomainErrors.Product.StockInvalid);

        if (d.AffiliatePercent is { } percent && (percent < percentMin || percent > percentMax))
            return Result.Fail(DomainErrors.Product.PercentOutOfRange);

        if (!Enum.IsDefined(d.Status)) return Result.Fail(DomainErrors.Product.StatusInvalid);

        var images = (d.ImageIds ?? []).Distinct().ToList();
        if (images.Count > MaxImages) return Result.Fail(DomainErrors.Product.TooManyImages);

        var byKey = schema.ToDictionary(a => a.Key, StringComparer.Ordinal);

        // Variant axes: up to 2 distinct choice fields of the schema.
        var axes = (d.VariantAxes ?? []).Distinct(StringComparer.Ordinal).ToList();
        if (axes.Count > MaxVariantAxes || axes.Any(a => !byKey.TryGetValue(a, out var def) || !def.IsChoice))
            return Result.Fail(DomainErrors.Product.VariantAxesInvalid);

        var values = new List<AttributeValue>();
        foreach (var input in d.Values ?? [])
        {
            if (!byKey.TryGetValue(input.Key ?? "", out var definition)) return Result.Fail(DomainErrors.Product.UnknownAttribute);
            if (axes.Contains(definition.Key)) continue; // given by each variant instead
            if (values.Any(v => v.Key == definition.Key)) return Result.Fail(DomainErrors.Product.UnknownAttribute);
            var value = AttributeValue.From(definition, input);
            if (!value.IsSuccess) return Result.Fail(value.Error!);
            if (value.Value is { } v) values.Add(v);
        }

        var variants = new List<(VariantInput Input, List<AttributeValue> Selection)>();
        if (axes.Count > 0)
        {
            var inputs = d.Variants ?? [];
            if (inputs.Count == 0 || inputs.Count > MaxVariants) return Result.Fail(DomainErrors.Product.VariantsRequired);
            var combinations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var v in inputs)
            {
                if (v.Options is null || v.Options.Count != axes.Count) return Result.Fail(DomainErrors.Product.VariantInvalid);
                var selection = new List<AttributeValue>();
                foreach (var axis in axes)
                {
                    if (!v.Options.TryGetValue(axis, out var option) || !byKey[axis].Options.Any(o => o.Key == option))
                        return Result.Fail(DomainErrors.Product.VariantInvalid);
                    selection.Add(AttributeValue.Of(axis, option));
                }
                if (!combinations.Add(string.Join('|', selection.Select(s => $"{s.Key}={s.Value}"))))
                    return Result.Fail(DomainErrors.Product.VariantDuplicate);
                if (v.Stock is < 0 or > MaxStock) return Result.Fail(DomainErrors.Product.StockInvalid);
                if (v.Price is { } p && !ValidPrice(p)) return Result.Fail(DomainErrors.Product.PriceInvalid);
                if (Optional(v.Sku)?.Length > MaxSkuLength) return Result.Fail(DomainErrors.Product.SkuTooLong);
                variants.Add((v, selection));
            }
        }

        // Only a published product must be complete; a draft can be saved half-filled.
        if (d.Status == ProductStatus.Published)
        {
            var missing = schema.Where(a => a.Required && !axes.Contains(a.Key)).Any(a => values.All(v => v.Key != a.Key));
            if (missing) return Result.Fail(DomainErrors.Product.RequiredAttributeMissing);
        }

        Name = name;
        Slug = slug;
        CategoryId = d.CategoryId;
        Description = description;
        Price = d.Price;
        CompareAtPrice = d.CompareAtPrice;
        Sku = sku;
        Stock = axes.Count > 0 ? 0 : d.Stock;
        AffiliatePercent = d.AffiliatePercent;
        Status = d.Status;
        _imageIds.Clear();
        _imageIds.AddRange(images);
        _values.Clear();
        _values.AddRange(values);
        _variantAxes.Clear();
        _variantAxes.AddRange(axes);
        SyncVariants(variants);
        return Result.Ok();
    }

    /// <summary>Keeps the id of variants that still exist (orders will point to them), adds new ones, drops the others.</summary>
    private void SyncVariants(List<(VariantInput Input, List<AttributeValue> Selection)> wanted)
    {
        var kept = new List<ProductVariant>();
        foreach (var (input, selection) in wanted)
        {
            var existing = _variants.FirstOrDefault(v => v.Id == input.Id)
                ?? _variants.FirstOrDefault(v => v.SameSelection(selection));
            if (existing is not null)
            {
                existing.Set(selection, input.Stock, input.Price, Optional(input.Sku));
                kept.Add(existing);
            }
            else
            {
                kept.Add(ProductVariant.Create(selection, input.Stock, input.Price, Optional(input.Sku)));
            }
        }
        _variants.Clear();
        _variants.AddRange(kept);
    }

    private static bool ValidPrice(decimal price) => price > 0 && price <= MaxPrice && decimal.Round(price, 3) == price;

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// A stored field value (JSON on the product): the field key and a canonical string —
/// text as typed, numbers invariant (<c>50</c>, <c>2.5</c>), booleans <c>true</c> / <c>false</c>,
/// choices as option keys (several joined by commas: option keys never contain one).
/// </summary>
public sealed class AttributeValue
{
    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;

    private AttributeValue() { }

    public IReadOnlyList<string> OptionKeys => Value.Split(',', StringSplitOptions.RemoveEmptyEntries);

    internal static AttributeValue Of(string key, string value) => new() { Key = key, Value = value };

    /// <summary>Checks a value against its field. An empty value is "not filled" (null), not an error.</summary>
    internal static Result<AttributeValue?> From(AttributeDefinition definition, AttributeValueInput input)
    {
        switch (definition.Type)
        {
            case AttributeType.Text:
                var text = input.Text?.Trim();
                if (string.IsNullOrEmpty(text)) return Result.Ok<AttributeValue?>(null);
                return text.Length > Product.MaxTextValueLength
                    ? Result.Fail<AttributeValue?>(DomainErrors.Product.AttributeValueInvalid)
                    : Result.Ok<AttributeValue?>(Of(definition.Key, text));

            case AttributeType.Number:
                if (input.Number is not { } number) return Result.Ok<AttributeValue?>(null);
                return number < 0 || number > 1_000_000_000m || decimal.Round(number, 3) != number
                    ? Result.Fail<AttributeValue?>(DomainErrors.Product.AttributeValueInvalid)
                    : Result.Ok<AttributeValue?>(Of(definition.Key, number.ToString("0.###", CultureInfo.InvariantCulture)));

            case AttributeType.Boolean:
                return input.Boolean is { } flag
                    ? Result.Ok<AttributeValue?>(Of(definition.Key, flag ? "true" : "false"))
                    : Result.Ok<AttributeValue?>(null);

            case AttributeType.SingleChoice or AttributeType.MultiChoice:
                var chosen = (input.Options ?? []).Distinct(StringComparer.Ordinal).ToList();
                if (chosen.Count == 0) return Result.Ok<AttributeValue?>(null);
                if (definition.Type == AttributeType.SingleChoice && chosen.Count > 1)
                    return Result.Fail<AttributeValue?>(DomainErrors.Product.AttributeValueInvalid);
                if (chosen.Any(k => definition.Options.All(o => o.Key != k)))
                    return Result.Fail<AttributeValue?>(DomainErrors.Product.AttributeValueInvalid);
                // Canonical order = the field's order of choices.
                var ordered = definition.Options.Where(o => chosen.Contains(o.Key)).Select(o => o.Key);
                return Result.Ok<AttributeValue?>(Of(definition.Key, string.Join(',', ordered)));

            default:
                return Result.Fail<AttributeValue?>(DomainErrors.Product.AttributeValueInvalid);
        }
    }
}

/// <summary>One combination of the variant axes (<c>M · Noir</c>) with its own stock and optional price.</summary>
public sealed class ProductVariant : BaseEntity
{
    private readonly List<AttributeValue> _selection = [];

    /// <summary>One option per variant axis, in the product's axis order.</summary>
    public IReadOnlyList<AttributeValue> Selection => _selection;
    public int Stock { get; private set; }
    /// <summary>Overrides the product price for this combination.</summary>
    public decimal? Price { get; private set; }
    public string? Sku { get; private set; }

    private ProductVariant() { }

    internal static ProductVariant Create(List<AttributeValue> selection, int stock, decimal? price, string? sku)
    {
        var variant = new ProductVariant();
        variant.Set(selection, stock, price, sku);
        return variant;
    }

    internal void Set(List<AttributeValue> selection, int stock, decimal? price, string? sku)
    {
        _selection.Clear();
        _selection.AddRange(selection);
        Stock = stock;
        Price = price;
        Sku = sku;
        MarkUpdated();
    }

    internal bool SameSelection(List<AttributeValue> selection) =>
        _selection.Count == selection.Count && _selection.All(s => selection.Any(o => o.Key == s.Key && o.Value == s.Value));
}
