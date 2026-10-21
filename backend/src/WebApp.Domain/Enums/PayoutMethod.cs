namespace WebApp.Domain.Enums;

/// <summary>How an affiliate gets their money (021). Stored as int — append only, never renumber.</summary>
public enum PayoutMethod
{
    Cash = 1,
    BankTransfer = 2,
}
