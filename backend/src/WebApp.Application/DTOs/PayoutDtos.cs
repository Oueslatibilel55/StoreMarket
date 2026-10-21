using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

/// <summary>The affiliate asks for N coins, in cash or by bank transfer (bank details required for a transfer).</summary>
public sealed record CreatePayoutRequest(decimal Coins, PayoutMethod Method, string? BankName = null, string? AccountHolder = null, string? Rib = null, string? Note = null);

public sealed record VerifyPayoutRequest(string? Note = null);

public sealed record RejectPayoutRequest(string? Reason);

/// <summary>Cash: date + time and place. Transfer: the planned date.</summary>
public sealed record SchedulePayoutRequest(DateTime? ScheduledFor, string? Place = null);

/// <summary>When the money was handed over (default now) and the bank reference or the name of who handed the cash.</summary>
public sealed record MarkPayoutPaidRequest(string? Reference, DateTime? PaidAt = null);

/// <param name="ByAffiliate">True for the affiliate's own steps (request, cancel).</param>
/// <param name="ActorName">Who did it — only in the admin view.</param>
public sealed record PayoutEventDto(PayoutStatus Status, DateTime At, bool ByAffiliate, string? ActorName, string? Note);

public sealed record PayoutDto(
    Guid Id,
    string Reference,
    DateTime CreatedAt,
    Guid AffiliateId,
    string AffiliateName,
    string? AffiliateCode,
    decimal Coins,
    decimal CoinValueInTnd,
    decimal AmountTnd,
    PayoutMethod Method,
    string? BankName,
    string? AccountHolder,
    string? Rib,
    string? Note,
    PayoutStatus Status,
    DateTime? ScheduledFor,
    string? Place,
    DateTime? PaidAt,
    string? PaidReference,
    string? RejectionReason,
    IReadOnlyList<PayoutEventDto> History);

/// <summary>The last bank details the affiliate used, offered again on their next transfer request.</summary>
public sealed record PayoutBankDetails(string BankName, string AccountHolder, string Rib);

/// <summary>
/// The affiliate's payout page: coins they can ask for now (balance), the platform minimum, the coin value, their
/// last bank details and every request they made, newest first.
/// </summary>
public sealed record MyPayoutsDto(decimal Available, decimal Minimum, decimal CoinValueInTnd, PayoutBankDetails? LastBankDetails, IReadOnlyList<PayoutDto> Items);

public sealed record PayoutQuery(
    PayoutStatus? Status, PayoutMethod? Method, DateTime? ScheduledFrom, DateTime? ScheduledTo, string? Search, Guid? AffiliateId, int? Page, int? PageSize);

public sealed record PayoutListItem(
    Guid Id,
    string Reference,
    DateTime CreatedAt,
    Guid AffiliateId,
    string AffiliateName,
    string? AffiliateCode,
    decimal Coins,
    decimal AmountTnd,
    PayoutMethod Method,
    PayoutStatus Status,
    DateTime? ScheduledFor,
    string? Place,
    DateTime? PaidAt);

/// <summary>What the admin needs on the list header: open requests per step and the coins they lock.</summary>
public sealed record AdminPayoutCounts(int Requested, int Verified, int Scheduled, decimal LockedCoins);

public sealed record PayoutAffiliate(
    Guid Id, string FullName, string Email, string? Phone, string? Code, bool IsActive, bool CanEarn, decimal Balance, decimal TotalEarned);

/// <summary>
/// An earned entry the request pays out (oldest coins first, after what earlier payouts already took).
/// <c>InvoiceId</c> is set once the store was billed for this commission (025).
/// </summary>
/// <param name="CoveredCoins">The part of the entry this request takes (the first and last entries can be partial).</param>
public sealed record PayoutCoveredEntry(
    Guid EntryId, DateTime CreditedAt, string? OrderReference, Guid? StoreId, string? StoreName, string? ProductName, decimal Coins, decimal CoveredCoins, Guid? InvoiceId);

/// <summary>The verification screen: the request, the affiliate, the coins it covers and their other requests.</summary>
public sealed record AdminPayoutDetail(
    PayoutDto Payout,
    PayoutAffiliate Affiliate,
    IReadOnlyList<PayoutCoveredEntry> Covered,
    decimal AdjustmentsTotal,
    IReadOnlyList<PayoutListItem> OtherPayouts);
