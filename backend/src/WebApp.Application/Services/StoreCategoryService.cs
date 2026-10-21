using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.Services;

/// <summary>Admin management of store categories. Every change is audited in the same transaction.</summary>
public sealed class StoreCategoryService(
    IStoreCategoryRepository categories,
    IStoreRepository stores,
    ImageService imageService,
    ICatalogQueries catalog,
    IAdminQueries queries,
    IAuditLog audit,
    IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<StoreCategoryDto>> ListActiveAsync(CancellationToken ct = default) =>
        catalog.ListActiveStoreCategoriesAsync(ct);

    public Task<IReadOnlyList<AdminStoreCategoryDto>> ListAllAsync(CancellationToken ct = default) =>
        queries.ListStoreCategoriesAsync(ct);

    public async Task<Result<AdminStoreCategoryDto>> CreateAsync(AdminActor actor, SaveStoreCategoryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);

        var imagePath = await ImagePathAsync(request.ImageId, ct).ConfigureAwait(false);
        if (!imagePath.IsSuccess) return Result.Fail<AdminStoreCategoryDto>(imagePath.Error!);

        var order = await categories.NextSortOrderAsync(ct).ConfigureAwait(false);
        var created = StoreCategory.Create(request.NameFr, request.NameAr, request.NameEn, request.Slug, request.Icon, order, request.ImageId);
        if (!created.IsSuccess) return Result.Fail<AdminStoreCategoryDto>(created.Error!);

        var category = created.Value;
        var unique = await CheckUniqueAsync(category, exceptId: null, ct).ConfigureAwait(false);
        if (!unique.IsSuccess) return Result.Fail<AdminStoreCategoryDto>(unique.Error!);

        categories.Add(category);
        var after = AdminStoreCategoryDto.From(category, imagePath.Value);
        Audit(actor, AuditActions.StoreCategoryCreate, category, before: null, after);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(after);
    }

    public Task<Result<AdminStoreCategoryDto>> UpdateAsync(AdminActor actor, Guid id, SaveStoreCategoryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ChangeAsync(actor, id, AuditActions.StoreCategoryUpdate, async category =>
        {
            var imagePath = await ImagePathAsync(request.ImageId, ct).ConfigureAwait(false);
            if (!imagePath.IsSuccess) return Result.Fail(imagePath.Error!);

            var updated = category.Update(request.NameFr, request.NameAr, request.NameEn, request.Slug, request.Icon, request.ImageId);
            return updated.IsSuccess ? await CheckUniqueAsync(category, category.Id, ct).ConfigureAwait(false) : updated;
        }, ct);
    }

    public Task<Result<AdminStoreCategoryDto>> ActivateAsync(AdminActor actor, Guid id, CancellationToken ct = default) =>
        ChangeAsync(actor, id, AuditActions.StoreCategoryActivate, c => { c.Activate(); return Task.FromResult(Result.Ok()); }, ct);

    /// <summary>Hides the category from the shop; stores already in it keep it.</summary>
    public Task<Result<AdminStoreCategoryDto>> DeactivateAsync(AdminActor actor, Guid id, CancellationToken ct = default) =>
        ChangeAsync(actor, id, AuditActions.StoreCategoryDeactivate, c => { c.Deactivate(); return Task.FromResult(Result.Ok()); }, ct);

    /// <summary>Moves the category one place up or down. At the top / bottom it is a no-op.</summary>
    public async Task<Result<AdminStoreCategoryDto>> MoveAsync(AdminActor actor, Guid id, MoveStoreCategoryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var step = request.Direction?.ToUpperInvariant() switch { "UP" => -1, "DOWN" => 1, _ => 0 };
        if (step == 0) return Result.Fail<AdminStoreCategoryDto>(StoreCategoryErrors.DirectionInvalid);

        var all = await categories.ListOrderedAsync(ct).ConfigureAwait(false);
        var index = all.ToList().FindIndex(c => c.Id == id);
        if (index < 0) return Result.Fail<AdminStoreCategoryDto>(StoreCategoryErrors.NotFound);

        var category = all[index];
        var imagePath = (await ImagePathAsync(category.ImageId, ct).ConfigureAwait(false)).Value;
        var neighbour = index + step;
        if (neighbour < 0 || neighbour >= all.Count) return Result.Ok(AdminStoreCategoryDto.From(category, imagePath));

        var before = AdminStoreCategoryDto.From(category, imagePath);
        category.SwapOrderWith(all[neighbour]);
        var after = AdminStoreCategoryDto.From(category, imagePath);
        Audit(actor, AuditActions.StoreCategoryMove, category, before, after, reason: step < 0 ? "up" : "down");
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(after);
    }

    /// <summary>
    /// Deletes a category no store uses; one in use must be hidden instead (<c>storeCategory.inUse</c>).
    /// Its image stays in storage, unreferenced.
    /// </summary>
    public async Task<Result> DeleteAsync(AdminActor actor, Guid id, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var category = await categories.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (category is null) return Result.Fail(StoreCategoryErrors.NotFound);
        if (await stores.AnyInCategoryAsync(id, ct).ConfigureAwait(false)) return Result.Fail(StoreCategoryErrors.InUse);

        var imagePath = (await ImagePathAsync(category.ImageId, ct).ConfigureAwait(false)).Value;
        Audit(actor, AuditActions.StoreCategoryDelete, category, AdminStoreCategoryDto.From(category, imagePath), after: null);
        categories.Remove(category);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok();
    }

    private async Task<Result<AdminStoreCategoryDto>> ChangeAsync(
        AdminActor actor, Guid id, string action, Func<StoreCategory, Task<Result>> change, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var category = await categories.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (category is null) return Result.Fail<AdminStoreCategoryDto>(StoreCategoryErrors.NotFound);

        var before = AdminStoreCategoryDto.From(category, (await ImagePathAsync(category.ImageId, ct).ConfigureAwait(false)).Value);
        var changed = await change(category).ConfigureAwait(false);
        if (!changed.IsSuccess) return Result.Fail<AdminStoreCategoryDto>(changed.Error!);

        var after = AdminStoreCategoryDto.From(category, (await ImagePathAsync(category.ImageId, ct).ConfigureAwait(false)).Value);
        if (before with { UpdatedAt = after.UpdatedAt } == after) return Result.Ok(after); // no-op: nothing saved or audited

        Audit(actor, action, category, before, after);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(after);
    }

    /// <summary>Path of a category image, or null without one. Fails when the id is not a category image.</summary>
    private async Task<Result<string?>> ImagePathAsync(Guid? imageId, CancellationToken ct)
    {
        if (imageId is not { } id) return Result.Ok<string?>(null);
        var image = await imageService.RequireAsync(id, ImagePurpose.CategoryImage, ct).ConfigureAwait(false);
        return image.IsSuccess ? Result.Ok<string?>(image.Value.Path) : Result.Fail<string?>(image.Error!);
    }

    private async Task<Result> CheckUniqueAsync(StoreCategory category, Guid? exceptId, CancellationToken ct)
    {
        if (await categories.SlugExistsAsync(category.Slug, exceptId, ct).ConfigureAwait(false))
            return Result.Fail(StoreCategoryErrors.SlugTaken);
        if (await categories.NameFrExistsAsync(category.NameFr, exceptId, ct).ConfigureAwait(false))
            return Result.Fail(StoreCategoryErrors.NameTaken);
        return Result.Ok();
    }

    private void Audit(AdminActor actor, string action, StoreCategory category, object? before, object? after, string? reason = null) =>
        audit.Record(actor.Email, actor.Id, action, AuditTargets.StoreCategory, category.Id, category.NameFr, before, after, reason);
}
