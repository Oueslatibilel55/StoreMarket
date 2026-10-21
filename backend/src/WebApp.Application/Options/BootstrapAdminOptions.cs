namespace WebApp.Application.Options;

/// <summary>
/// Emails that are made Admin when they sign up or sign in. The only way to create the
/// first admin: set it in user-secrets (dev) or BootstrapAdmin__Emails__0 (prod).
/// Public sign-up can never ask for the Admin role.
/// </summary>
public sealed class BootstrapAdminOptions
{
    public const string SectionName = "BootstrapAdmin";

    public IReadOnlyList<string> Emails { get; init; } = [];

    public bool Contains(string normalizedEmail) =>
        Emails.Any(e => string.Equals(e.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase));
}
