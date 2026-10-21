namespace WebApp.Application.DTOs;

/// <summary>Add a product to the cart. <c>VariantId</c> is required for a product with variants.</summary>
public sealed record AddCartLineRequest(Guid ProductId, Guid? VariantId, int Quantity, string? AffiliateCode);

public sealed record UpdateCartLineRequest(int Quantity);

/// <summary>Empty or null removes the line's code.</summary>
public sealed record SetCartCodeRequest(string? Code);

/// <summary>A line of a guest cart kept in the browser (no id yet): sent to preview it, or merged at sign-in.</summary>
public sealed record GuestCartLine(Guid ProductId, Guid? VariantId, int Quantity, string? AffiliateCode);

public sealed record GuestCartRequest(IReadOnlyList<GuestCartLine> Lines);

/// <summary>
/// Why a line can't be ordered right now (null = fine): <c>productUnavailable</c> (removed, hidden or its store
/// disabled), <c>storeClosed</c>, <c>variantRequired</c>, <c>variantUnavailable</c>, <c>outOfStock</c>,
/// <c>notEnoughStock</c> (see <see cref="CartLineDto.Available"/>). Translated by the client as <c>cart.issues.*</c>.
/// </summary>
public static class CartIssues
{
    public const string ProductUnavailable = "productUnavailable";
    public const string StoreClosed = "storeClosed";
    public const string VariantRequired = "variantRequired";
    public const string VariantUnavailable = "variantUnavailable";
    public const string OutOfStock = "outOfStock";
    public const string NotEnoughStock = "notEnoughStock";
}

/// <summary>The line's code, checked now: valid (who and what %) or the <c>affiliateCode.*</c> error of 012.</summary>
public sealed record CartCodeStatus(bool Valid, string? AffiliateName, int? AffiliatePercent, string? Error);

/// <summary>One option of the chosen variant, e.g. Pointure: 38.</summary>
public sealed record CartOptionLabel(LocalizedName Axis, LocalizedName Value);

/// <param name="Available">Units in stock right now (variant or product).</param>
/// <param name="Issue">Why the line can't be ordered (see <see cref="CartIssues"/>), or null.</param>
public sealed record CartLineDto(
    Guid? Id,
    Guid ProductId,
    Guid? VariantId,
    string ProductName,
    string ProductSlug,
    string? ImageUrl,
    IReadOnlyList<CartOptionLabel> Options,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    int Available,
    string? Issue,
    string? AffiliateCode,
    CartCodeStatus? Code);

/// <param name="Subtotal">Lines that can be ordered, without delivery.</param>
public sealed record CartStoreGroup(
    Guid StoreId,
    string Name,
    string Slug,
    string? LogoUrl,
    bool IsOpen,
    decimal DeliveryFee,
    IReadOnlyList<string> DeliveryCities,
    decimal Subtotal,
    IReadOnlyList<CartLineDto> Lines);

/// <summary>
/// The cart as shown: grouped by store, prices and stock read now. Totals count only the lines that can
/// be ordered; <c>CanCheckout</c> is false while a line has an issue or an invalid code.
/// </summary>
public sealed record CartDto(
    IReadOnlyList<CartStoreGroup> Stores,
    int ItemCount,
    decimal ItemsTotal,
    decimal DeliveryTotal,
    decimal Total,
    bool CanCheckout);

// -- What the catalogue tells the cart about its products ------------------------------

public sealed record CartStoreInfo(Guid Id, string Name, string Slug, string? LogoUrl, bool IsOpen, decimal DeliveryFee, IReadOnlyList<string> DeliveryCities);

public sealed record CartVariantInfo(Guid Id, decimal Price, int Stock, IReadOnlyList<CartOptionLabel> Options);

/// <summary>A product as the cart needs it. <c>Visible</c> = can be ordered from the shop (same rule as the shop).</summary>
public sealed record CartProductInfo(
    Guid Id,
    string Name,
    string Slug,
    string? ImageUrl,
    decimal Price,
    int Stock,
    bool HasVariants,
    bool Visible,
    CartStoreInfo Store,
    IReadOnlyList<CartVariantInfo> Variants);
