namespace WebApp.Domain.Enums;

/// <summary>Stored as int — append only, never renumber.</summary>
public enum ProductStatus
{
    /// <summary>Being prepared: not in the shop, may be incomplete.</summary>
    Draft = 1,
    /// <summary>In the shop (when the store is open — 010 / 011).</summary>
    Published = 2,
    /// <summary>No longer sold, kept for history.</summary>
    Archived = 3,
}
