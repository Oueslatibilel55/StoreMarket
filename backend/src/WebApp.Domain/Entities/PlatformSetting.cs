using WebApp.Domain.Common;
using WebApp.Domain.Settings;

namespace WebApp.Domain.Entities;

/// <summary>
/// A setting the admin changed from its default (see <see cref="PlatformSettingCatalog"/>).
/// One row per key; the value is the canonical string produced by <see cref="SettingDefinition.Validate"/>.
/// </summary>
public sealed class PlatformSetting : BaseEntity
{
    public const int MaxKeyLength = 64;
    public const int MaxValueLength = 200;

    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public Guid UpdatedById { get; private set; }

    private PlatformSetting() { }

    public static Result<PlatformSetting> Create(SettingDefinition definition, string? raw, Guid adminId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var value = definition.Validate(raw);
        return value.IsSuccess
            ? Result.Ok(new PlatformSetting { Key = definition.Key, Value = value.Value, UpdatedById = adminId })
            : Result.Fail<PlatformSetting>(value.Error!);
    }

    public Result Change(SettingDefinition definition, string? raw, Guid adminId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Key != Key) throw new ArgumentException($"Definition {definition.Key} does not match setting {Key}.", nameof(definition));

        var value = definition.Validate(raw);
        if (!value.IsSuccess) return Result.Fail(value.Error!);
        if (value.Value == Value) return Result.Ok();

        Value = value.Value;
        UpdatedById = adminId;
        MarkUpdated();
        return Result.Ok();
    }
}
