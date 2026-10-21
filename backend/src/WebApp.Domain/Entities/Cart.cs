using WebApp.Domain.Common;

namespace WebApp.Domain.Entities;

/// <summary>
/// A signed-in user's cart (013): one per user, kept between visits. A guest keeps their cart in the
/// browser and it is merged here at sign-in. Lines are grouped by store when shown; each line can carry
/// its own affiliate code. Prices, stock and code validity are not stored: they are read fresh every
/// time the cart is shown, and checked again at checkout (014).
/// </summary>
public sealed class Cart : AggregateRoot
{
    public const int MaxLines = 50;
    public const int MaxQuantity = 99;

    private readonly List<CartLine> _lines = [];

    public Guid UserId { get; private set; }
    public IReadOnlyList<CartLine> Lines => _lines;

    private Cart() { }

    public static Cart For(Guid userId) => new() { UserId = userId };

    /// <summary>
    /// Adds a product (or a variant of it). The same product + variant already in the cart gets the
    /// quantity added; a code given here replaces the line's code. Stock and availability are checked by the caller.
    /// </summary>
    public Result<CartLine> Add(Guid productId, Guid? variantId, int quantity, string? affiliateCode)
    {
        var code = NormalizeCode(affiliateCode);
        if (!code.IsSuccess) return Result.Fail<CartLine>(code.Error!);

        var line = Find(productId, variantId);
        if (line is null)
        {
            if (quantity is < 1 or > MaxQuantity) return Result.Fail<CartLine>(DomainErrors.Cart.QuantityInvalid);
            if (_lines.Count >= MaxLines) return Result.Fail<CartLine>(DomainErrors.Cart.TooManyLines);
            line = new CartLine(productId, variantId, quantity, code.Value);
            _lines.Add(line);
        }
        else
        {
            var set = line.SetQuantity(line.Quantity + quantity);
            if (!set.IsSuccess) return Result.Fail<CartLine>(set.Error!);
            if (code.Value is not null) line.SetCode(code.Value);
        }
        MarkUpdated();
        return Result.Ok(line);
    }

    public Result SetQuantity(Guid lineId, int quantity) => Change(lineId, line => line.SetQuantity(quantity));

    /// <summary>Sets (or, with null / empty, removes) the line's affiliate code. Only its format is checked here.</summary>
    public Result SetCode(Guid lineId, string? affiliateCode)
    {
        var code = NormalizeCode(affiliateCode);
        return code.IsSuccess ? Change(lineId, line => line.SetCode(code.Value)) : Result.Fail(code.Error!);
    }

    public Result Remove(Guid lineId)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null) return Result.Fail(DomainErrors.Cart.LineNotFound);
        _lines.Remove(line);
        MarkUpdated();
        return Result.Ok();
    }

    public void Clear()
    {
        if (_lines.Count == 0) return;
        _lines.Clear();
        MarkUpdated();
    }

    /// <summary>
    /// Brings in the cart a guest built before signing in. Same product + variant: quantities add up
    /// (capped at <see cref="MaxQuantity"/>) and the existing code wins; lines beyond <see cref="MaxLines"/>
    /// and malformed codes are dropped rather than failing the sign-in.
    /// </summary>
    public void Merge(IEnumerable<(Guid ProductId, Guid? VariantId, int Quantity, string? AffiliateCode)> guestLines)
    {
        ArgumentNullException.ThrowIfNull(guestLines);
        foreach (var (productId, variantId, quantity, rawCode) in guestLines)
        {
            if (quantity < 1) continue;
            var code = NormalizeCode(rawCode);
            var line = Find(productId, variantId);
            if (line is null)
            {
                if (_lines.Count >= MaxLines) continue;
                _lines.Add(new CartLine(productId, variantId, Math.Min(quantity, MaxQuantity), code.IsSuccess ? code.Value : null));
            }
            else
            {
                line.SetQuantity(Math.Min(line.Quantity + quantity, MaxQuantity));
                if (line.AffiliateCode is null && code is { IsSuccess: true, Value: not null }) line.SetCode(code.Value);
            }
        }
        MarkUpdated();
    }

    public CartLine? Find(Guid productId, Guid? variantId) =>
        _lines.FirstOrDefault(l => l.ProductId == productId && l.VariantId == variantId);

    private Result Change(Guid lineId, Func<CartLine, Result> change)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null) return Result.Fail(DomainErrors.Cart.LineNotFound);
        var changed = change(line);
        if (changed.IsSuccess) MarkUpdated();
        return changed;
    }

    /// <summary>Empty → no code; otherwise the canonical form (<c>#drf45</c> → <c>DRF-45</c>), or an error.</summary>
    private static Result<string?> NormalizeCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Result.Ok<string?>(null);
        var code = Common.AffiliateCode.Normalize(raw);
        return code is null ? Result.Fail<string?>(DomainErrors.Cart.CodeInvalid) : Result.Ok<string?>(code);
    }
}

/// <summary>One product (or variant) in a cart, with its quantity and optional affiliate code.</summary>
public sealed class CartLine : BaseEntity
{
    public Guid ProductId { get; private set; }
    /// <summary>The chosen combination for a product with variants; null otherwise.</summary>
    public Guid? VariantId { get; private set; }
    public int Quantity { get; private set; }
    /// <summary>Canonical code (<c>DRF-45</c>) — checked against the product's store each time the cart is read.</summary>
    public string? AffiliateCode { get; private set; }

    private CartLine() { }

    internal CartLine(Guid productId, Guid? variantId, int quantity, string? affiliateCode)
    {
        ProductId = productId;
        VariantId = variantId;
        Quantity = quantity;
        AffiliateCode = affiliateCode;
    }

    internal Result SetQuantity(int quantity)
    {
        if (quantity is < 1 or > Cart.MaxQuantity) return Result.Fail(DomainErrors.Cart.QuantityInvalid);
        Quantity = quantity;
        MarkUpdated();
        return Result.Ok();
    }

    internal Result SetCode(string? code)
    {
        AffiliateCode = code;
        MarkUpdated();
        return Result.Ok();
    }
}
