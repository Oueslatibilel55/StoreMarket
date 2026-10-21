using System.Globalization;
using WebApp.Domain.Common;

namespace WebApp.Domain.Settings;

public enum SettingType
{
    Text = 1,
    WholeNumber = 2,
    Number = 3,
}

/// <summary>
/// One tunable business value: its type, default and allowed range. Values are stored as
/// invariant strings (<c>"80"</c>, <c>"0.5"</c>) so the table stays one simple key/value list.
/// </summary>
public sealed record SettingDefinition(
    string Key,
    string Group,
    SettingType Type,
    string DefaultValue,
    decimal? Min = null,
    decimal? Max = null,
    string? Unit = null,
    bool Editable = true)
{
    /// <summary>Max decimals kept for <see cref="SettingType.Number"/> values (millimes for TND).</summary>
    public const int MaxDecimals = 3;

    /// <summary>Checks a raw admin input and returns its canonical stored form.</summary>
    public Result<string> Validate(string? raw)
    {
        if (!Editable) return Result.Fail<string>(DomainErrors.Setting.ReadOnly);
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value)) return Result.Fail<string>(DomainErrors.Setting.ValueRequired);

        if (Type == SettingType.Text) return Result.Ok(value);

        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            || (Type == SettingType.WholeNumber && number != decimal.Truncate(number))
            || (Type == SettingType.Number && decimal.Round(number, MaxDecimals) != number))
            return Result.Fail<string>(Type == SettingType.WholeNumber ? DomainErrors.Setting.IntegerRequired : DomainErrors.Setting.NumberRequired);

        if ((Min is { } min && number < min) || (Max is { } max && number > max))
            return Result.Fail<string>(DomainErrors.Setting.OutOfRange);

        return Result.Ok(Format(number));
    }

    public static string Format(decimal number) => number.ToString("0.###", CultureInfo.InvariantCulture);
}
