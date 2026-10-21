namespace WebApp.Application.Interfaces;

/// <summary>
/// Records admin actions. Entries are added to the current unit of work, so they are saved in the
/// same transaction as the change they describe — no change without its audit line.
/// </summary>
public interface IAuditLog
{
    void Record(string actorEmail, Guid actorId, string action, string targetType, Guid targetId,
        string? targetLabel, object? before, object? after, string? reason = null);
}

public static class AuditActions
{
    public const string UserRoleGrant = "user.role.grant";
    public const string UserRoleRevoke = "user.role.revoke";
    public const string UserBlock = "user.block";
    public const string UserUnblock = "user.unblock";
    public const string RoleRequestApprove = "roleRequest.approve";
    public const string RoleRequestRefuse = "roleRequest.refuse";
    public const string SettingUpdate = "setting.update";
    public const string StoreCategoryCreate = "storeCategory.create";
    public const string StoreCategoryUpdate = "storeCategory.update";
    public const string StoreCategoryActivate = "storeCategory.activate";
    public const string StoreCategoryDeactivate = "storeCategory.deactivate";
    public const string StoreCategoryMove = "storeCategory.move";
    public const string StoreCategoryDelete = "storeCategory.delete";
    public const string ProductCategoryCreate = "productCategory.create";
    public const string ProductCategoryUpdate = "productCategory.update";
    public const string ProductCategoryActivate = "productCategory.activate";
    public const string ProductCategoryDeactivate = "productCategory.deactivate";
    public const string ProductCategoryMove = "productCategory.move";
    public const string ProductCategoryDelete = "productCategory.delete";
    public const string AttributeCreate = "productCategory.attribute.create";
    public const string AttributeUpdate = "productCategory.attribute.update";
    public const string AttributeMove = "productCategory.attribute.move";
    public const string AttributeDelete = "productCategory.attribute.delete";
    public const string StoreDisable = "store.disable";
    public const string StoreEnable = "store.enable";
    public const string StoreDelete = "store.delete";
    public const string ProductDisable = "product.disable";
    public const string ProductEnable = "product.enable";
    public const string ProductDelete = "product.delete";
    public const string UserAffiliateCodeDisable = "user.affiliateCode.disable";
    public const string UserAffiliateCodeEnable = "user.affiliateCode.enable";
    public const string PartnershipEnd = "partnership.end";
    public const string OrderForceStatus = "order.forceStatus";
    public const string CoinAdjustment = "coins.adjustment";
    public const string PayoutVerify = "payout.verify";
    public const string PayoutReject = "payout.reject";
    public const string PayoutSchedule = "payout.schedule";
    public const string PayoutPaid = "payout.paid";
}

public static class AuditTargets
{
    public const string User = "User";
    public const string Setting = "Setting";
    public const string StoreCategory = "StoreCategory";
    public const string ProductCategory = "ProductCategory";
    public const string Store = "Store";
    public const string Product = "Product";
    public const string Order = "Order";
    public const string Payout = "Payout";
}
