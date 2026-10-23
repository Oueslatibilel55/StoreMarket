using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Domain.Entities;

/// <summary>A ledger entry to bill: an <c>Earned</c> commission of the store, or an admin adjustment of one (signed).</summary>
public sealed record CommissionLineInput(
    Guid CoinEntryId,
    CoinEntryType Type,
    DateTime CreditedAt,
    string? OrderReference,
    string? ProductName,
    int? Quantity,
    decimal? LineTotal,
    int? AffiliatePercent,
    string? AffiliateCode,
    decimal Coins,
    decimal CoinValueInTnd);

/// <summary>
/// What a store owes for one month of affiliate commissions (025): one frozen line per ledger entry, amount = coins ×
/// the coin value of the order (= line total × %). Issued after the month ends, due on a day of the next month; paid by
/// approved store payments, fully or in parts. Each ledger entry is on one invoice only (unique index on the line).
/// </summary>
public sealed class CommissionInvoice : AggregateRoot
{
    public const int MaxLines = 20_000;

    private readonly List<CommissionInvoiceLine> _lines = [];

    /// <summary><c>COM-202610-000042</c>.</summary>
    public string Reference { get; private set; } = null!;
    public Guid StoreId { get; private set; }
    public string StoreName { get; private set; } = null!;
    /// <summary>First day of the invoiced month.</summary>
    public DateOnly Month { get; private set; }
    public DateOnly DueOn { get; private set; }
    public decimal Total { get; private set; }
    /// <summary>Sum of the approved payments.</summary>
    public decimal PaidAmount { get; private set; }
    public CommissionInvoiceStatus Status { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public IReadOnlyList<CommissionInvoiceLine> Lines => _lines;

    private CommissionInvoice() { }

    public static string ReferenceFor(DateOnly month, long number) => $"COM-{month:yyyyMM}-{number:D6}";

    /// <summary>The due date: <paramref name="dueDay"/> (1–28) of the month after <paramref name="month"/>.</summary>
    public static DateOnly DueDateOf(DateOnly month, int dueDay)
    {
        var next = new DateOnly(month.Year, month.Month, 1).AddMonths(1);
        return new DateOnly(next.Year, next.Month, Math.Clamp(dueDay, 1, 28));
    }

    /// <summary>A month with nothing (or a net credit) to bill is not issued: its lines wait for the next month.</summary>
    public static Result<CommissionInvoice> Issue(long number, Guid storeId, string storeName, DateOnly month,
        IReadOnlyList<CommissionLineInput> lines, int dueDay)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var first = new DateOnly(month.Year, month.Month, 1);
        var built = lines.Select(CommissionInvoiceLine.From).ToList();
        var total = built.Sum(l => l.Amount);
        if (built.Count == 0 || built.Count > MaxLines || total <= 0) return Result.Fail<CommissionInvoice>(DomainErrors.Billing.NothingToInvoice);

        var invoice = new CommissionInvoice
        {
            Reference = ReferenceFor(first, number),
            StoreId = storeId,
            StoreName = storeName,
            Month = first,
            DueOn = DueDateOf(first, dueDay),
            Total = total,
            Status = CommissionInvoiceStatus.Issued,
        };
        invoice._lines.AddRange(built);
        return Result.Ok(invoice);
    }

    public decimal Remaining => Total - PaidAmount;
    public bool IsPaid => Status == CommissionInvoiceStatus.Paid;

    /// <summary>Unpaid after its due date and the grace days.</summary>
    public bool IsOverdue(DateOnly today, int graceDays) => !IsPaid && today > DueOn.AddDays(graceDays);

    /// <summary>An approved payment; it can't be more than what is left.</summary>
    public Result ApplyPayment(decimal amount, DateTime utcNow)
    {
        if (IsPaid) return Result.Fail(DomainErrors.Billing.InvoicePaid);
        if (amount <= 0 || decimal.Round(amount, 3) != amount) return Result.Fail(DomainErrors.Billing.AmountInvalid);
        if (amount > Remaining) return Result.Fail(DomainErrors.Billing.AmountTooHigh);

        PaidAmount += amount;
        if (Remaining == 0)
        {
            Status = CommissionInvoiceStatus.Paid;
            PaidAt = utcNow;
        }
        else
        {
            Status = CommissionInvoiceStatus.PartiallyPaid;
        }
        MarkUpdated();
        return Result.Ok();
    }
}

/// <summary>One billed ledger entry, frozen when the invoice is issued.</summary>
public sealed class CommissionInvoiceLine : BaseEntity
{
    public Guid CoinEntryId { get; private set; }
    public CoinEntryType Type { get; private set; }
    public DateTime CreditedAt { get; private set; }
    public string? OrderReference { get; private set; }
    public string? ProductName { get; private set; }
    public int? Quantity { get; private set; }
    public decimal? LineTotal { get; private set; }
    public int? AffiliatePercent { get; private set; }
    public string? AffiliateCode { get; private set; }
    public decimal Coins { get; private set; }
    public decimal CoinValueInTnd { get; private set; }
    /// <summary>TND owed for this entry (negative for an adjustment that gives coins back).</summary>
    public decimal Amount { get; private set; }

    private CommissionInvoiceLine() { }

    internal static CommissionInvoiceLine From(CommissionLineInput i) => new()
    {
        CoinEntryId = i.CoinEntryId,
        Type = i.Type,
        CreditedAt = i.CreditedAt,
        OrderReference = i.OrderReference,
        ProductName = i.ProductName,
        Quantity = i.Quantity,
        LineTotal = i.LineTotal,
        AffiliatePercent = i.AffiliatePercent,
        AffiliateCode = i.AffiliateCode,
        Coins = i.Coins,
        CoinValueInTnd = i.CoinValueInTnd,
        Amount = decimal.Round(i.Coins * i.CoinValueInTnd, 3, MidpointRounding.AwayFromZero),
    };
}
