using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Domain.Entities;

/// <summary>
/// An affiliate's request to turn coins into money (021). Affiliates never withdraw directly: they ask, an admin
/// verifies, schedules a date (cash handover or bank transfer) and marks it paid. The coins are locked in the ledger
/// while the request is open (<see cref="CoinEntryType.PayoutLocked"/>) and released if it is rejected or cancelled.
/// The TND amount is frozen at request time — a later change of the coin value never touches it.
/// </summary>
public sealed class PayoutRequest : AggregateRoot
{
    public const int MaxNoteLength = 500;
    public const int MaxReasonLength = 500;
    public const int MaxPlaceLength = 200;
    public const int MaxPaidReferenceLength = 120;
    public const int MaxBankNameLength = 100;
    public const int MaxAccountHolderLength = 150;
    /// <summary>A Tunisian RIB: 20 digits.</summary>
    public const int RibLength = 20;
    public const decimal MaxCoins = 1_000_000m;

    private readonly List<PayoutEvent> _history = [];

    /// <summary>Shown to the affiliate and the admin: <c>PAY-2026-000012</c>.</summary>
    public string Reference { get; private set; } = null!;
    public Guid AffiliateId { get; private set; }
    public decimal Coins { get; private set; }
    /// <summary>Coin value when the request was sent (platform setting, frozen).</summary>
    public decimal CoinValueInTnd { get; private set; }
    public decimal AmountTnd { get; private set; }
    public PayoutMethod Method { get; private set; }
    public string? BankName { get; private set; }
    public string? AccountHolder { get; private set; }
    public string? Rib { get; private set; }
    public string? Note { get; private set; }
    public PayoutStatus Status { get; private set; }

    /// <summary>Cash: date and time of the handover. Transfer: the planned transfer date.</summary>
    public DateTime? ScheduledFor { get; private set; }
    /// <summary>Cash only: where the affiliate gets the money.</summary>
    public string? Place { get; private set; }
    public DateTime? PaidAt { get; private set; }
    /// <summary>Bank reference of the transfer, or who handed over the cash.</summary>
    public string? PaidReference { get; private set; }
    public string? RejectionReason { get; private set; }
    public IReadOnlyList<PayoutEvent> History => _history;

    private PayoutRequest() { }

    public static string ReferenceFor(int year, long number) => $"PAY-{year}-{number:D6}";

    /// <summary>
    /// A new request. The caller checked the minimum, the available coins and that no other request is open, and
    /// locks the coins in the same transaction.
    /// </summary>
    public static Result<PayoutRequest> Create(long number, Guid affiliateId, decimal coins, decimal coinValueInTnd, PayoutMethod method,
        string? bankName, string? accountHolder, string? rib, string? note, DateTime utcNow)
    {
        if (coins <= 0 || coins > MaxCoins || decimal.Round(coins, 3) != coins) return Result.Fail<PayoutRequest>(DomainErrors.Payout.CoinsInvalid);
        if (!Enum.IsDefined(method)) return Result.Fail<PayoutRequest>(DomainErrors.Payout.MethodInvalid);
        var text = Optional(note);
        if (text?.Length > MaxNoteLength) return Result.Fail<PayoutRequest>(DomainErrors.Payout.NoteTooLong);

        string? bank = null, holder = null, account = null;
        if (method == PayoutMethod.BankTransfer)
        {
            bank = Optional(bankName);
            holder = Optional(accountHolder);
            if (bank is null || holder is null || string.IsNullOrWhiteSpace(rib)) return Result.Fail<PayoutRequest>(DomainErrors.Payout.BankDetailsRequired);
            if (bank.Length > MaxBankNameLength || holder.Length > MaxAccountHolderLength) return Result.Fail<PayoutRequest>(DomainErrors.Payout.BankDetailsTooLong);
            account = NormalizeRib(rib);
            if (account is null) return Result.Fail<PayoutRequest>(DomainErrors.Payout.RibInvalid);
        }

        var request = new PayoutRequest
        {
            Reference = ReferenceFor(utcNow.Year, number),
            AffiliateId = affiliateId,
            Coins = coins,
            CoinValueInTnd = coinValueInTnd,
            AmountTnd = decimal.Round(coins * coinValueInTnd, 3),
            Method = method,
            BankName = bank,
            AccountHolder = holder,
            Rib = account,
            Note = text,
            Status = PayoutStatus.Requested,
        };
        request._history.Add(new PayoutEvent(PayoutStatus.Requested, affiliateId, text, utcNow));
        return Result.Ok(request);
    }

    /// <summary>Requested, verified or scheduled: its coins are locked and the affiliate cannot ask again.</summary>
    public bool IsOpen => IsOpenStatus(Status);
    public static bool IsOpenStatus(PayoutStatus status) => status is PayoutStatus.Requested or PayoutStatus.Verified or PayoutStatus.Scheduled;

    public Result Verify(Guid adminId, string? note, DateTime utcNow)
    {
        if (Status != PayoutStatus.Requested) return Result.Fail(DomainErrors.Payout.TransitionNotAllowed);
        var text = Optional(note);
        if (text?.Length > MaxReasonLength) return Result.Fail(DomainErrors.Payout.ReasonTooLong);
        return Move(PayoutStatus.Verified, adminId, text, utcNow);
    }

    /// <summary>Refused before it is scheduled; the caller releases the coins.</summary>
    public Result Reject(Guid adminId, string? reason, DateTime utcNow)
    {
        if (Status is not (PayoutStatus.Requested or PayoutStatus.Verified)) return Result.Fail(DomainErrors.Payout.TransitionNotAllowed);
        var text = Optional(reason);
        if (text is null) return Result.Fail(DomainErrors.Payout.ReasonRequired);
        if (text.Length > MaxReasonLength) return Result.Fail(DomainErrors.Payout.ReasonTooLong);
        RejectionReason = text;
        return Move(PayoutStatus.Rejected, adminId, text, utcNow);
    }

    /// <summary>Sets (or moves) the date. Cash needs a place; a transfer only a date. The date cannot be before today.</summary>
    public Result Schedule(Guid adminId, DateTime? scheduledFor, string? place, DateTime utcNow)
    {
        if (Status is not (PayoutStatus.Verified or PayoutStatus.Scheduled)) return Result.Fail(DomainErrors.Payout.TransitionNotAllowed);
        if (scheduledFor is not { } at) return Result.Fail(DomainErrors.Payout.DateRequired);
        var when = DateTime.SpecifyKind(at, DateTimeKind.Utc);
        if (when.Date < utcNow.Date) return Result.Fail(DomainErrors.Payout.DateInPast);
        var where = Optional(place);
        if (Method == PayoutMethod.Cash && where is null) return Result.Fail(DomainErrors.Payout.PlaceRequired);
        if (where?.Length > MaxPlaceLength) return Result.Fail(DomainErrors.Payout.PlaceTooLong);

        ScheduledFor = when;
        Place = Method == PayoutMethod.Cash ? where : null;
        return Move(PayoutStatus.Scheduled, adminId, Place, utcNow);
    }

    /// <summary>The money was handed over / transferred. Final: the locked coins are gone for good.</summary>
    public Result MarkPaid(Guid adminId, DateTime? paidAt, string? reference, DateTime utcNow)
    {
        if (Status != PayoutStatus.Scheduled) return Result.Fail(DomainErrors.Payout.TransitionNotAllowed);
        var text = Optional(reference);
        if (text is null) return Result.Fail(DomainErrors.Payout.PaidReferenceRequired);
        if (text.Length > MaxPaidReferenceLength) return Result.Fail(DomainErrors.Payout.PaidReferenceTooLong);
        var when = paidAt is { } p ? DateTime.SpecifyKind(p, DateTimeKind.Utc) : utcNow;
        if (when > utcNow.AddMinutes(5)) return Result.Fail(DomainErrors.Payout.PaidInFuture);

        PaidAt = when;
        PaidReference = text;
        return Move(PayoutStatus.Paid, adminId, text, utcNow);
    }

    /// <summary>The affiliate withdraws it while nothing is planned yet; the caller releases the coins.</summary>
    public Result Cancel(Guid affiliateId, DateTime utcNow)
    {
        if (affiliateId != AffiliateId || Status is not (PayoutStatus.Requested or PayoutStatus.Verified))
            return Result.Fail(DomainErrors.Payout.TransitionNotAllowed);
        return Move(PayoutStatus.Cancelled, affiliateId, null, utcNow);
    }

    /// <summary>"0123 4567 8901 2345 6789" → 20 digits, or null when it is not a RIB.</summary>
    public static string? NormalizeRib(string? rib)
    {
        if (string.IsNullOrWhiteSpace(rib)) return null;
        var digits = new string([.. rib.Where(c => c is not (' ' or '-' or '.'))]);
        return digits.Length == RibLength && digits.All(char.IsAsciiDigit) ? digits : null;
    }

    private Result Move(PayoutStatus to, Guid actorId, string? note, DateTime utcNow)
    {
        Status = to;
        _history.Add(new PayoutEvent(to, actorId, note, utcNow));
        MarkUpdated();
        return Result.Ok();
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>One step of a payout request: who moved it, when, with which note / reason / place / reference.</summary>
public sealed class PayoutEvent : BaseEntity
{
    public PayoutStatus Status { get; private set; }
    public Guid ActorId { get; private set; }
    public string? Note { get; private set; }
    public DateTime At { get; private set; }

    private PayoutEvent() { }

    internal PayoutEvent(PayoutStatus status, Guid actorId, string? note, DateTime at)
    {
        Status = status;
        ActorId = actorId;
        Note = note;
        At = at;
    }
}
