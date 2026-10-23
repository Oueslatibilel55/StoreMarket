using WebApp.Domain.Common;
using WebApp.Domain.Enums;

namespace WebApp.Domain.Entities;

/// <summary>
/// A verified rating (017): 1–5 stars and an optional comment, written by the client of a <b>delivered</b> order —
/// one per order line (the product) and one per store order (the store). Who may write it is checked by the caller
/// against the order; the domain keeps the rules of the review itself: the edit window frozen at creation, the
/// store owner's public reply, and the admin's hide (excluded from averages, audited by the caller).
/// </summary>
public sealed class Review : AggregateRoot
{
    public const int MaxCommentLength = 1000;
    public const int MaxReplyLength = 1000;
    public const int MaxReasonLength = 500;
    public const int MaxAuthorNameLength = 60;

    public ReviewTarget Target { get; private set; }
    public Guid AuthorId { get; private set; }
    /// <summary>Shown publicly: first name and initial (<c>Leila G.</c>), frozen at creation.</summary>
    public string AuthorName { get; private set; } = null!;
    public Guid OrderId { get; private set; }
    public Guid StoreOrderId { get; private set; }
    /// <summary>Set for a product review only.</summary>
    public Guid? OrderLineId { get; private set; }
    /// <summary>Set for a product review only.</summary>
    public Guid? ProductId { get; private set; }
    public Guid StoreId { get; private set; }

    public int Stars { get; private set; }
    public string? Comment { get; private set; }
    /// <summary>Last time the author changed stars or comment; null when never edited.</summary>
    public DateTime? EditedAt { get; private set; }
    /// <summary>Creation + <c>reviews.editWindowDays</c> at that time (frozen: a setting change never reopens old reviews).</summary>
    public DateTime EditableUntil { get; private set; }

    public string? Reply { get; private set; }
    public DateTime? RepliedAt { get; private set; }

    /// <summary>Set when an admin hides an abusive review: out of every public list and average.</summary>
    public DateTime? HiddenAt { get; private set; }
    public string? HiddenReason { get; private set; }

    public bool IsHidden => HiddenAt is not null;

    private Review() { }

    public static Result<Review> ForProduct(Guid authorId, string authorFullName, Guid orderId, Guid storeOrderId, Guid orderLineId,
        Guid productId, Guid storeId, int stars, string? comment, DateTime utcNow, int editWindowDays) =>
        Create(ReviewTarget.Product, authorId, authorFullName, orderId, storeOrderId, orderLineId, productId, storeId, stars, comment, utcNow, editWindowDays);

    public static Result<Review> ForStore(Guid authorId, string authorFullName, Guid orderId, Guid storeOrderId, Guid storeId,
        int stars, string? comment, DateTime utcNow, int editWindowDays) =>
        Create(ReviewTarget.Store, authorId, authorFullName, orderId, storeOrderId, null, null, storeId, stars, comment, utcNow, editWindowDays);

    private static Result<Review> Create(ReviewTarget target, Guid authorId, string authorFullName, Guid orderId, Guid storeOrderId,
        Guid? orderLineId, Guid? productId, Guid storeId, int stars, string? comment, DateTime utcNow, int editWindowDays)
    {
        var checkedContent = Check(stars, comment);
        if (!checkedContent.IsSuccess) return Result.Fail<Review>(checkedContent.Error!);

        return Result.Ok(new Review
        {
            Target = target,
            AuthorId = authorId,
            AuthorName = DisplayName(authorFullName),
            OrderId = orderId,
            StoreOrderId = storeOrderId,
            OrderLineId = orderLineId,
            ProductId = productId,
            StoreId = storeId,
            Stars = stars,
            Comment = checkedContent.Value,
            EditableUntil = utcNow.AddDays(Math.Max(0, editWindowDays)),
        });
    }

    public bool CanEdit(DateTime utcNow) => !IsHidden && utcNow <= EditableUntil;

    /// <summary>The author changes stars or comment, within the edit window. A hidden review can't be edited.</summary>
    public Result Edit(int stars, string? comment, DateTime utcNow)
    {
        if (IsHidden) return Result.Fail(DomainErrors.Review.Hidden);
        if (utcNow > EditableUntil) return Result.Fail(DomainErrors.Review.EditWindowClosed);
        var checkedContent = Check(stars, comment);
        if (!checkedContent.IsSuccess) return Result.Fail(checkedContent.Error!);
        if (Stars == stars && Comment == checkedContent.Value) return Result.Ok();

        Stars = stars;
        Comment = checkedContent.Value;
        EditedAt = utcNow;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>The store owner answers publicly (or changes the answer). Not on a hidden review.</summary>
    public Result SetReply(string? text, DateTime utcNow)
    {
        if (IsHidden) return Result.Fail(DomainErrors.Review.Hidden);
        var reply = text?.Trim();
        if (string.IsNullOrEmpty(reply)) return Result.Fail(DomainErrors.Review.ReplyRequired);
        if (reply.Length > MaxReplyLength) return Result.Fail(DomainErrors.Review.ReplyTooLong);
        if (Reply == reply) return Result.Ok();

        Reply = reply;
        RepliedAt = utcNow;
        MarkUpdated();
        return Result.Ok();
    }

    public void RemoveReply()
    {
        if (Reply is null) return;
        Reply = null;
        RepliedAt = null;
        MarkUpdated();
    }

    /// <summary>An admin hides an abusive review (reason required): it leaves the public lists and the averages.</summary>
    public Result Hide(string? reason, DateTime utcNow)
    {
        if (IsHidden) return Result.Fail(DomainErrors.Review.AlreadyHidden);
        var why = CheckReason(reason);
        if (!why.IsSuccess) return Result.Fail(why.Error!);

        HiddenAt = utcNow;
        HiddenReason = why.Value;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>An admin brings a hidden review back (reason required, kept in the audit log only).</summary>
    public Result Unhide(string? reason)
    {
        if (!IsHidden) return Result.Fail(DomainErrors.Review.NotHidden);
        var why = CheckReason(reason);
        if (!why.IsSuccess) return Result.Fail(why.Error!);

        HiddenAt = null;
        HiddenReason = null;
        MarkUpdated();
        return Result.Ok();
    }

    /// <summary>"Leila Gharbi" → "Leila G."; a single name stays as is.</summary>
    public static string DisplayName(string fullName)
    {
        var parts = (fullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var name = parts.Length switch
        {
            0 => "?",
            1 => parts[0],
            _ => $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}.",
        };
        return name.Length > MaxAuthorNameLength ? name[..MaxAuthorNameLength] : name;
    }

    private static Result<string?> Check(int stars, string? comment)
    {
        if (stars is < Ranking.MinStars or > Ranking.MaxStars) return Result.Fail<string?>(DomainErrors.Review.StarsInvalid);
        var text = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (text?.Length > MaxCommentLength) return Result.Fail<string?>(DomainErrors.Review.CommentTooLong);
        return Result.Ok(text);
    }

    private static Result<string> CheckReason(string? reason)
    {
        var why = reason?.Trim();
        if (string.IsNullOrEmpty(why)) return Result.Fail<string>(DomainErrors.Review.ReasonRequired);
        if (why.Length > MaxReasonLength) return Result.Fail<string>(DomainErrors.Review.ReasonTooLong);
        return Result.Ok(why);
    }
}
