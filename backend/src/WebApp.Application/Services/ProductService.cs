using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Domain.Settings;

namespace WebApp.Application.Services;

/// <summary>
/// A store owner's products. Field values are checked against the category schema (its fields and its
/// ancestors'), photos must be the owner's own uploads, the affiliate % must respect the platform limits.
/// Owner actions are not admin actions: not audited.
/// </summary>
public sealed class ProductService(
    IProductRepository products,
    IStoreRepository stores,
    IProductCategoryRepository categories,
    ImageService images,
    ICatalogQueries catalog,
    IPlatformSettings settings,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<PagedResult<ProductListItem>>> ListMineAsync(Guid ownerId, ProductListQuery query, CancellationToken ct = default)
    {
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        return store is null
            ? Result.Fail<PagedResult<ProductListItem>>(StoreErrors.NotFound)
            : Result.Ok(await catalog.ListStoreProductsAsync(store.Id, query, ct).ConfigureAwait(false));
    }

    public async Task<Result<ProductDto>> GetMineAsync(Guid ownerId, Guid id, CancellationToken ct = default)
    {
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<ProductDto>(StoreErrors.NotFound);
        return await catalog.GetStoreProductAsync(store.Id, id, ct).ConfigureAwait(false) is { } product
            ? Result.Ok(product)
            : Result.Fail<ProductDto>(ProductErrors.NotFound);
    }

    public async Task<Result<PublicProductDto>> GetPublicAsync(string slug, CancellationToken ct = default) =>
        await catalog.GetPublishedProductAsync(slug, ct).ConfigureAwait(false) is { } product
            ? Result.Ok(product)
            : Result.Fail<PublicProductDto>(ProductErrors.NotFound);

    public async Task<Result<ProductDto>> CreateAsync(Guid ownerId, SaveProductRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<ProductDto>(StoreErrors.NotFound);
        if (store.IsDeleted) return Result.Fail<ProductDto>(DomainErrors.Store.Deleted);

        var prepared = await PrepareAsync(ownerId, request, currentCategoryId: null, ct).ConfigureAwait(false);
        if (!prepared.IsSuccess) return Result.Fail<ProductDto>(prepared.Error!);
        var (schema, min, max) = prepared.Value;

        var slug = await UniqueSlugAsync(request, exceptId: null, ct).ConfigureAwait(false);
        if (!slug.IsSuccess) return Result.Fail<ProductDto>(slug.Error!);

        var created = Product.Create(store.Id, request.ToDetails(slug.Value), schema, min, max);
        if (!created.IsSuccess) return Result.Fail<ProductDto>(created.Error!);

        products.Add(created.Value);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return await GetMineAsync(ownerId, created.Value.Id, ct).ConfigureAwait(false);
    }

    public async Task<Result<ProductDto>> UpdateAsync(Guid ownerId, Guid id, SaveProductRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<ProductDto>(StoreErrors.NotFound);
        if (store.IsDeleted) return Result.Fail<ProductDto>(DomainErrors.Store.Deleted);
        var product = await products.GetForStoreAsync(id, store.Id, ct).ConfigureAwait(false);
        if (product is null) return Result.Fail<ProductDto>(ProductErrors.NotFound);

        var prepared = await PrepareAsync(ownerId, request, product.CategoryId, ct).ConfigureAwait(false);
        if (!prepared.IsSuccess) return Result.Fail<ProductDto>(prepared.Error!);
        var (schema, min, max) = prepared.Value;

        var slug = string.IsNullOrWhiteSpace(request.Slug) && request.Name.Trim() == product.Name
            ? Result.Ok(product.Slug) // unchanged name and no slug given: keep the URL
            : await UniqueSlugAsync(request, product.Id, ct).ConfigureAwait(false);
        if (!slug.IsSuccess) return Result.Fail<ProductDto>(slug.Error!);

        // Keeping a % the admin's new limits no longer allow is fine; changing it must respect them.
        var keepsPercent = request.AffiliatePercent == product.AffiliatePercent && product.AffiliatePercent is not null;
        var updated = product.Update(request.ToDetails(slug.Value), schema,
            keepsPercent ? Math.Min(min, product.AffiliatePercent!.Value) : min,
            keepsPercent ? Math.Max(max, product.AffiliatePercent!.Value) : max);
        if (!updated.IsSuccess) return Result.Fail<ProductDto>(updated.Error!);

        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return await GetMineAsync(ownerId, id, ct).ConfigureAwait(false);
    }

    /// <summary>Soft delete: the product leaves the store and the shop, past orders keep pointing to it.</summary>
    public async Task<Result> DeleteAsync(Guid ownerId, Guid id, CancellationToken ct = default)
    {
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail(StoreErrors.NotFound);
        if (store.IsDeleted) return Result.Fail(DomainErrors.Store.Deleted);
        var product = await products.GetForStoreAsync(id, store.Id, ct).ConfigureAwait(false);
        if (product is null) return Result.Fail(ProductErrors.NotFound);

        product.Delete(clock.GetUtcNow().UtcDateTime);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok();
    }

    /// <summary>
    /// The category must be visible (unless the product already sits in it — a category hidden later stays
    /// on its products); its schema includes the ancestors' fields. Photos must be the owner's own.
    /// </summary>
    private async Task<Result<(IReadOnlyList<AttributeDefinition> Schema, int Min, int Max)>> PrepareAsync(
        Guid ownerId, SaveProductRequest request, Guid? currentCategoryId, CancellationToken ct)
    {
        var tree = new CategoryTree(await categories.ListAllAsync(ct).ConfigureAwait(false));
        var category = tree.Find(request.CategoryId);
        if (category is null || (request.CategoryId != currentCategoryId && !tree.IsVisible(category)))
            return Result.Fail<(IReadOnlyList<AttributeDefinition>, int, int)>(ProductErrors.CategoryUnavailable);

        foreach (var imageId in (request.ImageIds ?? []).Distinct())
        {
            var image = await images.RequireOwnAsync(imageId, ImagePurpose.ProductPhoto, ownerId, ct).ConfigureAwait(false);
            if (!image.IsSuccess) return Result.Fail<(IReadOnlyList<AttributeDefinition>, int, int)>(image.Error!);
        }

        var schema = tree.PathTo(category).SelectMany(c => c.Attributes.OrderBy(a => a.SortOrder)).ToList();
        var min = await settings.GetIntAsync(SettingKeys.AffiliatePercentMin, ct).ConfigureAwait(false);
        var max = await settings.GetIntAsync(SettingKeys.AffiliatePercentMax, ct).ConfigureAwait(false);
        return Result.Ok<(IReadOnlyList<AttributeDefinition>, int, int)>((schema, min, max));
    }

    /// <summary>A slug the owner typed must be free; one made from the name gets <c>-2</c>, <c>-3</c>… until it is.</summary>
    private async Task<Result<string>> UniqueSlugAsync(SaveProductRequest request, Guid? exceptId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            var given = request.Slug.Trim().ToLowerInvariant();
            return await products.SlugExistsAsync(given, exceptId, ct).ConfigureAwait(false)
                ? Result.Fail<string>(ProductErrors.SlugTaken)
                : Result.Ok(given);
        }

        var base_ = Slug.From(request.Name ?? "");
        if (base_.Length > Slug.MaxLength - 4) base_ = base_[..(Slug.MaxLength - 4)].TrimEnd('-');
        if (!Slug.IsValid(base_)) return Result.Ok(base_); // the domain reports the invalid name / slug

        var candidate = base_;
        for (var n = 2; await products.SlugExistsAsync(candidate, exceptId, ct).ConfigureAwait(false); n++)
            candidate = $"{base_}-{n}";
        return Result.Ok(candidate);
    }
}
