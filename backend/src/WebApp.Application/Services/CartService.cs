using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;

namespace WebApp.Application.Services;

/// <summary>
/// The cart (013). Signed-in users keep it on the server; guests keep it in the browser and get the same
/// view through <see cref="PreviewAsync"/>, then merge it at sign-in. Every read re-checks prices, stock,
/// availability and each line's affiliate code (012) — checkout (014) checks them all again.
/// </summary>
public sealed class CartService(
    ICartRepository carts,
    ICatalogQueries catalog,
    AffiliateCodeService codes,
    IUnitOfWork unitOfWork)
{
    private sealed record Item(Guid? Id, Guid ProductId, Guid? VariantId, int Quantity, string? AffiliateCode);

    public async Task<CartDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var cart = await carts.GetByUserAsync(userId, ct).ConfigureAwait(false);
        return await BuildAsync(Items(cart), userId, ct).ConfigureAwait(false);
    }

    /// <summary>A guest's browser cart, shown like a server one. Unknown products are dropped.</summary>
    public Task<CartDto> PreviewAsync(GuestCartRequest request, Guid? clientId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var items = (request.Lines ?? [])
            .Where(l => l.Quantity > 0)
            .GroupBy(l => (l.ProductId, l.VariantId))
            .Take(Cart.MaxLines)
            .Select(g => new Item(null, g.Key.ProductId, g.Key.VariantId, Math.Min(g.Sum(l => l.Quantity), Cart.MaxQuantity),
                AffiliateCode.Normalize(g.Select(l => l.AffiliateCode).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)))))
            .ToList();
        return BuildAsync(items, clientId, ct);
    }

    public async Task<Result<CartDto>> AddAsync(Guid userId, AddCartLineRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = await ProductAsync(request.ProductId, ct).ConfigureAwait(false);
        var orderable = CartRules.Orderable(product, request.VariantId);
        if (!orderable.IsSuccess) return Result.Fail<CartDto>(orderable.Error!);

        var cart = await GetOrCreateAsync(userId, ct).ConfigureAwait(false);
        var already = cart.Find(request.ProductId, request.VariantId)?.Quantity ?? 0;
        if (request.Quantity > 0 && already + request.Quantity > orderable.Value)
            return Result.Fail<CartDto>(CartErrors.NotEnoughStock);

        var added = cart.Add(request.ProductId, request.VariantId, request.Quantity, request.AffiliateCode);
        if (!added.IsSuccess) return Result.Fail<CartDto>(added.Error!);
        return await SaveAsync(cart, userId, ct).ConfigureAwait(false);
    }

    /// <summary>A quantity can always go down; going up needs the stock.</summary>
    public async Task<Result<CartDto>> UpdateQuantityAsync(Guid userId, Guid lineId, UpdateCartLineRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cart = await carts.GetByUserAsync(userId, ct).ConfigureAwait(false);
        var line = cart?.Lines.FirstOrDefault(l => l.Id == lineId);
        if (cart is null || line is null) return Result.Fail<CartDto>(DomainErrors.Cart.LineNotFound);

        if (request.Quantity > line.Quantity)
        {
            var product = await ProductAsync(line.ProductId, ct).ConfigureAwait(false);
            var orderable = CartRules.Orderable(product, line.VariantId);
            if (!orderable.IsSuccess) return Result.Fail<CartDto>(orderable.Error!);
            if (request.Quantity > orderable.Value) return Result.Fail<CartDto>(CartErrors.NotEnoughStock);
        }

        var set = cart.SetQuantity(lineId, request.Quantity);
        return set.IsSuccess ? await SaveAsync(cart, userId, ct).ConfigureAwait(false) : Result.Fail<CartDto>(set.Error!);
    }

    /// <summary>Stores the code (format checked); whether it counts on that product shows on the line.</summary>
    public Task<Result<CartDto>> SetCodeAsync(Guid userId, Guid lineId, SetCartCodeRequest request, CancellationToken ct = default) =>
        ChangeAsync(userId, cart => cart.SetCode(lineId, request?.Code), ct);

    public Task<Result<CartDto>> RemoveAsync(Guid userId, Guid lineId, CancellationToken ct = default) =>
        ChangeAsync(userId, cart => cart.Remove(lineId), ct);

    public async Task<CartDto> ClearAsync(Guid userId, CancellationToken ct = default)
    {
        var cart = await carts.GetByUserAsync(userId, ct).ConfigureAwait(false);
        if (cart is not null)
        {
            cart.Clear();
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        }
        return await BuildAsync([], userId, ct).ConfigureAwait(false);
    }

    /// <summary>At sign-in: the guest's browser cart joins the account's cart. Unknown products are ignored.</summary>
    public async Task<CartDto> MergeAsync(Guid userId, GuestCartRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lines = request.Lines ?? [];
        if (lines.Count == 0) return await GetAsync(userId, ct).ConfigureAwait(false);

        var known = (await catalog.GetCartProductsAsync([.. lines.Select(l => l.ProductId).Distinct()], ct).ConfigureAwait(false))
            .Select(p => p.Id).ToHashSet();
        var cart = await GetOrCreateAsync(userId, ct).ConfigureAwait(false);
        cart.Merge(lines.Where(l => known.Contains(l.ProductId)).Select(l => (l.ProductId, l.VariantId, l.Quantity, l.AffiliateCode)));
        return (await SaveAsync(cart, userId, ct).ConfigureAwait(false)).Value;
    }

    // -- Helpers ------------------------------------------------------------------------

    private async Task<Result<CartDto>> ChangeAsync(Guid userId, Func<Cart, Result> change, CancellationToken ct)
    {
        var cart = await carts.GetByUserAsync(userId, ct).ConfigureAwait(false);
        if (cart is null) return Result.Fail<CartDto>(DomainErrors.Cart.LineNotFound);
        var changed = change(cart);
        return changed.IsSuccess ? await SaveAsync(cart, userId, ct).ConfigureAwait(false) : Result.Fail<CartDto>(changed.Error!);
    }

    private async Task<CartProductInfo?> ProductAsync(Guid productId, CancellationToken ct) =>
        await catalog.GetCartProductsAsync([productId], ct).ConfigureAwait(false) is [var product, ..] ? product : null;

    private async Task<Cart> GetOrCreateAsync(Guid userId, CancellationToken ct)
    {
        var cart = await carts.GetByUserAsync(userId, ct).ConfigureAwait(false);
        if (cart is not null) return cart;
        cart = Cart.For(userId);
        carts.Add(cart);
        return cart;
    }

    private async Task<Result<CartDto>> SaveAsync(Cart cart, Guid userId, CancellationToken ct)
    {
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(await BuildAsync(Items(cart), userId, ct).ConfigureAwait(false));
    }

    private static List<Item> Items(Cart? cart) =>
        cart is null ? [] : [.. cart.Lines.OrderBy(l => l.CreatedAt).Select(l => new Item(l.Id, l.ProductId, l.VariantId, l.Quantity, l.AffiliateCode))];

    private async Task<CartDto> BuildAsync(IReadOnlyList<Item> items, Guid? clientId, CancellationToken ct)
    {
        if (items.Count == 0) return new CartDto([], 0, 0, 0, 0, false);

        var products = (await catalog.GetCartProductsAsync([.. items.Select(i => i.ProductId).Distinct()], ct).ConfigureAwait(false))
            .ToDictionary(p => p.Id);

        var lines = new List<(CartStoreInfo Store, CartLineDto Line)>();
        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product)) continue;

            var variant = item.VariantId is { } vid ? product.Variants.FirstOrDefault(v => v.Id == vid) : null;
            var unitPrice = variant?.Price ?? product.Price;
            var available = product.HasVariants ? variant?.Stock ?? 0 : product.Stock;
            var issue = CartRules.IssueOf(product, item.VariantId, variant, available, item.Quantity);

            CartCodeStatus? code = null;
            if (item.AffiliateCode is { } c && issue != CartIssues.ProductUnavailable)
            {
                var check = await codes.ValidateAsync(c, product.Id, clientId, ct).ConfigureAwait(false);
                code = check.IsSuccess
                    ? new CartCodeStatus(true, check.Value.AffiliateName, check.Value.AffiliatePercent, null)
                    : new CartCodeStatus(false, null, null, check.Error);
            }

            lines.Add((product.Store, new CartLineDto(
                item.Id, product.Id, item.VariantId, product.Name, product.Slug, product.ImageUrl, variant?.Options ?? [],
                unitPrice, item.Quantity, unitPrice * item.Quantity, available, issue, item.AffiliateCode, code)));
        }

        var groups = lines
            .GroupBy(l => l.Store.Id)
            .Select(g =>
            {
                var store = g.First().Store;
                var subtotal = g.Where(l => l.Line.Issue is null).Sum(l => l.Line.LineTotal);
                return new CartStoreGroup(store.Id, store.Name, store.Slug, store.LogoUrl, store.IsOpen, store.DeliveryFee,
                    store.DeliveryCities, subtotal, [.. g.Select(l => l.Line)]);
            })
            .ToList();

        var itemsTotal = groups.Sum(g => g.Subtotal);
        // Delivery is paid per store that has something to deliver.
        var deliveryTotal = groups.Where(g => g.Lines.Any(l => l.Issue is null)).Sum(g => g.DeliveryFee);
        var all = groups.SelectMany(g => g.Lines).ToList();
        var canCheckout = all.Count > 0 && all.All(l => l.Issue is null && l.Code is not { Valid: false });

        return new CartDto(groups, all.Sum(l => l.Quantity), itemsTotal, deliveryTotal, itemsTotal + deliveryTotal, canCheckout);
    }
}

/// <summary>What makes a cart line orderable — shared by the cart (013) and checkout (014), so both apply the same rules.</summary>
public static class CartRules
{
    /// <summary>The units that can be put in the cart for this product / variant, or why none can.</summary>
    public static Result<int> Orderable(CartProductInfo? product, Guid? variantId)
    {
        if (product is null || !product.Visible) return Result.Fail<int>(CartErrors.ProductUnavailable);
        if (!product.Store.IsOpen) return Result.Fail<int>(CartErrors.StoreClosed);
        if (product.HasVariants && variantId is null) return Result.Fail<int>(CartErrors.VariantRequired);
        if (!product.HasVariants) return variantId is null ? Result.Ok(product.Stock) : Result.Fail<int>(CartErrors.VariantUnavailable);
        var variant = product.Variants.FirstOrDefault(v => v.Id == variantId);
        return variant is null ? Result.Fail<int>(CartErrors.VariantUnavailable) : Result.Ok(variant.Stock);
    }

    /// <summary>Why the line can't be ordered right now (<see cref="CartIssues"/>), or null.</summary>
    public static string? IssueOf(CartProductInfo product, Guid? variantId, CartVariantInfo? variant, int available, int quantity)
    {
        if (!product.Visible) return CartIssues.ProductUnavailable;
        if (!product.Store.IsOpen) return CartIssues.StoreClosed;
        if (product.HasVariants && variantId is null) return CartIssues.VariantRequired;
        if ((product.HasVariants && variant is null) || (!product.HasVariants && variantId is not null)) return CartIssues.VariantUnavailable;
        if (available <= 0) return CartIssues.OutOfStock;
        return quantity > available ? CartIssues.NotEnoughStock : null;
    }
}
