using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

/// <summary>Create or edit a product category. <c>ParentId</c> null = top level.</summary>
public sealed record SaveProductCategoryRequest(Guid? ParentId, string NameFr, string? NameAr, string? NameEn, string? Slug);

public sealed record MoveRequest(string Direction);

public sealed record AttributeOptionDto(string? Key, LocalizedName Name);

/// <summary>Create or edit an attribute. The type is ignored on edit (it can't change once products use it).</summary>
public sealed record SaveAttributeRequest(
    string? Key,
    string NameFr,
    string? NameAr,
    string? NameEn,
    AttributeType Type,
    string? Unit,
    bool Required,
    bool Filterable,
    IReadOnlyList<AttributeOptionDto>? Options)
{
    public AttributeInput ToInput() => new(Key, NameFr, NameAr, NameEn, Type, Unit, Required, Filterable,
        [.. (Options ?? []).Select(o => new OptionInput(o.Key, o.Name.Fr, o.Name.Ar, o.Name.En))]);
}

public sealed record AttributeDto(
    Guid Id, string Key, LocalizedName Name, AttributeType Type, string? Unit, bool Required, bool Filterable, int SortOrder,
    IReadOnlyList<AttributeOptionDto> Options);

/// <summary>Admin view of a category with its own attributes (inherited ones come from its ancestors in the same list).</summary>
public sealed record AdminProductCategoryDto(
    Guid Id, Guid? ParentId, string Slug, LocalizedName Name, int SortOrder, bool IsActive, IReadOnlyList<AttributeDto> Attributes);

/// <summary>Audit snapshot of a category (its attributes are audited one by one).</summary>
public sealed record ProductCategorySnapshot(Guid? ParentId, string Slug, LocalizedName Name, int SortOrder, bool IsActive);

/// <summary>Public: a node of the visible tree (a hidden category hides its whole branch).</summary>
public sealed record ProductCategoryNode(Guid Id, Guid? ParentId, string Slug, LocalizedName Name, int Depth);

/// <summary>An attribute a product of the category must (or may) fill, with the category that defines it.</summary>
public sealed record SchemaAttributeDto(AttributeDto Attribute, Guid DefinedById, string DefinedBySlug, LocalizedName DefinedByName);

/// <summary>Everything the product form (009) and the search filters (018) need for one category.</summary>
public sealed record CategorySchemaDto(ProductCategoryNode Category, IReadOnlyList<ProductCategoryNode> Path, IReadOnlyList<SchemaAttributeDto> Attributes);

public static class ProductCategoryMapping
{
    public static LocalizedName Name(ProductCategory c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new(c.NameFr, c.NameAr, c.NameEn);
    }

    public static AttributeDto ToDto(AttributeDefinition a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new(a.Id, a.Key, new LocalizedName(a.NameFr, a.NameAr, a.NameEn), a.Type, a.Unit, a.Required, a.Filterable, a.SortOrder,
            [.. a.Options.Select(o => new AttributeOptionDto(o.Key, new LocalizedName(o.NameFr, o.NameAr, o.NameEn)))]);
    }

    public static AdminProductCategoryDto ToAdminDto(ProductCategory c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new(c.Id, c.ParentId, c.Slug, Name(c), c.SortOrder, c.IsActive, [.. c.Attributes.OrderBy(a => a.SortOrder).Select(ToDto)]);
    }

    public static ProductCategorySnapshot Snapshot(ProductCategory c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new(c.ParentId, c.Slug, Name(c), c.SortOrder, c.IsActive);
    }
}

/// <summary>A loaded category tree (small: loaded whole) with the walks the rules need.</summary>
public sealed class CategoryTree(IReadOnlyList<ProductCategory> all)
{
    private readonly Dictionary<Guid, ProductCategory> _byId = all.ToDictionary(c => c.Id);

    public IReadOnlyList<ProductCategory> All => all;

    public ProductCategory? Find(Guid id) => _byId.GetValueOrDefault(id);

    /// <summary>Root first, the category itself last.</summary>
    public IReadOnlyList<ProductCategory> PathTo(ProductCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        var path = new List<ProductCategory>();
        for (var c = category; c is not null; c = c.ParentId is { } p ? Find(p) : null)
        {
            if (path.Contains(c)) break; // defensive: never loop on bad data
            path.Insert(0, c);
        }
        return path;
    }

    public int Depth(ProductCategory category) => PathTo(category).Count;

    public IEnumerable<ProductCategory> Children(Guid? parentId) =>
        all.Where(c => c.ParentId == parentId).OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt);

    public IEnumerable<ProductCategory> Descendants(ProductCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        foreach (var child in Children(category.Id))
        {
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }

    /// <summary>Levels below and including the category (a leaf = 1).</summary>
    public int Height(ProductCategory category) => 1 + Children(category.Id).Select(Height).DefaultIfEmpty(0).Max();

    /// <summary>Visible in the shop: it and all its ancestors are active.</summary>
    public bool IsVisible(ProductCategory category) => PathTo(category).All(c => c.IsActive);

    public ProductCategoryNode Node(ProductCategory c) =>
        new(c.Id, c.ParentId, c.Slug, ProductCategoryMapping.Name(c), Depth(c));
}
