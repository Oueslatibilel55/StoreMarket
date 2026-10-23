using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Domain.Settings;

namespace WebApp.Application.Services;

/// <summary>
/// A store owner creates and configures their store (one per owner in v1, live at once).
/// Owner actions are not admin actions, so they are not written to the audit log.
/// </summary>
public sealed class StoreService(
    IStoreRepository stores,
    IStoreCategoryRepository categories,
    ImageService images,
    ICatalogQueries catalog,
    IPlatformSettings settings,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<MyStoreDto>> GetMineAsync(Guid ownerId, CancellationToken ct = default) =>
        await catalog.GetStoreOfOwnerAsync(ownerId, ct).ConfigureAwait(false) is { } store
            ? Result.Ok(store)
            : Result.Fail<MyStoreDto>(StoreErrors.NotFound);

    public async Task<Result<PublicStoreDto>> GetPublicAsync(string slug, CancellationToken ct = default) =>
        await catalog.GetStoreBySlugAsync(slug, ct).ConfigureAwait(false) is { } store
            ? Result.Ok(store)
            : Result.Fail<PublicStoreDto>(StoreErrors.NotFound);

    public async Task<Result<MyStoreDto>> CreateAsync(Guid ownerId, SaveStoreRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false) is not null)
            return Result.Fail<MyStoreDto>(StoreErrors.AlreadyExists);

        var checks = await CheckReferencesAsync(ownerId, request, currentCategoryId: null, ct).ConfigureAwait(false);
        if (!checks.IsSuccess) return Result.Fail<MyStoreDto>(checks.Error!);

        var (min, max) = await PercentRangeAsync(ct).ConfigureAwait(false);
        var price = await settings.GetDecimalAsync(SettingKeys.SubscriptionDefaultMonthlyPrice, ct).ConfigureAwait(false);
        // A new store starts with the trial days paid (025).
        var trialDays = await settings.GetIntAsync(SettingKeys.SubscriptionTrialDays, ct).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var created = Store.Create(ownerId, request.ToDetails(), min, max, price, today.AddDays(trialDays));
        if (!created.IsSuccess) return Result.Fail<MyStoreDto>(created.Error!);

        if (await stores.SlugExistsAsync(created.Value.Slug, exceptId: null, ct).ConfigureAwait(false))
            return Result.Fail<MyStoreDto>(StoreErrors.SlugTaken);

        stores.Add(created.Value);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return await GetMineAsync(ownerId, ct).ConfigureAwait(false);
    }

    public async Task<Result<MyStoreDto>> UpdateAsync(Guid ownerId, SaveStoreRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<MyStoreDto>(StoreErrors.NotFound);

        var checks = await CheckReferencesAsync(ownerId, request, store.CategoryId, ct).ConfigureAwait(false);
        if (!checks.IsSuccess) return Result.Fail<MyStoreDto>(checks.Error!);

        var (min, max) = await PercentRangeAsync(ct).ConfigureAwait(false);
        // Keeping a % that the admin's new limits no longer allow is fine; changing it must respect them.
        var keepsPercent = request.DefaultAffiliatePercent == store.DefaultAffiliatePercent;
        var updated = store.Update(request.ToDetails(),
            keepsPercent ? Math.Min(min, store.DefaultAffiliatePercent) : min,
            keepsPercent ? Math.Max(max, store.DefaultAffiliatePercent) : max);
        if (!updated.IsSuccess) return Result.Fail<MyStoreDto>(updated.Error!);

        if (await stores.SlugExistsAsync(store.Slug, store.Id, ct).ConfigureAwait(false))
            return Result.Fail<MyStoreDto>(StoreErrors.SlugTaken);

        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return await GetMineAsync(ownerId, ct).ConfigureAwait(false);
    }

    public Task<Result<MyStoreDto>> CloseAsync(Guid ownerId, CancellationToken ct = default) =>
        ChangeStatusAsync(ownerId, s => s.Close(), ct);

    public Task<Result<MyStoreDto>> ReopenAsync(Guid ownerId, CancellationToken ct = default) =>
        ChangeStatusAsync(ownerId, s => s.Reopen(), ct);

    private async Task<Result<MyStoreDto>> ChangeStatusAsync(Guid ownerId, Func<Store, Result> change, CancellationToken ct)
    {
        var store = await stores.GetByOwnerAsync(ownerId, ct).ConfigureAwait(false);
        if (store is null) return Result.Fail<MyStoreDto>(StoreErrors.NotFound);

        var changed = change(store);
        if (!changed.IsSuccess) return Result.Fail<MyStoreDto>(changed.Error!);

        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return await GetMineAsync(ownerId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The category must be active — unless the store already uses it (a category hidden later stays on its
    /// stores). Logo and banner must be the owner's own uploads of the right purpose.
    /// </summary>
    private async Task<Result> CheckReferencesAsync(Guid ownerId, SaveStoreRequest request, Guid? currentCategoryId, CancellationToken ct)
    {
        if (request.CategoryId != currentCategoryId)
        {
            var category = await categories.GetByIdAsync(request.CategoryId, ct).ConfigureAwait(false);
            if (category is not { IsActive: true }) return Result.Fail(StoreErrors.CategoryUnavailable);
        }

        if (request.LogoImageId is { } logo)
        {
            var image = await images.RequireOwnAsync(logo, ImagePurpose.StoreLogo, ownerId, ct).ConfigureAwait(false);
            if (!image.IsSuccess) return Result.Fail(image.Error!);
        }
        if (request.BannerImageId is { } banner)
        {
            var image = await images.RequireOwnAsync(banner, ImagePurpose.StoreBanner, ownerId, ct).ConfigureAwait(false);
            if (!image.IsSuccess) return Result.Fail(image.Error!);
        }
        return Result.Ok();
    }

    private async Task<(int Min, int Max)> PercentRangeAsync(CancellationToken ct) =>
        (await settings.GetIntAsync(SettingKeys.AffiliatePercentMin, ct).ConfigureAwait(false),
         await settings.GetIntAsync(SettingKeys.AffiliatePercentMax, ct).ConfigureAwait(false));
}
