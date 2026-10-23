namespace WebApp.Domain.Enums;

/// <summary>What a review rates (017). Stored as int — append only, never renumber.</summary>
public enum ReviewTarget
{
    /// <summary>One delivered order line: the product as received.</summary>
    Product = 1,
    /// <summary>One delivered store order: the store's service (preparation, delivery, contact).</summary>
    Store = 2,
}
