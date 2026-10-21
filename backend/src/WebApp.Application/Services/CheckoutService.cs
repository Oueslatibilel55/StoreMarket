using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Settings;

namespace WebApp.Application.Services;

/// <summary>
/// Checkout (014): turns a cart into a cash-on-delivery order split per store. Everything the cart showed is
/// checked again here — availability, variants, stock, delivery zones and every affiliate code — and the stock
/// is taken in the same transaction as the order, so a parcel is never sold twice.
/// </summary>
public sealed class CheckoutService(
    ICartRepository carts,
    ICatalogQueries catalog,
    IProductRepository products,
    IOrderRepository orders,
    IOrderQueries orderQueries,
    IUserRepository users,
    AffiliateCodeService codes,
    IPlatformSettings settings,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    private sealed record Item(Guid ProductId, Guid? VariantId, int Quantity, string? AffiliateCode);

    /// <param name="userId">The signed-in user, or null for a guest checkout.</param>
    public async Task<Result<OrderDto>> PlaceAsync(Guid? userId, CheckoutRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Contact);

        // Affiliate mode: the signed-in affiliate orders for a client.
        User? affiliate = null;
        if (request.ForClient)
        {
            affiliate = userId is { } id ? await users.GetByIdAsync(id, ct).ConfigureAwait(false) : null;
            if (affiliate is not { CanEarnAsAffiliate: true }) return Result.Fail<OrderDto>(CheckoutErrors.NotAffiliate);
        }

        var cart = userId is { } uid ? await carts.GetByUserAsync(uid, ct).ConfigureAwait(false) : null;
        List<Item> items = cart is not null
            ? [.. cart.Lines.OrderBy(l => l.CreatedAt).Select(l => new Item(l.ProductId, l.VariantId, l.Quantity, l.AffiliateCode))]
            : userId is null
                ? [.. (request.Lines ?? []).Where(l => l.Quantity > 0).Take(Cart.MaxLines)
                    .Select(l => new Item(l.ProductId, l.VariantId, Math.Min(l.Quantity, Cart.MaxQuantity), l.AffiliateCode))]
                : [];
        if (items.Count == 0) return Result.Fail<OrderDto>(CheckoutErrors.CartEmpty);

        var catalogue = (await catalog.GetCartProductsAsync([.. items.Select(i => i.ProductId).Distinct()], ct).ConfigureAwait(false))
            .ToDictionary(p => p.Id);

        // Same rules as the cart: any line it would flag stops the checkout (the client sees why in the cart).
        var drafts = new List<OrderLineDraft>();
        foreach (var item in items)
        {
            if (!catalogue.TryGetValue(item.ProductId, out var product)) return Result.Fail<OrderDto>(CheckoutErrors.CartNotReady);
            var variant = item.VariantId is { } vid ? product.Variants.FirstOrDefault(v => v.Id == vid) : null;
            var available = product.HasVariants ? variant?.Stock ?? 0 : product.Stock;
            if (CartRules.IssueOf(product, item.VariantId, variant, available, item.Quantity) is not null)
                return Result.Fail<OrderDto>(CheckoutErrors.CartNotReady);
            if (!product.Store.DeliveryCities.Contains(request.Contact.City))
                return Result.Fail<OrderDto>(CheckoutErrors.CityNotDelivered);

            var line = await AffiliateOfAsync(item, product, userId, affiliate, ct).ConfigureAwait(false);
            if (!line.IsSuccess) return Result.Fail<OrderDto>(line.Error!);

            drafts.Add(new OrderLineDraft(
                product.Store.Id, product.Store.Name, product.Store.DeliveryFee, product.Id, item.VariantId, product.Name, product.Slug,
                [.. (variant?.Options ?? []).Select(o => new OrderOption(o.Axis.Fr, o.Axis.Ar, o.Axis.En, o.Value.Fr, o.Value.Ar, o.Value.En))],
                variant?.Price ?? product.Price, item.Quantity, line.Value));
        }

        var coinValue = await settings.GetDecimalAsync(SettingKeys.CoinValueInTnd, ct).ConfigureAwait(false);

        var placed = await unitOfWork.InTransactionAsync(async () =>
        {
            foreach (var item in items)
            {
                if (!await products.TryTakeStockAsync(item.ProductId, item.VariantId, item.Quantity, ct).ConfigureAwait(false))
                    return Result.Fail<Order>(CartErrors.NotEnoughStock);
            }

            var number = await orders.NextNumberAsync(ct).ConfigureAwait(false);
            var order = Order.Place(number, clock.GetUtcNow().UtcDateTime,
                clientId: affiliate is null ? userId : null, placedByAffiliateId: affiliate?.Id,
                new OrderContact(request.Contact.FullName, request.Contact.Phone, request.Contact.City, request.Contact.Address, request.Contact.Note),
                drafts, coinValue);
            if (!order.IsSuccess) return order;

            orders.Add(order.Value);
            cart?.Clear();
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            return order;
        }, ct).ConfigureAwait(false);
        if (!placed.IsSuccess) return Result.Fail<OrderDto>(placed.Error!);

        var dto = await orderQueries.GetForUserAsync(userId ?? Guid.Empty, placed.Value.Reference, ct).ConfigureAwait(false)
            ?? await orderQueries.GetByReferenceAndPhoneAsync(placed.Value.Reference, placed.Value.ClientPhone, ct).ConfigureAwait(false);
        return Result.Ok(dto!);
    }

    public Task<PagedResult<OrderListItem>> ListMineAsync(Guid userId, OrderListQuery query, CancellationToken ct = default) =>
        orderQueries.ListForUserAsync(userId, query, ct);

    public async Task<Result<OrderDto>> GetMineAsync(Guid userId, string reference, CancellationToken ct = default) =>
        await orderQueries.GetForUserAsync(userId, reference, ct).ConfigureAwait(false) is { } order
            ? Result.Ok(order)
            : Result.Fail<OrderDto>(CheckoutErrors.OrderNotFound);

    /// <summary>A guest finds their order: reference + the phone given at checkout (both must match).</summary>
    public async Task<Result<OrderDto>> LookupAsync(string? reference, string? phone, CancellationToken ct = default)
    {
        var normalized = User.NormalizePhone(phone);
        var order = normalized is null || string.IsNullOrWhiteSpace(reference)
            ? null
            : await orderQueries.GetByReferenceAndPhoneAsync(reference.Trim().ToUpperInvariant(), normalized, ct).ConfigureAwait(false);
        return order is null ? Result.Fail<OrderDto>(CheckoutErrors.OrderNotFound) : Result.Ok(order);
    }

    public Task<AffiliateOrdersDto> ListForAffiliateAsync(Guid affiliateId, OrderListQuery query, CancellationToken ct = default) =>
        orderQueries.ListForAffiliateAsync(affiliateId, query, ct);

    /// <summary>
    /// The affiliate a line pays. Normal mode: the line's code, checked again now — a refused code stops the checkout.
    /// Affiliate mode: the affiliate's own code on the stores they work with, nothing elsewhere (other codes ignored).
    /// </summary>
    private async Task<Result<OrderAffiliate?>> AffiliateOfAsync(Item item, CartProductInfo product, Guid? userId, User? affiliate, CancellationToken ct)
    {
        if (affiliate is not null)
        {
            var own = await codes.MatchAsync(affiliate.AffiliateCode, product.Id, clientId: null, ct).ConfigureAwait(false);
            return Result.Ok(own.IsSuccess ? ToAffiliate(own.Value) : null);
        }
        if (item.AffiliateCode is null) return Result.Ok<OrderAffiliate?>(null);

        var match = await codes.MatchAsync(item.AffiliateCode, product.Id, userId, ct).ConfigureAwait(false);
        return match.IsSuccess ? Result.Ok(ToAffiliate(match.Value)) : Result.Fail<OrderAffiliate?>(CheckoutErrors.CodeRefused);
    }

    private static OrderAffiliate? ToAffiliate(AffiliateCodeMatch m) => new(m.AffiliateId, m.PartnershipId, m.Code, m.AffiliatePercent);
}
