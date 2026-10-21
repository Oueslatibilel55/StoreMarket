namespace WebApp.Domain.Common;

/// <summary>
/// Error codes returned in <see cref="Result.Error"/>. They are stable identifiers, not
/// messages: the API sends them to the client, which translates them (FR / AR / EN).
/// </summary>
public static class DomainErrors
{
    public static class User
    {
        public const string EmailInvalid = "user.emailInvalid";
        public const string PasswordTooWeak = "user.passwordTooWeak";
        public const string FullNameRequired = "user.fullNameRequired";
        public const string PhoneInvalid = "user.phoneInvalid";
        public const string Blocked = "user.blocked";
        public const string AffiliateTermsRequired = "user.affiliateTermsRequired";
        public const string AffiliateCodeInvalid = "user.affiliateCodeInvalid";
        public const string BaseRoleRequired = "user.baseRoleRequired";
        public const string UseAffiliateActivation = "user.useAffiliateActivation";
        public const string AccountTypeInvalid = "user.accountTypeInvalid";
        public const string CannotBlockAdmin = "user.cannotBlockAdmin";
        public const string BlockReasonRequired = "user.blockReasonRequired";
        public const string AffiliateCodeReasonRequired = "user.affiliateCodeReasonRequired";
        public const string NotAffiliate = "user.notAffiliate";
    }

    public static class Order
    {
        public const string NameRequired = "order.nameRequired";
        public const string PhoneInvalid = "order.phoneInvalid";
        public const string CityInvalid = "order.cityInvalid";
        public const string AddressRequired = "order.addressRequired";
        public const string NoteTooLong = "order.noteTooLong";
        public const string Empty = "order.empty";
        public const string LineInvalid = "order.lineInvalid";
        public const string StoreOrderNotFound = "order.storeOrderNotFound";
        public const string TransitionNotAllowed = "order.transitionNotAllowed";
        public const string ReasonRequired = "order.reasonRequired";
        public const string ReasonTooLong = "order.reasonTooLong";
        public const string DeliveredIsFinal = "order.deliveredIsFinal";
    }

    public static class Coins
    {
        public const string ReasonRequired = "coins.reasonRequired";
        public const string ReasonTooLong = "coins.reasonTooLong";
        public const string AmountInvalid = "coins.amountInvalid";
    }

    public static class Payout
    {
        public const string CoinsInvalid = "payout.coinsInvalid";
        public const string MethodInvalid = "payout.methodInvalid";
        public const string NoteTooLong = "payout.noteTooLong";
        public const string BankDetailsRequired = "payout.bankDetailsRequired";
        public const string BankDetailsTooLong = "payout.bankDetailsTooLong";
        public const string RibInvalid = "payout.ribInvalid";
        public const string TransitionNotAllowed = "payout.transitionNotAllowed";
        public const string ReasonRequired = "payout.reasonRequired";
        public const string ReasonTooLong = "payout.reasonTooLong";
        public const string DateRequired = "payout.dateRequired";
        public const string DateInPast = "payout.dateInPast";
        public const string PlaceRequired = "payout.placeRequired";
        public const string PlaceTooLong = "payout.placeTooLong";
        public const string PaidReferenceRequired = "payout.paidReferenceRequired";
        public const string PaidReferenceTooLong = "payout.paidReferenceTooLong";
        public const string PaidInFuture = "payout.paidInFuture";
    }

    public static class Cart
    {
        public const string QuantityInvalid = "cart.quantityInvalid";
        public const string TooManyLines = "cart.tooManyLines";
        public const string LineNotFound = "cart.lineNotFound";
        public const string CodeInvalid = "cart.codeInvalid";
    }

    public static class Partnership
    {
        public const string MessageTooLong = "partnership.messageTooLong";
        public const string ReasonTooLong = "partnership.reasonTooLong";
        public const string NotRequested = "partnership.notRequested";
        public const string NotAccepted = "partnership.notAccepted";
    }

    public static class RoleRequest
    {
        public const string RoleNotRequestable = "roleRequest.roleNotRequestable";
        public const string MessageTooLong = "roleRequest.messageTooLong";
        public const string NotPending = "roleRequest.notPending";
        public const string ReasonRequired = "roleRequest.reasonRequired";
    }

    public static class Setting
    {
        public const string Unknown = "setting.unknown";
        public const string ReadOnly = "setting.readOnly";
        public const string ValueRequired = "setting.valueRequired";
        public const string IntegerRequired = "setting.integerRequired";
        public const string NumberRequired = "setting.numberRequired";
        public const string OutOfRange = "setting.outOfRange";
        public const string PercentMinAboveMax = "setting.percentMinAboveMax";
    }

    public static class StoreCategory
    {
        public const string NameRequired = "storeCategory.nameRequired";
        public const string NameTooLong = "storeCategory.nameTooLong";
        public const string SlugInvalid = "storeCategory.slugInvalid";
        public const string IconTooLong = "storeCategory.iconTooLong";
    }

    public static class Store
    {
        public const string NameRequired = "store.nameRequired";
        public const string NameLength = "store.nameLength";
        public const string SlugInvalid = "store.slugInvalid";
        public const string DescriptionTooLong = "store.descriptionTooLong";
        public const string PhoneInvalid = "store.phoneInvalid";
        public const string CityInvalid = "store.cityInvalid";
        public const string AddressTooLong = "store.addressTooLong";
        public const string PercentOutOfRange = "store.percentOutOfRange";
        public const string DeliveryFeeInvalid = "store.deliveryFeeInvalid";
        public const string DeliveryCitiesRequired = "store.deliveryCitiesRequired";
        public const string DeliveryDelayInvalid = "store.deliveryDelayInvalid";
        public const string LinkInvalid = "store.linkInvalid";
        public const string NotOwnerControlled = "store.notOwnerControlled";
        public const string Deleted = "store.deleted";
    }

    public static class Moderation
    {
        public const string ReasonRequired = "moderation.reasonRequired";
    }

    public static class ProductCategory
    {
        public const string NameRequired = "productCategory.nameRequired";
        public const string NameTooLong = "productCategory.nameTooLong";
        public const string SlugInvalid = "productCategory.slugInvalid";
        public const string TooManyAttributes = "productCategory.tooManyAttributes";
        public const string AttributeNotFound = "productCategory.attributeNotFound";
        public const string AttributeKeyInvalid = "productCategory.attributeKeyInvalid";
        public const string AttributeKeyTaken = "productCategory.attributeKeyTaken";
        public const string AttributeTypeInvalid = "productCategory.attributeTypeInvalid";
        public const string UnitInvalid = "productCategory.unitInvalid";
        public const string OptionsRequired = "productCategory.optionsRequired";
        public const string OptionsNotAllowed = "productCategory.optionsNotAllowed";
        public const string OptionKeyInvalid = "productCategory.optionKeyInvalid";
        public const string OptionKeyTaken = "productCategory.optionKeyTaken";
    }

    public static class Product
    {
        public const string NameRequired = "product.nameRequired";
        public const string NameLength = "product.nameLength";
        public const string SlugInvalid = "product.slugInvalid";
        public const string DescriptionTooLong = "product.descriptionTooLong";
        public const string PriceInvalid = "product.priceInvalid";
        public const string CompareAtPriceInvalid = "product.compareAtPriceInvalid";
        public const string SkuTooLong = "product.skuTooLong";
        public const string StockInvalid = "product.stockInvalid";
        public const string PercentOutOfRange = "product.percentOutOfRange";
        public const string StatusInvalid = "product.statusInvalid";
        public const string TooManyImages = "product.tooManyImages";
        public const string UnknownAttribute = "product.unknownAttribute";
        public const string AttributeValueInvalid = "product.attributeValueInvalid";
        public const string RequiredAttributeMissing = "product.requiredAttributeMissing";
        public const string VariantAxesInvalid = "product.variantAxesInvalid";
        public const string VariantsRequired = "product.variantsRequired";
        public const string VariantInvalid = "product.variantInvalid";
        public const string VariantDuplicate = "product.variantDuplicate";
        public const string Deleted = "product.deleted";
    }

    public static class Image
    {
        public const string Required = "image.required";
        public const string TooLarge = "image.tooLarge";
        public const string UnsupportedType = "image.unsupportedType";
    }
}
