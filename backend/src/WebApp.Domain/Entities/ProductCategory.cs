using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Domain.Entities;

/// <summary>
/// Admin-managed category of products, in a tree (at most <see cref="MaxDepth"/> levels). Each category
/// defines attributes; a product in a category fills those of the category <b>and of its ancestors</b>.
/// Rules that need the whole tree (no cycle, depth, attribute keys unique along a branch) are checked by the caller.
/// </summary>
public sealed class ProductCategory : AggregateRoot
{
    public const int MaxNameLength = 60;
    public const int MaxDepth = 3;
    public const int MaxAttributes = 30;

    private readonly List<AttributeDefinition> _attributes = [];

    public Guid? ParentId { get; private set; }
    public string NameFr { get; private set; } = null!;
    public string? NameAr { get; private set; }
    public string? NameEn { get; private set; }
    public string Slug { get; private set; } = null!;
    /// <summary>Position among its sisters (same parent).</summary>
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyList<AttributeDefinition> Attributes => _attributes;

    private ProductCategory() { }

    public static Result<ProductCategory> Create(Guid? parentId, string nameFr, string? nameAr, string? nameEn, string? slug, int sortOrder)
    {
        var category = new ProductCategory { ParentId = parentId, IsActive = true, SortOrder = sortOrder };
        var applied = category.Apply(nameFr, nameAr, nameEn, slug);
        return applied.IsSuccess ? Result.Ok(category) : Result.Fail<ProductCategory>(applied.Error!);
    }

    public Result Update(string nameFr, string? nameAr, string? nameEn, string? slug)
    {
        var applied = Apply(nameFr, nameAr, nameEn, slug);
        if (applied.IsSuccess) MarkUpdated();
        return applied;
    }

    /// <summary>Moves the category under another parent (or to the top), placed last there.</summary>
    public void MoveUnder(Guid? parentId, int sortOrder)
    {
        if (ParentId == parentId) return;
        if (parentId == Id) throw new InvalidOperationException("A category can't be its own parent.");
        ParentId = parentId;
        SortOrder = sortOrder;
        MarkUpdated();
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

    public void SwapOrderWith(ProductCategory sister)
    {
        ArgumentNullException.ThrowIfNull(sister);
        (SortOrder, sister.SortOrder) = (sister.SortOrder, SortOrder);
        MarkUpdated();
        sister.MarkUpdated();
    }

    /// <summary>Adds an attribute, placed last. Its key must not exist elsewhere in the branch (checked by the caller).</summary>
    public Result<AttributeDefinition> AddAttribute(AttributeInput input)
    {
        if (_attributes.Count >= MaxAttributes) return Result.Fail<AttributeDefinition>(DomainErrors.ProductCategory.TooManyAttributes);
        var created = AttributeDefinition.Create(input, _attributes.Count == 0 ? 1 : _attributes.Max(a => a.SortOrder) + 1);
        if (!created.IsSuccess) return created;
        if (_attributes.Any(a => a.Key == created.Value.Key)) return Result.Fail<AttributeDefinition>(DomainErrors.ProductCategory.AttributeKeyTaken);

        _attributes.Add(created.Value);
        MarkUpdated();
        return created;
    }

    public Result UpdateAttribute(Guid attributeId, AttributeInput input)
    {
        var attribute = _attributes.FirstOrDefault(a => a.Id == attributeId);
        if (attribute is null) return Result.Fail(DomainErrors.ProductCategory.AttributeNotFound);
        var updated = attribute.Update(input);
        if (updated.IsSuccess) MarkUpdated();
        return updated;
    }

    public Result RemoveAttribute(Guid attributeId)
    {
        var attribute = _attributes.FirstOrDefault(a => a.Id == attributeId);
        if (attribute is null) return Result.Fail(DomainErrors.ProductCategory.AttributeNotFound);
        _attributes.Remove(attribute);
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>Moves an attribute one place up (-1) or down (+1) in the form. At the edge it is a no-op.</summary>
    public Result MoveAttribute(Guid attributeId, int step)
    {
        var ordered = _attributes.OrderBy(a => a.SortOrder).ToList();
        var index = ordered.FindIndex(a => a.Id == attributeId);
        if (index < 0) return Result.Fail(DomainErrors.ProductCategory.AttributeNotFound);
        var neighbour = index + step;
        if (neighbour < 0 || neighbour >= ordered.Count) return Result.Ok();

        ordered[index].SwapOrderWith(ordered[neighbour]);
        MarkUpdated();
        return Result.Ok();
    }

    private Result Apply(string nameFr, string? nameAr, string? nameEn, string? slug)
    {
        var names = LocalizedNames.Validate(nameFr, nameAr, nameEn, MaxNameLength,
            DomainErrors.ProductCategory.NameRequired, DomainErrors.ProductCategory.NameTooLong);
        if (!names.IsSuccess) return Result.Fail(names.Error!);

        var finalSlug = Common.Slug.FromInputOrName(slug, names.Value.Fr);
        if (!Common.Slug.IsValid(finalSlug)) return Result.Fail(DomainErrors.ProductCategory.SlugInvalid);

        (NameFr, NameAr, NameEn) = names.Value;
        Slug = finalSlug;
        return Result.Ok();
    }
}

/// <summary>What the admin types for an attribute. <c>Type</c> is only read on creation: it can't change afterwards.</summary>
public sealed record AttributeInput(
    string? Key,
    string NameFr,
    string? NameAr,
    string? NameEn,
    AttributeType Type,
    string? Unit,
    bool Required,
    bool Filterable,
    IReadOnlyList<OptionInput>? Options);

public sealed record OptionInput(string? Key, string NameFr, string? NameAr, string? NameEn);

/// <summary>A field of the product form for a category (and its sub-categories).</summary>
public sealed class AttributeDefinition : BaseEntity
{
    public const int MaxUnitLength = 16;
    public const int MaxOptions = 50;

    private readonly List<AttributeOption> _options = [];

    /// <summary>Stable identifier (<c>taille</c>, <c>volume</c>), unique along the branch; product values are stored by key.</summary>
    public string Key { get; private set; } = null!;
    public string NameFr { get; private set; } = null!;
    public string? NameAr { get; private set; }
    public string? NameEn { get; private set; }
    public AttributeType Type { get; private set; }
    /// <summary>Shown after a number (<c>ml</c>, <c>g</c>, <c>mois</c>).</summary>
    public string? Unit { get; private set; }
    public bool Required { get; private set; }
    /// <summary>Offered as a filter in search (018).</summary>
    public bool Filterable { get; private set; }
    public int SortOrder { get; private set; }
    public IReadOnlyList<AttributeOption> Options => _options;

    private AttributeDefinition() { }

    internal static Result<AttributeDefinition> Create(AttributeInput input, int sortOrder)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Enum.IsDefined(input.Type)) return Result.Fail<AttributeDefinition>(DomainErrors.ProductCategory.AttributeTypeInvalid);
        var attribute = new AttributeDefinition { Type = input.Type, SortOrder = sortOrder };
        var applied = attribute.Apply(input);
        return applied.IsSuccess ? Result.Ok(attribute) : Result.Fail<AttributeDefinition>(applied.Error!);
    }

    internal Result Update(AttributeInput input) => Apply(input);

    internal void SwapOrderWith(AttributeDefinition other) => (SortOrder, other.SortOrder) = (other.SortOrder, SortOrder);

    public bool IsChoice => Type is AttributeType.SingleChoice or AttributeType.MultiChoice;

    private Result Apply(AttributeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var names = LocalizedNames.Validate(input.NameFr, input.NameAr, input.NameEn, ProductCategory.MaxNameLength,
            DomainErrors.ProductCategory.NameRequired, DomainErrors.ProductCategory.NameTooLong);
        if (!names.IsSuccess) return Result.Fail(names.Error!);

        // The key never changes once set: product values point to it.
        var key = Key ?? Common.Slug.FromInputOrName(input.Key, names.Value.Fr);
        if (!Common.Slug.IsValid(key)) return Result.Fail(DomainErrors.ProductCategory.AttributeKeyInvalid);

        var unit = string.IsNullOrWhiteSpace(input.Unit) ? null : input.Unit.Trim();
        if (unit is not null && (Type != AttributeType.Number || unit.Length > MaxUnitLength))
            return Result.Fail(DomainErrors.ProductCategory.UnitInvalid);

        var options = new List<AttributeOption>();
        if (IsChoice)
        {
            foreach (var o in input.Options ?? [])
            {
                var option = AttributeOption.Create(o, _options.FirstOrDefault(e => e.Key == o.Key));
                if (!option.IsSuccess) return Result.Fail(option.Error!);
                if (options.Any(e => e.Key == option.Value.Key)) return Result.Fail(DomainErrors.ProductCategory.OptionKeyTaken);
                options.Add(option.Value);
            }
            if (options.Count < 2 || options.Count > MaxOptions) return Result.Fail(DomainErrors.ProductCategory.OptionsRequired);
        }
        else if (input.Options is { Count: > 0 })
        {
            return Result.Fail(DomainErrors.ProductCategory.OptionsNotAllowed);
        }

        Key = key;
        (NameFr, NameAr, NameEn) = names.Value;
        Unit = unit;
        Required = input.Required;
        Filterable = input.Filterable;
        _options.Clear();
        _options.AddRange(options);
        return Result.Ok();
    }
}

/// <summary>A value of a choice attribute (<c>M</c>, <c>EDP</c>). Stored as JSON on its attribute.</summary>
public sealed class AttributeOption
{
    public string Key { get; private set; } = null!;
    public string NameFr { get; private set; } = null!;
    public string? NameAr { get; private set; }
    public string? NameEn { get; private set; }

    private AttributeOption() { }

    internal static Result<AttributeOption> Create(OptionInput input, AttributeOption? existing)
    {
        ArgumentNullException.ThrowIfNull(input);
        var names = LocalizedNames.Validate(input.NameFr, input.NameAr, input.NameEn, ProductCategory.MaxNameLength,
            DomainErrors.ProductCategory.NameRequired, DomainErrors.ProductCategory.NameTooLong);
        if (!names.IsSuccess) return Result.Fail<AttributeOption>(names.Error!);

        var key = existing?.Key ?? Common.Slug.FromInputOrName(input.Key, names.Value.Fr);
        // Option keys may be a single character (sizes S / M / L).
        if (!Common.Slug.IsValid(key) && !(key.Length == 1 && char.IsAsciiLetterOrDigit(key[0])))
            return Result.Fail<AttributeOption>(DomainErrors.ProductCategory.OptionKeyInvalid);

        var (fr, ar, en) = names.Value;
        return Result.Ok(new AttributeOption { Key = key, NameFr = fr, NameAr = ar, NameEn = en });
    }
}

/// <summary>French name required, Arabic / English optional — trimmed and length-checked together.</summary>
public static class LocalizedNames
{
    public static Result<(string Fr, string? Ar, string? En)> Validate(string? fr, string? ar, string? en, int maxLength,
        string requiredError, string tooLongError)
    {
        var f = fr?.Trim();
        if (string.IsNullOrEmpty(f)) return Result.Fail<(string, string?, string?)>(requiredError);
        var a = string.IsNullOrWhiteSpace(ar) ? null : ar.Trim();
        var e = string.IsNullOrWhiteSpace(en) ? null : en.Trim();
        if (f.Length > maxLength || a?.Length > maxLength || e?.Length > maxLength)
            return Result.Fail<(string, string?, string?)>(tooLongError);
        return Result.Ok((f, a, e));
    }
}
