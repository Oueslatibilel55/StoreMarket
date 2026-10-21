using WebApp.Application.DTOs;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Settings;

namespace WebApp.Application.Services;

/// <summary>Admin edits of the platform settings. Every change is audited, then the cache is dropped.</summary>
public sealed class PlatformSettingsService(
    IPlatformSettingRepository repository,
    IPlatformSettings settings,
    IAdminQueries queries,
    IAuditLog audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>Public values (no admin data) — what the shop needs to display prices, limits, codes.</summary>
    public Task<IReadOnlyDictionary<string, string>> GetValuesAsync(CancellationToken ct = default) => settings.GetAllAsync(ct);

    public async Task<IReadOnlyList<AdminSettingDto>> ListAsync(CancellationToken ct = default)
    {
        var stored = (await queries.ListStoredSettingsAsync(ct).ConfigureAwait(false)).ToDictionary(s => s.Key, StringComparer.Ordinal);
        return [.. PlatformSettingCatalog.All.Select(d => AdminSettingDto.From(d, stored.GetValueOrDefault(d.Key)))];
    }

    public async Task<Result<AdminSettingDto>> UpdateAsync(AdminActor actor, string key, UpdateSettingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);

        var definition = PlatformSettingCatalog.Find(key);
        if (definition is null) return Result.Fail<AdminSettingDto>(DomainErrors.Setting.Unknown);

        var validated = definition.Validate(request.Value);
        if (!validated.IsSuccess) return Result.Fail<AdminSettingDto>(validated.Error!);

        var crossCheck = await CheckPercentRangeAsync(key, validated.Value, ct).ConfigureAwait(false);
        if (!crossCheck.IsSuccess) return Result.Fail<AdminSettingDto>(crossCheck.Error!);

        var current = await settings.GetAllAsync(ct).ConfigureAwait(false);
        var before = current[key];
        if (before == validated.Value) return await GetAsync(definition, ct).ConfigureAwait(false); // no-op: nothing saved or audited

        var setting = await repository.GetByKeyAsync(key, ct).ConfigureAwait(false);
        if (setting is null)
        {
            var created = PlatformSetting.Create(definition, validated.Value, actor.Id);
            if (!created.IsSuccess) return Result.Fail<AdminSettingDto>(created.Error!);
            setting = created.Value;
            repository.Add(setting);
        }
        else
        {
            var changed = setting.Change(definition, validated.Value, actor.Id);
            if (!changed.IsSuccess) return Result.Fail<AdminSettingDto>(changed.Error!);
        }

        audit.Record(actor.Email, actor.Id, AuditActions.SettingUpdate, AuditTargets.Setting, setting.Id, key,
            new { Value = before }, new { Value = setting.Value }, request.Reason);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        settings.Invalidate();

        return await GetAsync(definition, ct).ConfigureAwait(false);
    }

    /// <summary>The affiliate % range must stay a range: min ≤ max, whichever of the two changes.</summary>
    private async Task<Result> CheckPercentRangeAsync(string key, string value, CancellationToken ct)
    {
        if (key is not (SettingKeys.AffiliatePercentMin or SettingKeys.AffiliatePercentMax)) return Result.Ok();

        var min = key == SettingKeys.AffiliatePercentMin
            ? decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)
            : await settings.GetDecimalAsync(SettingKeys.AffiliatePercentMin, ct).ConfigureAwait(false);
        var max = key == SettingKeys.AffiliatePercentMax
            ? decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)
            : await settings.GetDecimalAsync(SettingKeys.AffiliatePercentMax, ct).ConfigureAwait(false);

        return min <= max ? Result.Ok() : Result.Fail(DomainErrors.Setting.PercentMinAboveMax);
    }

    private async Task<Result<AdminSettingDto>> GetAsync(SettingDefinition definition, CancellationToken ct)
    {
        var stored = (await queries.ListStoredSettingsAsync(ct).ConfigureAwait(false)).FirstOrDefault(s => s.Key == definition.Key);
        return Result.Ok(AdminSettingDto.From(definition, stored));
    }
}
