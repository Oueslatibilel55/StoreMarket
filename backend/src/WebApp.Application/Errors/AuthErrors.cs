namespace WebApp.Application.Errors;

/// <summary>Error codes for auth use cases — translated by the client.</summary>
public static class AuthErrors
{
    public const string EmailTaken = "auth.emailTaken";
    public const string InvalidCredentials = "auth.invalidCredentials";
    public const string AccountBlocked = "auth.accountBlocked";
    public const string InvalidRefreshToken = "auth.invalidRefreshToken";
    public const string UserNotFound = "auth.userNotFound";
}

/// <summary>Error codes for admin user management.</summary>
public static class AdminUserErrors
{
    public const string CannotChangeOwnAccount = "admin.cannotChangeOwnAccount";
    public const string UnknownRole = "admin.unknownRole";
}

/// <summary>Error codes for role requests (user asks, admin decides).</summary>
public static class RoleRequestErrors
{
    public const string NotFound = "roleRequest.notFound";
    public const string AlreadyHasRole = "roleRequest.alreadyHasRole";
    public const string AlreadyPending = "roleRequest.alreadyPending";
}

/// <summary>Error codes for store category management.</summary>
public static class StoreCategoryErrors
{
    public const string NotFound = "storeCategory.notFound";
    public const string InUse = "storeCategory.inUse";
    public const string SlugTaken = "storeCategory.slugTaken";
    public const string NameTaken = "storeCategory.nameTaken";
    public const string DirectionInvalid = "storeCategory.directionInvalid";
}

/// <summary>Error codes for image upload.</summary>
public static class ImageErrors
{
    public const string NotFound = "image.notFound";
    public const string PurposeNotAllowed = "image.purposeNotAllowed";
    public const string WrongPurpose = "image.wrongPurpose";
    public const string InUse = "image.inUse";
    public const string NotYours = "image.notYours";
}

/// <summary>Error codes for stores.</summary>
public static class StoreErrors
{
    public const string NotFound = "store.notFound";
    public const string AlreadyExists = "store.alreadyExists";
    public const string SlugTaken = "store.slugTaken";
    public const string CategoryUnavailable = "store.categoryUnavailable";
    public const string OwnerNotStoreOwner = "store.ownerNotStoreOwner";
}

/// <summary>Error codes for store / product moderation (010).</summary>
public static class ModerationErrors
{
    public const string SelectionInvalid = "moderation.selectionInvalid";
}

/// <summary>Error codes for the product category tree.</summary>
public static class ProductCategoryErrors
{
    public const string NotFound = "productCategory.notFound";
    public const string ParentNotFound = "productCategory.parentNotFound";
    public const string TooDeep = "productCategory.tooDeep";
    public const string Cycle = "productCategory.cycle";
    public const string SlugTaken = "productCategory.slugTaken";
    public const string HasChildren = "productCategory.hasChildren";
    public const string KeyUsedInBranch = "productCategory.keyUsedInBranch";
    public const string DirectionInvalid = "productCategory.directionInvalid";
    public const string HasProducts = "productCategory.hasProducts";
    public const string AttributeInUse = "productCategory.attributeInUse";
    public const string OptionInUse = "productCategory.optionInUse";
}

/// <summary>Error codes for products.</summary>
public static class ProductErrors
{
    public const string NotFound = "product.notFound";
    public const string SlugTaken = "product.slugTaken";
    public const string CategoryUnavailable = "product.categoryUnavailable";
}

/// <summary>Error codes for affiliate ↔ store partnerships (012).</summary>
public static class PartnershipErrors
{
    public const string NotFound = "partnership.notFound";
    public const string AlreadyOpen = "partnership.alreadyOpen";
    public const string OwnStore = "partnership.ownStore";
    public const string StoreUnavailable = "partnership.storeUnavailable";
    public const string AffiliateInactive = "partnership.affiliateInactive";
}

/// <summary>Why a code is refused on a product (012) — each case has its own message on the client.</summary>
public static class AffiliateCodeErrors
{
    public const string Invalid = "affiliateCode.invalid";
    public const string Unknown = "affiliateCode.unknown";
    public const string Inactive = "affiliateCode.inactive";
    public const string NotPartner = "affiliateCode.notPartner";
    public const string OwnStore = "affiliateCode.ownStore";
    public const string OwnCode = "affiliateCode.ownCode";
    public const string ProductUnavailable = "affiliateCode.productUnavailable";
}

/// <summary>Error codes for the cart (013).</summary>
public static class CartErrors
{
    public const string ProductUnavailable = "cart.productUnavailable";
    public const string StoreClosed = "cart.storeClosed";
    public const string VariantRequired = "cart.variantRequired";
    public const string VariantUnavailable = "cart.variantUnavailable";
    public const string NotEnoughStock = "cart.notEnoughStock";
}

/// <summary>Error codes for checkout and orders (014).</summary>
public static class CheckoutErrors
{
    public const string CartEmpty = "checkout.cartEmpty";
    /// <summary>A line can't be ordered any more (stock, availability, variant…): the cart shows which.</summary>
    public const string CartNotReady = "checkout.cartNotReady";
    /// <summary>A line's affiliate code is refused now (partnership ended…): the cart shows which.</summary>
    public const string CodeRefused = "checkout.codeRefused";
    public const string CityNotDelivered = "checkout.cityNotDelivered";
    public const string NotAffiliate = "checkout.notAffiliate";
    public const string OrderNotFound = "order.notFound";
}

/// <summary>Error codes for the order lifecycle (016).</summary>
public static class OrderErrors
{
    /// <summary>Someone changed this order at the same time: reload it.</summary>
    public const string Changed = "order.changed";
}

/// <summary>Error codes for the coin ledger (019).</summary>
public static class CoinErrors
{
    public const string AffiliateNotFound = "coins.affiliateNotFound";
    public const string EntryNotFound = "coins.entryNotFound";
}

/// <summary>Error codes for payout requests (021).</summary>
public static class PayoutErrors
{
    public const string NotFound = "payout.notFound";
    /// <summary>Only an active affiliate account can ask for a payout.</summary>
    public const string NotAffiliate = "payout.notAffiliate";
    public const string BelowMinimum = "payout.belowMinimum";
    public const string NotEnoughCoins = "payout.notEnoughCoins";
    /// <summary>One open request at a time.</summary>
    public const string AlreadyOpen = "payout.alreadyOpen";
    /// <summary>Someone changed this request at the same time: reload it.</summary>
    public const string Changed = "payout.changed";
}

/// <summary>Error codes for ratings and reviews (017).</summary>
public static class ReviewErrors
{
    public const string NotFound = "review.notFound";
    /// <summary>Not one of the user's orders (or no such line) — reported as missing.</summary>
    public const string OrderNotFound = "review.orderNotFound";
    /// <summary>Only a delivered parcel can be rated.</summary>
    public const string NotDelivered = "review.notDelivered";
    public const string AlreadyReviewed = "review.alreadyReviewed";
    /// <summary>A store owner never rates their own store or products.</summary>
    public const string OwnStore = "review.ownStore";
}

/// <summary>Error codes for store billing (025).</summary>
public static class BillingErrors
{
    public const string OfferNotFound = "billing.offerNotFound";
    public const string InvoiceNotFound = "billing.invoiceNotFound";
    public const string PaymentNotFound = "billing.paymentNotFound";
    /// <summary>A payment of the same thing is already waiting for an admin.</summary>
    public const string AlreadyDeclared = "billing.alreadyDeclared";
    public const string Changed = "billing.changed";
}
