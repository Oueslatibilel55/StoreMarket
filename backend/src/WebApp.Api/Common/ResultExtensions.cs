using WebApp.Application.Errors;
using WebApp.Domain.Common;

namespace WebApp.Api.Common;

internal static class ResultExtensions
{
    /// <summary>
    /// Turns a failed <see cref="Result"/> into RFC 9457 ProblemDetails. The error code goes in
    /// the `code` extension so the client can translate it (FR / AR / EN).
    /// </summary>
    public static IResult ToProblem(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var code = result.Error ?? "error.unknown";

        var status = code switch
        {
            AuthErrors.EmailTaken => StatusCodes.Status409Conflict,
            AuthErrors.InvalidCredentials or AuthErrors.InvalidRefreshToken => StatusCodes.Status401Unauthorized,
            AuthErrors.AccountBlocked or DomainErrors.User.Blocked => StatusCodes.Status403Forbidden,
            AuthErrors.UserNotFound or RoleRequestErrors.NotFound or DomainErrors.Setting.Unknown
                or StoreCategoryErrors.NotFound or ImageErrors.NotFound or StoreErrors.NotFound
                or ProductCategoryErrors.NotFound or DomainErrors.ProductCategory.AttributeNotFound or ProductErrors.NotFound
                => StatusCodes.Status404NotFound,
            ProductCategoryErrors.SlugTaken or ProductCategoryErrors.HasChildren or ProductCategoryErrors.KeyUsedInBranch
                or DomainErrors.ProductCategory.AttributeKeyTaken or ProductCategoryErrors.HasProducts or ProductCategoryErrors.AttributeInUse
                or ProductCategoryErrors.OptionInUse or ProductErrors.SlugTaken => StatusCodes.Status409Conflict,
            ImageErrors.PurposeNotAllowed => StatusCodes.Status403Forbidden,
            ImageErrors.InUse or StoreCategoryErrors.InUse or StoreErrors.AlreadyExists or StoreErrors.SlugTaken
                or DomainErrors.Store.NotOwnerControlled or DomainErrors.Store.Deleted or DomainErrors.Product.Deleted
                or StoreErrors.OwnerNotStoreOwner => StatusCodes.Status409Conflict,
            DomainErrors.Image.TooLarge => StatusCodes.Status413PayloadTooLarge,
            StoreCategoryErrors.SlugTaken or StoreCategoryErrors.NameTaken => StatusCodes.Status409Conflict,
            AdminUserErrors.CannotChangeOwnAccount or DomainErrors.User.CannotBlockAdmin => StatusCodes.Status409Conflict,
            RoleRequestErrors.AlreadyHasRole or RoleRequestErrors.AlreadyPending
                or DomainErrors.RoleRequest.NotPending => StatusCodes.Status409Conflict,
            PartnershipErrors.NotFound or PartnershipErrors.StoreUnavailable => StatusCodes.Status404NotFound,
            PartnershipErrors.AlreadyOpen or PartnershipErrors.OwnStore or DomainErrors.Partnership.NotRequested
                or DomainErrors.Partnership.NotAccepted or DomainErrors.User.NotAffiliate => StatusCodes.Status409Conflict,
            PartnershipErrors.AffiliateInactive => StatusCodes.Status403Forbidden,
            DomainErrors.Cart.LineNotFound => StatusCodes.Status404NotFound,
            CheckoutErrors.OrderNotFound => StatusCodes.Status404NotFound,
            CheckoutErrors.CartNotReady or CheckoutErrors.CodeRefused or CheckoutErrors.CityNotDelivered
                or CheckoutErrors.CartEmpty => StatusCodes.Status409Conflict,
            CheckoutErrors.NotAffiliate => StatusCodes.Status403Forbidden,
            DomainErrors.Order.StoreOrderNotFound => StatusCodes.Status404NotFound,
            CoinErrors.AffiliateNotFound or CoinErrors.EntryNotFound => StatusCodes.Status404NotFound,
            PayoutErrors.NotFound => StatusCodes.Status404NotFound,
            PayoutErrors.NotAffiliate => StatusCodes.Status403Forbidden,
            PayoutErrors.AlreadyOpen or PayoutErrors.Changed or PayoutErrors.NotEnoughCoins
                or DomainErrors.Payout.TransitionNotAllowed => StatusCodes.Status409Conflict,
            DomainErrors.Order.TransitionNotAllowed or DomainErrors.Order.DeliveredIsFinal or OrderErrors.Changed => StatusCodes.Status409Conflict,
            CartErrors.ProductUnavailable or CartErrors.StoreClosed or CartErrors.NotEnoughStock
                or CartErrors.VariantUnavailable or DomainErrors.Cart.TooManyLines => StatusCodes.Status409Conflict,
            ReviewErrors.NotFound or ReviewErrors.OrderNotFound => StatusCodes.Status404NotFound,
            ReviewErrors.NotDelivered or ReviewErrors.AlreadyReviewed or ReviewErrors.OwnStore or DomainErrors.Review.EditWindowClosed
                or DomainErrors.Review.Hidden or DomainErrors.Review.AlreadyHidden or DomainErrors.Review.NotHidden => StatusCodes.Status409Conflict,
            BillingErrors.OfferNotFound or BillingErrors.InvoiceNotFound or BillingErrors.PaymentNotFound => StatusCodes.Status404NotFound,
            BillingErrors.AlreadyDeclared or BillingErrors.Changed or DomainErrors.Billing.NotDeclared or DomainErrors.Billing.InvoicePaid
                or DomainErrors.Billing.AmountTooHigh or DomainErrors.Billing.OfferNotApplicable => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return TypedResults.Problem(
            statusCode: status,
            title: code,
            extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
