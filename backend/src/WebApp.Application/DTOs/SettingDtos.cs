using WebApp.Domain.Settings;

namespace WebApp.Application.DTOs;

/// <summary>New value as typed by the admin (invariant format: <c>0.5</c>, <c>80</c>), with an optional reason.</summary>
public sealed record UpdateSettingRequest(string Value, string? Reason);

/// <summary>A changed setting as stored (keys without a row use their default).</summary>
public sealed record StoredSetting(string Key, string Value, DateTime UpdatedAt, string? UpdatedByEmail);

/// <summary>Admin view: definition + current value + who changed it last.</summary>
public sealed record AdminSettingDto(
    string Key,
    string Group,
    SettingType Type,
    string Value,
    string DefaultValue,
    decimal? Min,
    decimal? Max,
    string? Unit,
    bool Editable,
    DateTime? UpdatedAt,
    string? UpdatedByEmail)
{
    public static AdminSettingDto From(SettingDefinition definition, StoredSetting? stored)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new(definition.Key, definition.Group, definition.Type, stored?.Value ?? definition.DefaultValue,
            definition.DefaultValue, definition.Min, definition.Max, definition.Unit, definition.Editable,
            stored?.UpdatedAt, stored?.UpdatedByEmail);
    }
}
