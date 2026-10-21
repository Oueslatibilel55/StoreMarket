using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;

namespace WebApp.Application.Services;

/// <summary>
/// Whether an affiliate code may be used on a product (012). The cart (013) checks it when the code is
/// typed and checkout (014) checks it again. A code is valid only when the affiliate can earn
/// (active account, Affiliate role, code not deactivated), works with the product's store (accepted
/// partnership), and is neither the store owner nor the client.
/// </summary>
public sealed class AffiliateCodeService(
    IUserRepository users,
    IPartnershipRepository partnerships,
    ICatalogQueries catalog)
{
    /// <param name="clientId">The signed-in client, if any: an affiliate never earns on their own purchase.</param>
    public async Task<Result<AffiliateCodeCheckDto>> ValidateAsync(string? code, Guid productId, Guid? clientId, CancellationToken ct = default)
    {
        var match = await MatchAsync(code, productId, clientId, ct).ConfigureAwait(false);
        return match.IsSuccess
            ? Result.Ok(new AffiliateCodeCheckDto(match.Value.Code, match.Value.AffiliateName, match.Value.StoreId, match.Value.AffiliatePercent))
            : Result.Fail<AffiliateCodeCheckDto>(match.Error!);
    }

    /// <summary>Same checks, with the ids checkout (014) freezes on the order line.</summary>
    public async Task<Result<AffiliateCodeMatch>> MatchAsync(string? code, Guid productId, Guid? clientId, CancellationToken ct = default)
    {
        var normalized = AffiliateCode.Normalize(code);
        if (normalized is null) return Result.Fail<AffiliateCodeMatch>(AffiliateCodeErrors.Invalid);

        var product = await catalog.GetVisibleProductStoreAsync(productId, ct).ConfigureAwait(false);
        if (product is null) return Result.Fail<AffiliateCodeMatch>(AffiliateCodeErrors.ProductUnavailable);

        var affiliate = await users.GetByAffiliateCodeAsync(normalized, ct).ConfigureAwait(false);
        if (affiliate is null) return Result.Fail<AffiliateCodeMatch>(AffiliateCodeErrors.Unknown);
        if (!affiliate.CanEarnAsAffiliate) return Result.Fail<AffiliateCodeMatch>(AffiliateCodeErrors.Inactive);
        if (affiliate.Id == clientId) return Result.Fail<AffiliateCodeMatch>(AffiliateCodeErrors.OwnCode);
        if (affiliate.Id == product.OwnerId) return Result.Fail<AffiliateCodeMatch>(AffiliateCodeErrors.OwnStore);
        if (await partnerships.GetActiveIdAsync(affiliate.Id, product.StoreId, ct).ConfigureAwait(false) is not { } partnershipId)
            return Result.Fail<AffiliateCodeMatch>(AffiliateCodeErrors.NotPartner);

        return Result.Ok(new AffiliateCodeMatch(normalized, affiliate.Id, partnershipId, ShortName(affiliate.FullName), product.StoreId, product.AffiliatePercent));
    }

    /// <summary>"Sana Trabelsi" → "Sana T." — enough for the client to recognise who recommended the product.</summary>
    public static string ShortName(string fullName)
    {
        var parts = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => string.Empty,
            1 => parts[0],
            _ => $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}.",
        };
    }
}
