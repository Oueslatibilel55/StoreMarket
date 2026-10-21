namespace WebApp.Domain.Enums;

/// <summary>Stored as int — append only, never renumber.</summary>
public enum RoleRequestStatus
{
    Pending = 1,
    Approved = 2,
    Refused = 3,
    Cancelled = 4,
}
