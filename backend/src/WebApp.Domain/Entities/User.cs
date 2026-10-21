using System.Net.Mail;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;
using WebApp.Domain.Events;

namespace WebApp.Domain.Entities;

public sealed class User : AggregateRoot
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int MaxFullNameLength = 120;
    public const int MaxEmailLength = 254;
    public const int MaxBlockReasonLength = 500;

    private readonly List<Role> _roles = [];

    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public UserStatus Status { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public IReadOnlyList<Role> Roles => _roles;

    /// <summary>Unique code (<c>DRF-45</c>) given when the affiliate role is activated; never changes, never reused.</summary>
    public string? AffiliateCode { get; private set; }
    public DateTime? AffiliateTermsAcceptedAt { get; private set; }
    /// <summary>Set when an admin deactivates the code (fraud): it is refused on every order until re-enabled (012).</summary>
    public DateTime? AffiliateCodeDisabledAt { get; private set; }
    public string? AffiliateCodeDisabledReason { get; private set; }

    public DateTime? BlockedAt { get; private set; }
    public string? BlockedReason { get; private set; }

    private User() { }

    /// <summary>
    /// Validates the raw input and creates a client account (Buyer). The password is only checked
    /// here; hashing it is the caller's job (the domain knows no hashing algorithm).
    /// </summary>
    public static Result<User> Register(string email, string password, string fullName, string phone,
        Func<string, string> hashPassword)
    {
        var created = Create(email, password, fullName, phone, hashPassword);
        if (created.IsSuccess) created.Value._roles.Add(Role.Buyer);
        return created;
    }

    /// <summary>
    /// Creates an affiliate account: Affiliate role and code, no Buyer role. Any other role comes
    /// later, through a <see cref="RoleRequest"/> approved by an admin.
    /// </summary>
    public static Result<User> RegisterAffiliate(string email, string password, string fullName, string phone,
        Func<string, string> hashPassword, string affiliateCode, bool termsAccepted, DateTime utcNow)
    {
        var created = Create(email, password, fullName, phone, hashPassword);
        if (!created.IsSuccess) return created;

        var activated = created.Value.ActivateAffiliate(affiliateCode, termsAccepted, utcNow);
        return activated.IsSuccess ? created : Result.Fail<User>(activated.Error!);
    }

    private static Result<User> Create(string email, string password, string fullName, string phone,
        Func<string, string> hashPassword)
    {
        ArgumentNullException.ThrowIfNull(hashPassword);

        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null) return Result.Fail<User>(DomainErrors.User.EmailInvalid);

        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength || password.Length > MaxPasswordLength
            || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return Result.Fail<User>(DomainErrors.User.PasswordTooWeak);

        var name = fullName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxFullNameLength)
            return Result.Fail<User>(DomainErrors.User.FullNameRequired);

        var normalizedPhone = NormalizePhone(phone);
        if (normalizedPhone is null) return Result.Fail<User>(DomainErrors.User.PhoneInvalid);

        return Result.Ok(new User
        {
            Email = normalizedEmail,
            PasswordHash = hashPassword(password),
            FullName = name,
            Phone = normalizedPhone,
            Status = UserStatus.Active,
        });
    }

    public bool IsActive => Status == UserStatus.Active;

    public bool HasRole(Role role) => _roles.Contains(role);

    /// <summary>The roles an account is created with (client or affiliate). Every account keeps at least one.</summary>
    public static bool IsBaseRole(Role role) => role is Role.Buyer or Role.Affiliate;

    public void RecordLogin(DateTime utcNow)
    {
        LastLoginAt = utcNow;
        MarkUpdated();
    }

    /// <summary>
    /// Adds the Affiliate role and code, with the terms the user accepted: at affiliate sign-up, or
    /// when an admin approves an affiliate <see cref="RoleRequest"/> — never as a self-service action.
    /// Idempotent: an existing affiliate keeps their code. The code is generated (and checked unique) by the caller.
    /// </summary>
    public Result ActivateAffiliate(string code, bool termsAccepted, DateTime utcNow)
    {
        if (HasRole(Role.Affiliate)) return Result.Ok();
        if (!IsActive) return Result.Fail(DomainErrors.User.Blocked);
        if (!termsAccepted) return Result.Fail(DomainErrors.User.AffiliateTermsRequired);
        if (!Common.AffiliateCode.IsValid(code)) return Result.Fail(DomainErrors.User.AffiliateCodeInvalid);

        AffiliateCode ??= code;
        AffiliateTermsAcceptedAt = utcNow;
        _roles.Add(Role.Affiliate);
        MarkUpdated();
        RaiseDomainEvent(new AffiliateActivatedEvent(Id, AffiliateCode));
        return Result.Ok();
    }

    /// <summary>
    /// Admin grant. Affiliate needs a code and terms accepted by the user, so it only comes from
    /// sign-up or an approved <see cref="RoleRequest"/>. Granting a role the user has is a no-op.
    /// </summary>
    public Result GrantRole(Role role)
    {
        if (role == Role.Affiliate) return Result.Fail(DomainErrors.User.UseAffiliateActivation);
        if (HasRole(role)) return Result.Ok();
        _roles.Add(role);
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>
    /// Admin revoke. An account keeps at least one base role (Buyer or Affiliate): the last one
    /// can't be removed — block the user instead. Revoking Affiliate keeps the code (history and
    /// coins stay attached to it); re-activating gives the same code back.
    /// </summary>
    public Result RevokeRole(Role role)
    {
        if (!HasRole(role)) return Result.Ok();
        if (IsBaseRole(role) && _roles.Count(IsBaseRole) == 1) return Result.Fail(DomainErrors.User.BaseRoleRequired);

        _roles.Remove(role);
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>
    /// The code an order may carry right now: an active account holding the Affiliate role, whose code
    /// an admin hasn't deactivated. Partnership with the product's store is checked separately.
    /// </summary>
    public bool CanEarnAsAffiliate => IsActive && HasRole(Role.Affiliate) && AffiliateCode is not null && AffiliateCodeDisabledAt is null;

    /// <summary>Admin, for fraud: the code is refused everywhere; partnerships stay as they are. Idempotent.</summary>
    public Result DisableAffiliateCode(string reason, DateTime utcNow)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxBlockReasonLength)
            return Result.Fail(DomainErrors.User.AffiliateCodeReasonRequired);
        if (AffiliateCode is null) return Result.Fail(DomainErrors.User.NotAffiliate);
        if (AffiliateCodeDisabledAt is not null) return Result.Ok();

        AffiliateCodeDisabledAt = utcNow;
        AffiliateCodeDisabledReason = trimmed;
        MarkUpdated();
        return Result.Ok();
    }

    public void EnableAffiliateCode()
    {
        if (AffiliateCodeDisabledAt is null) return;
        AffiliateCodeDisabledAt = null;
        AffiliateCodeDisabledReason = null;
        MarkUpdated();
    }

    public Result Block(string reason, DateTime utcNow)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxBlockReasonLength)
            return Result.Fail(DomainErrors.User.BlockReasonRequired);
        if (HasRole(Role.Admin)) return Result.Fail(DomainErrors.User.CannotBlockAdmin);
        if (!IsActive) return Result.Ok();

        Status = UserStatus.Blocked;
        BlockedAt = utcNow;
        BlockedReason = trimmed;
        MarkUpdated();
        RaiseDomainEvent(new UserBlockedEvent(Id, trimmed));
        return Result.Ok();
    }

    public void Unblock()
    {
        if (IsActive) return;
        Status = UserStatus.Active;
        BlockedAt = null;
        BlockedReason = null;
        MarkUpdated();
    }

    /// <summary>Lower-cased, trimmed, syntactically valid address — or null.</summary>
    public static string? NormalizeEmail(string? email)
    {
        var value = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(value) || value.Length > MaxEmailLength) return null;
        if (!MailAddress.TryCreate(value, out var parsed) || parsed.Address != value) return null;
        return value.Contains('.', StringComparison.Ordinal) ? value : null;
    }

    /// <summary>
    /// Tunisian numbers: 8 digits, optionally prefixed by +216 / 00216. Stored as "+216XXXXXXXX".
    /// Spaces, dots and dashes are ignored.
    /// </summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());

        if (digits.StartsWith("+216", StringComparison.Ordinal)) digits = digits[4..];
        else if (digits.StartsWith("00216", StringComparison.Ordinal)) digits = digits[5..];

        return digits.Length == 8 && digits.All(char.IsDigit) ? "+216" + digits : null;
    }
}
