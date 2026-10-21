using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

/// <summary>Reason the admin gives when disabling or deleting — shown to the owner and audited.</summary>
public sealed record ModerationRequest(string? Reason);

/// <summary>Disable several products at once (≤ <see cref="MaxIds"/>).</summary>
public sealed record BulkModerationRequest(IReadOnlyList<Guid>? Ids, string? Reason)
{
    public const int MaxIds = 100;
}

public sealed record BulkModerationResult(int Changed);

/// <summary>Admin store list. <c>Deleted</c>: false (default) = live ones only, true = deleted ones only.</summary>
public sealed record AdminStoreQuery(
    string? Search, StoreStatus? Status, string? Category, string? City, bool? Deleted, DateTime? CreatedFrom, DateTime? CreatedTo,
    int? Page, int? PageSize);

public sealed record AdminStoreListItem(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    LocalizedName CategoryName,
    string City,
    StoreStatus Status,
    bool IsDeleted,
    string? ModerationReason,
    Guid OwnerId,
    string OwnerName,
    string OwnerEmail,
    int ProductCount,
    DateTime CreatedAt);

public sealed record AdminStoreOwner(Guid Id, string FullName, string Email, string Phone, UserStatus Status, bool IsStoreOwner);

/// <summary>Everything about a store for the admin: configuration, moderation state, owner, product counts.</summary>
public sealed record AdminStoreDto(
    Guid Id,
    string Name,
    string Slug,
    StoreCategoryRef Category,
    string? Description,
    string Phone,
    string City,
    string? Address,
    string? LogoUrl,
    string? BannerUrl,
    StoreStatus Status,
    bool IsDeleted,
    string? ModerationReason,
    DateTime? DisabledAt,
    DateTime? DeletedAt,
    int DefaultAffiliatePercent,
    decimal DeliveryFee,
    IReadOnlyList<string> DeliveryCities,
    int DeliveryMinDays,
    int DeliveryMaxDays,
    string? FacebookUrl,
    string? InstagramUrl,
    string? WebsiteUrl,
    decimal SubscriptionMonthlyPrice,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    AdminStoreOwner Owner,
    int ProductCount,
    int PublishedCount,
    int DisabledCount);

/// <summary>Moderation view of a product. <c>Live</c> = what the shop shows (published, not disabled, not deleted).</summary>
public enum AdminProductState
{
    Live = 1,
    Draft = 2,
    Archived = 3,
    Disabled = 4,
    Deleted = 5,
}

/// <summary>Admin product list across all stores. Without a state, deleted products are left out.</summary>
public sealed record AdminProductQuery(
    string? Search, Guid? StoreId, string? Category, decimal? MinPrice, decimal? MaxPrice, AdminProductState? State,
    ProductSort? Sort, int? Page, int? PageSize);

public sealed record AdminProductListItem(
    Guid Id,
    string Slug,
    string Name,
    string? MainImageUrl,
    decimal Price,
    decimal MinPrice,
    int ImageCount,
    ProductStatus Status,
    bool IsDisabled,
    bool IsDeleted,
    string? ModerationReason,
    LocalizedName CategoryName,
    Guid StoreId,
    string StoreName,
    string StoreSlug,
    DateTime CreatedAt);

/// <summary>Audit snapshot of a store's moderation state.</summary>
public sealed record StoreModerationSnapshot(StoreStatus Status, bool IsDeleted, string? ModerationReason);

/// <summary>Audit snapshot of a product's moderation state.</summary>
public sealed record ProductModerationSnapshot(string Name, decimal Price, ProductStatus Status, bool IsDisabled, bool IsDeleted, string? ModerationReason);
