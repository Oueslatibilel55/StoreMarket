namespace WebApp.Domain.Enums;

/// <summary>Stored as int — append only, never renumber.</summary>
public enum Role
{
    Buyer = 1,
    Affiliate = 2,
    StoreOwner = 3,
    Admin = 4,
}
