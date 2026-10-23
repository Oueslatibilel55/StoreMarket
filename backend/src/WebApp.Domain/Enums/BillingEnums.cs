namespace WebApp.Domain.Enums;

/// <summary>What a store payment pays (025). Stored as int — append only, never renumber.</summary>
public enum StorePaymentKind
{
    /// <summary>Months of subscription: extends the store's paid-until date.</summary>
    Subscription = 1,
    /// <summary>All or part of a monthly commission invoice.</summary>
    Commission = 2,
}

/// <summary>Where a store payment stands (025). Append only.</summary>
public enum StorePaymentStatus
{
    /// <summary>The owner says they paid; waiting for an admin.</summary>
    Declared = 1,
    /// <summary>Checked by an admin (or recorded by them directly): it counts.</summary>
    Approved = 2,
    /// <summary>Refused by an admin with a reason: it never counts.</summary>
    Rejected = 3,
}

/// <summary>How a store paid the platform. Append only.</summary>
public enum StorePaymentMethod
{
    Cash = 1,
    BankTransfer = 2,
}

/// <summary>Which stores a subscription offer is for. Append only.</summary>
public enum OfferTarget
{
    AllStores = 1,
    StoreCategories = 2,
    Stores = 3,
}

/// <summary>Where a monthly commission invoice stands (025). Append only.</summary>
public enum CommissionInvoiceStatus
{
    Issued = 1,
    PartiallyPaid = 2,
    Paid = 3,
}
