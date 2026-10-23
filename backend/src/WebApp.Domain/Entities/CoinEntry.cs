using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Domain.Entities;

/// <summary>
/// One movement of an affiliate's coins (019). The ledger is <b>append-only</b>: an entry is never edited or
/// deleted — a mistake is corrected by a new <see cref="CoinEntryType.Adjustment"/>. An <c>Earned</c> entry points
/// to everything that produced it (order, store order, line, client, product, store) and the values used, all
/// copied from the frozen order line, so the coin can always be traced back.
/// </summary>
public sealed class CoinEntry : AggregateRoot
{
    public const int MaxReasonLength = 500;
    public const decimal MaxAdjustment = 1_000_000m;

    public Guid AffiliateId { get; private set; }
    public CoinEntryType Type { get; private set; }
    /// <summary>Signed: earned and released coins are positive, locked and negative adjustments are negative.</summary>
    public decimal Coins { get; private set; }

    public Guid? OrderId { get; private set; }
    public Guid? StoreOrderId { get; private set; }
    public Guid? OrderLineId { get; private set; }
    public string? OrderReference { get; private set; }
    public Guid? ClientUserId { get; private set; }
    public string? ClientName { get; private set; }
    public string? ClientPhone { get; private set; }
    public Guid? ProductId { get; private set; }
    public string? ProductName { get; private set; }
    public Guid? StoreId { get; private set; }
    public string? StoreName { get; private set; }
    public int? Quantity { get; private set; }
    public decimal? LineTotal { get; private set; }
    public int? AffiliatePercent { get; private set; }
    public decimal? CoinValueInTnd { get; private set; }

    /// <summary>The entry an adjustment corrects.</summary>
    public Guid? RelatedEntryId { get; private set; }
    /// <summary>The payout request that locked or released these coins (021).</summary>
    public Guid? PayoutRequestId { get; private set; }
    public string? Reason { get; private set; }
    /// <summary>The admin who made an adjustment.</summary>
    public Guid? CreatedById { get; private set; }

    private CoinEntry() { }

    /// <summary>
    /// The coins of a delivered order line with an affiliate (016 → 019): the expected coins frozen at checkout
    /// become earned. Null when the line pays nobody.
    /// </summary>
    public static CoinEntry? Earned(Order order, StoreOrder storeOrder, OrderLine line)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(storeOrder);
        ArgumentNullException.ThrowIfNull(line);
        if (storeOrder.Status != StoreOrderStatus.Delivered) throw new InvalidOperationException("Coins are earned on delivered orders only.");
        if (line.AffiliateId is not { } affiliateId || line.ExpectedCoins is not { } coins || coins <= 0) return null;

        return new CoinEntry
        {
            AffiliateId = affiliateId,
            Type = CoinEntryType.Earned,
            Coins = coins,
            OrderId = order.Id,
            StoreOrderId = storeOrder.Id,
            OrderLineId = line.Id,
            OrderReference = order.Reference,
            // In affiliate mode the client has no account here: name and phone still identify them.
            ClientUserId = order.ClientId,
            ClientName = order.ClientName,
            ClientPhone = order.ClientPhone,
            ProductId = line.ProductId,
            ProductName = line.ProductName,
            StoreId = storeOrder.StoreId,
            StoreName = storeOrder.StoreName,
            Quantity = line.Quantity,
            LineTotal = line.LineTotal,
            AffiliatePercent = line.AffiliatePercent,
            CoinValueInTnd = order.CoinValueInTnd,
        };
    }

    /// <summary>Coins set aside by a payout request (negative): they cannot be requested twice (021).</summary>
    public static CoinEntry PayoutLocked(PayoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new CoinEntry { AffiliateId = request.AffiliateId, Type = CoinEntryType.PayoutLocked, Coins = -request.Coins, PayoutRequestId = request.Id };
    }

    /// <summary>Locked coins given back when the request is rejected or cancelled (positive, with the reason if any).</summary>
    public static CoinEntry PayoutReleased(PayoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Status is not (PayoutStatus.Rejected or PayoutStatus.Cancelled))
            throw new InvalidOperationException("Only a rejected or cancelled payout releases its coins.");
        return new CoinEntry
        {
            AffiliateId = request.AffiliateId, Type = CoinEntryType.PayoutReleased, Coins = request.Coins,
            PayoutRequestId = request.Id, Reason = request.RejectionReason,
        };
    }

    /// <summary>An admin correction: signed, never zero, always with a reason (audited by the caller).</summary>
    public static Result<CoinEntry> Adjustment(Guid affiliateId, decimal coins, string? reason, Guid adminId, Guid? relatedEntryId)
    {
        var note = reason?.Trim();
        if (string.IsNullOrEmpty(note)) return Result.Fail<CoinEntry>(DomainErrors.Coins.ReasonRequired);
        if (note.Length > MaxReasonLength) return Result.Fail<CoinEntry>(DomainErrors.Coins.ReasonTooLong);
        if (coins == 0 || Math.Abs(coins) > MaxAdjustment || decimal.Round(coins, 3) != coins)
            return Result.Fail<CoinEntry>(DomainErrors.Coins.AmountInvalid);

        return Result.Ok(new CoinEntry
        {
            AffiliateId = affiliateId,
            Type = CoinEntryType.Adjustment,
            Coins = coins,
            RelatedEntryId = relatedEntryId,
            Reason = note,
            CreatedById = adminId,
        });
    }
}
