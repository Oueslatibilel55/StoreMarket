using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;

namespace WebApp.Application.Services;

/// <summary>
/// Admin management of the product category tree and its attributes. The tree is small, so it is
/// loaded whole and the tree rules (depth, no cycle, attribute keys unique along a branch) are checked
/// in memory. Every change is audited on the category.
/// </summary>
public sealed class ProductCategoryService(
    IProductCategoryRepository categories,
    IProductRepository products,
    ICatalogQueries catalog,
    IAdminQueries queries,
    IAuditLog audit,
    IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<ProductCategoryNode>> ListVisibleAsync(CancellationToken ct = default) =>
        catalog.ListVisibleProductCategoriesAsync(ct);

    public async Task<Result<CategorySchemaDto>> GetSchemaAsync(string slug, CancellationToken ct = default) =>
        await catalog.GetProductCategorySchemaAsync(slug, ct).ConfigureAwait(false) is { } schema
            ? Result.Ok(schema)
            : Result.Fail<CategorySchemaDto>(ProductCategoryErrors.NotFound);

    public Task<IReadOnlyList<AdminProductCategoryDto>> ListAllAsync(CancellationToken ct = default) =>
        queries.ListProductCategoriesAsync(ct);

    public async Task<Result<AdminProductCategoryDto>> CreateAsync(AdminActor actor, SaveProductCategoryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var tree = await TreeAsync(ct).ConfigureAwait(false);

        if (request.ParentId is { } parentId)
        {
            var parent = tree.Find(parentId);
            if (parent is null) return Result.Fail<AdminProductCategoryDto>(ProductCategoryErrors.ParentNotFound);
            if (tree.Depth(parent) + 1 > ProductCategory.MaxDepth) return Result.Fail<AdminProductCategoryDto>(ProductCategoryErrors.TooDeep);
        }

        var created = ProductCategory.Create(request.ParentId, request.NameFr, request.NameAr, request.NameEn, request.Slug,
            NextOrder(tree, request.ParentId));
        if (!created.IsSuccess) return Result.Fail<AdminProductCategoryDto>(created.Error!);

        var category = created.Value;
        if (tree.All.Any(c => c.Slug == category.Slug)) return Result.Fail<AdminProductCategoryDto>(ProductCategoryErrors.SlugTaken);

        categories.Add(category);
        Audit(actor, AuditActions.ProductCategoryCreate, category, before: null, ProductCategoryMapping.Snapshot(category));
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(ProductCategoryMapping.ToAdminDto(category));
    }

    /// <summary>Renames the category and/or moves it under another parent (with its whole branch).</summary>
    public Task<Result<AdminProductCategoryDto>> UpdateAsync(AdminActor actor, Guid id, SaveProductCategoryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ChangeAsync(actor, id, AuditActions.ProductCategoryUpdate, (tree, category) =>
        {
            if (request.ParentId != category.ParentId)
            {
                var moved = CheckMove(tree, category, request.ParentId);
                if (!moved.IsSuccess) return moved;
                category.MoveUnder(request.ParentId, NextOrder(tree, request.ParentId));
            }

            var updated = category.Update(request.NameFr, request.NameAr, request.NameEn, request.Slug);
            if (!updated.IsSuccess) return updated;
            return tree.All.Any(c => c.Id != category.Id && c.Slug == category.Slug)
                ? Result.Fail(ProductCategoryErrors.SlugTaken)
                : Result.Ok();
        }, ct);
    }

    public Task<Result<AdminProductCategoryDto>> ActivateAsync(AdminActor actor, Guid id, CancellationToken ct = default) =>
        ChangeAsync(actor, id, AuditActions.ProductCategoryActivate, (_, c) => { c.Activate(); return Result.Ok(); }, ct);

    /// <summary>Hides the category — and so its whole branch — from the shop.</summary>
    public Task<Result<AdminProductCategoryDto>> DeactivateAsync(AdminActor actor, Guid id, CancellationToken ct = default) =>
        ChangeAsync(actor, id, AuditActions.ProductCategoryDeactivate, (_, c) => { c.Deactivate(); return Result.Ok(); }, ct);

    /// <summary>Moves the category one place among its sisters.</summary>
    public Task<Result<AdminProductCategoryDto>> MoveAsync(AdminActor actor, Guid id, MoveRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var step = Step(request.Direction);
        if (step == 0) return Task.FromResult(Result.Fail<AdminProductCategoryDto>(ProductCategoryErrors.DirectionInvalid));

        return ChangeAsync(actor, id, AuditActions.ProductCategoryMove, (tree, category) =>
        {
            var sisters = tree.Children(category.ParentId).ToList();
            var neighbour = sisters.IndexOf(category) + step;
            if (neighbour >= 0 && neighbour < sisters.Count) category.SwapOrderWith(sisters[neighbour]);
            return Result.Ok();
        }, ct, reason: step < 0 ? "up" : "down");
    }

    /// <summary>Deletes a category without sub-categories and without products (deleted ones included: past orders point to them).</summary>
    public async Task<Result> DeleteAsync(AdminActor actor, Guid id, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var tree = await TreeAsync(ct).ConfigureAwait(false);
        var category = tree.Find(id);
        if (category is null) return Result.Fail(ProductCategoryErrors.NotFound);
        if (tree.Children(id).Any()) return Result.Fail(ProductCategoryErrors.HasChildren);
        if (await products.AnyInCategoriesAsync([id], ct).ConfigureAwait(false)) return Result.Fail(ProductCategoryErrors.HasProducts);

        Audit(actor, AuditActions.ProductCategoryDelete, category, ProductCategoryMapping.ToAdminDto(category), after: null);
        categories.Remove(category);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok();
    }

    public Task<Result<AdminProductCategoryDto>> AddAttributeAsync(AdminActor actor, Guid id, SaveAttributeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return AttributeChangeAsync(actor, id, AuditActions.AttributeCreate, (tree, category) =>
        {
            var added = category.AddAttribute(request.ToInput());
            if (!added.IsSuccess) return Result.Fail<AttributeDefinition>(added.Error!);
            return KeyFreeInBranch(tree, category, added.Value.Key, except: added.Value.Id)
                ? added
                : Result.Fail<AttributeDefinition>(ProductCategoryErrors.KeyUsedInBranch);
        }, ct);
    }

    /// <summary>Edits a field. A choice that products of the branch use can't be removed (rename it instead).</summary>
    public async Task<Result<AdminProductCategoryDto>> UpdateAttributeAsync(AdminActor actor, Guid id, Guid attributeId, SaveAttributeRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tree = await TreeAsync(ct).ConfigureAwait(false);
        if (tree.Find(id) is { } current && current.Attributes.FirstOrDefault(a => a.Id == attributeId) is { } existing)
        {
            var kept = (request.Options ?? []).Select(o => o.Key).Where(k => k is not null).ToHashSet(StringComparer.Ordinal);
            var removed = existing.Options.Select(o => o.Key).Where(k => !kept.Contains(k)).ToList();
            if (removed.Count > 0)
            {
                var used = await ProductsOfBranchAsync(tree, current, ct).ConfigureAwait(false);
                if (used.Any(p => removed.Any(option => p.UsesOption(existing.Key, option))))
                    return Result.Fail<AdminProductCategoryDto>(ProductCategoryErrors.OptionInUse);
            }
        }

        return await AttributeChangeAsync(actor, id, AuditActions.AttributeUpdate, (_, category) =>
        {
            var updated = category.UpdateAttribute(attributeId, request.ToInput());
            return updated.IsSuccess
                ? Result.Ok(category.Attributes.First(a => a.Id == attributeId))
                : Result.Fail<AttributeDefinition>(updated.Error!);
        }, ct, attributeId);
    }

    public Task<Result<AdminProductCategoryDto>> MoveAttributeAsync(AdminActor actor, Guid id, Guid attributeId, MoveRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var step = Step(request.Direction);
        if (step == 0) return Task.FromResult(Result.Fail<AdminProductCategoryDto>(ProductCategoryErrors.DirectionInvalid));

        return AttributeChangeAsync(actor, id, AuditActions.AttributeMove, (_, category) =>
        {
            var moved = category.MoveAttribute(attributeId, step);
            return moved.IsSuccess
                ? Result.Ok(category.Attributes.First(a => a.Id == attributeId))
                : Result.Fail<AttributeDefinition>(moved.Error!);
        }, ct, attributeId);
    }

    /// <summary>Removes a field — refused while a product of the branch has a value for it (rename it instead).</summary>
    public async Task<Result<AdminProductCategoryDto>> DeleteAttributeAsync(AdminActor actor, Guid id, Guid attributeId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var tree = await TreeAsync(ct).ConfigureAwait(false);
        var category = tree.Find(id);
        if (category is null) return Result.Fail<AdminProductCategoryDto>(ProductCategoryErrors.NotFound);
        var attribute = category.Attributes.FirstOrDefault(a => a.Id == attributeId);
        if (attribute is null) return Result.Fail<AdminProductCategoryDto>(DomainErrors.ProductCategory.AttributeNotFound);
        if ((await ProductsOfBranchAsync(tree, category, ct).ConfigureAwait(false)).Any(p => p.UsesAttribute(attribute.Key)))
            return Result.Fail<AdminProductCategoryDto>(ProductCategoryErrors.AttributeInUse);

        var before = ProductCategoryMapping.ToDto(attribute);
        category.RemoveAttribute(attributeId);
        Audit(actor, AuditActions.AttributeDelete, category, before, after: null, reason: attribute.Key);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(ProductCategoryMapping.ToAdminDto(category));
    }

    private async Task<Result<AdminProductCategoryDto>> ChangeAsync(
        AdminActor actor, Guid id, string action, Func<CategoryTree, ProductCategory, Result> change, CancellationToken ct, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var tree = await TreeAsync(ct).ConfigureAwait(false);
        var category = tree.Find(id);
        if (category is null) return Result.Fail<AdminProductCategoryDto>(ProductCategoryErrors.NotFound);

        var before = ProductCategoryMapping.Snapshot(category);
        var changed = change(tree, category);
        if (!changed.IsSuccess) return Result.Fail<AdminProductCategoryDto>(changed.Error!);

        var after = ProductCategoryMapping.Snapshot(category);
        if (before == after) return Result.Ok(ProductCategoryMapping.ToAdminDto(category)); // no-op: nothing saved or audited

        Audit(actor, action, category, before, after, reason);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(ProductCategoryMapping.ToAdminDto(category));
    }

    /// <summary>Attribute changes are audited with the attribute itself as before / after (reason = its key).</summary>
    private async Task<Result<AdminProductCategoryDto>> AttributeChangeAsync(
        AdminActor actor, Guid id, string action, Func<CategoryTree, ProductCategory, Result<AttributeDefinition>> change, CancellationToken ct,
        Guid? attributeId = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var tree = await TreeAsync(ct).ConfigureAwait(false);
        var category = tree.Find(id);
        if (category is null) return Result.Fail<AdminProductCategoryDto>(ProductCategoryErrors.NotFound);

        var existing = attributeId is { } aid ? category.Attributes.FirstOrDefault(a => a.Id == aid) : null;
        var before = existing is null ? null : ProductCategoryMapping.ToDto(existing);
        var changed = change(tree, category);
        if (!changed.IsSuccess) return Result.Fail<AdminProductCategoryDto>(changed.Error!);

        var after = ProductCategoryMapping.ToDto(changed.Value);
        if (before is not null && AttributesEqual(before, after)) return Result.Ok(ProductCategoryMapping.ToAdminDto(category));

        Audit(actor, action, category, before, after, reason: after.Key);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(ProductCategoryMapping.ToAdminDto(category));
    }

    private static bool AttributesEqual(AttributeDto a, AttributeDto b) =>
        a with { Options = [] } == b with { Options = [] } && a.Options.SequenceEqual(b.Options);

    /// <summary>
    /// The new parent must exist, not be the category or one of its descendants, and the moved branch
    /// must still fit in <see cref="ProductCategory.MaxDepth"/> levels with its attribute keys unique.
    /// </summary>
    private static Result CheckMove(CategoryTree tree, ProductCategory category, Guid? newParentId)
    {
        if (newParentId is not { } parentId) return Result.Ok();
        var parent = tree.Find(parentId);
        if (parent is null) return Result.Fail(ProductCategoryErrors.ParentNotFound);
        if (parent.Id == category.Id || tree.Descendants(category).Contains(parent)) return Result.Fail(ProductCategoryErrors.Cycle);
        if (tree.Depth(parent) + tree.Height(category) > ProductCategory.MaxDepth) return Result.Fail(ProductCategoryErrors.TooDeep);

        var ancestorKeys = tree.PathTo(parent).SelectMany(c => c.Attributes).Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
        var branchKeys = tree.Descendants(category).Prepend(category).SelectMany(c => c.Attributes).Select(a => a.Key);
        return branchKeys.Any(ancestorKeys.Contains) ? Result.Fail(ProductCategoryErrors.KeyUsedInBranch) : Result.Ok();
    }

    /// <summary>A product fills the attributes of its category and its ancestors: a key must be unique along every branch.</summary>
    private static bool KeyFreeInBranch(CategoryTree tree, ProductCategory category, string key, Guid except) =>
        !tree.PathTo(category).Concat(tree.Descendants(category))
            .SelectMany(c => c.Attributes)
            .Any(a => a.Id != except && a.Key == key);

    /// <summary>Products of the category and its sub-categories: they all fill the category's fields.</summary>
    private Task<IReadOnlyList<Product>> ProductsOfBranchAsync(CategoryTree tree, ProductCategory category, CancellationToken ct) =>
        products.ListInCategoriesAsync([.. tree.Descendants(category).Prepend(category).Select(c => c.Id)], ct);

    private static int NextOrder(CategoryTree tree, Guid? parentId) =>
        tree.Children(parentId).Select(c => c.SortOrder).DefaultIfEmpty(0).Max() + 1;

    private static int Step(string? direction) => direction?.ToUpperInvariant() switch { "UP" => -1, "DOWN" => 1, _ => 0 };

    private async Task<CategoryTree> TreeAsync(CancellationToken ct) => new(await categories.ListAllAsync(ct).ConfigureAwait(false));

    private void Audit(AdminActor actor, string action, ProductCategory category, object? before, object? after, string? reason = null) =>
        audit.Record(actor.Email, actor.Id, action, AuditTargets.ProductCategory, category.Id, category.NameFr, before, after, reason);
}
