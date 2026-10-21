using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

/// <summary>An admin correction: signed coins (positive gives, negative takes back), reason required.</summary>
public sealed record CreateCoinAdjustmentRequest(Guid AffiliateId, decimal Coins, string Reason, Guid? RelatedEntryId = null);

public sealed record CoinEntryQuery(Guid? AffiliateId, Guid? StoreId, CoinEntryType? Type, string? Search, DateTime? From, DateTime? To, int? Page, int? PageSize, Guid? ProductId = null);

/// <summary>How the affiliate splits their earned coins (020).</summary>
public enum CoinBreakdownBy
{
    Product = 1,
    Store = 2,
}

/// <summary>Earned coins of one product or store over a period: how many lines, how much sold, how many coins.</summary>
public sealed record CoinBreakdownRow(Guid Id, string Name, string? StoreName, int Lines, int Quantity, decimal Sales, decimal Coins);

/// <summary>A ledger row with everything it traces back to. The affiliate's own view masks the client's phone.</summary>
public sealed record CoinEntryDto(
    Guid Id,
    DateTime CreatedAt,
    Guid AffiliateId,
    string AffiliateName,
    string? AffiliateCode,
    CoinEntryType Type,
    decimal Coins,
    string? OrderReference,
    Guid? StoreId,
    string? StoreName,
    Guid? ProductId,
    string? ProductName,
    string? ClientName,
    string? ClientPhone,
    int? Quantity,
    decimal? LineTotal,
    int? AffiliatePercent,
    decimal? CoinValueInTnd,
    string? Reason,
    Guid? RelatedEntryId);

/// <summary>
/// An affiliate's coins: <c>Balance</c> = sum of the ledger (what they can ask for), <c>Expected</c> = coins of their
/// lines not delivered yet (computed from orders, never written to the ledger), <c>Locked</c> = coins in their open
/// payout request, <c>PaidOut</c> = coins of their paid requests (021).
/// </summary>
public sealed record CoinSummaryDto(decimal Balance, decimal TotalEarned, decimal Expected, decimal CoinValueInTnd, decimal Locked = 0, decimal PaidOut = 0);

/// <summary>Platform totals for the admin ledger screen.</summary>
public sealed record AdminCoinTotals(decimal Balance, decimal TotalEarned, decimal Expected, int Affiliates);
