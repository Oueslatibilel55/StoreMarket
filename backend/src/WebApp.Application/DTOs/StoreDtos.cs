using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

/// <summary>Create or edit my store. Without a slug, one is made from the name. Cities are governorate codes.</summary>
public sealed record SaveStoreRequest(
    string Name,
    string? Slug,
    Guid CategoryId,
    string? Description,
    string Phone,
    string City,
    string? Address,
    Guid? LogoImageId,
    Guid? BannerImageId,
    int DefaultAffiliatePercent,
    decimal DeliveryFee,
    IReadOnlyList<string>? DeliveryCities,
    int DeliveryMinDays,
    int DeliveryMaxDays,
    string? FacebookUrl,
    string? InstagramUrl,
    string? WebsiteUrl)
{
    public StoreDetails ToDetails() => new(Name, Slug, CategoryId, Description, Phone, City, Address, LogoImageId, BannerImageId,
        DefaultAffiliatePercent, DeliveryFee, DeliveryCities ?? [], DeliveryMinDays, DeliveryMaxDays, FacebookUrl, InstagramUrl, WebsiteUrl);
}

public sealed record StoreCategoryRef(Guid Id, string Slug, LocalizedName Name);

/// <summary>The owner's view of their store (everything, including the subscription price).</summary>
public sealed record MyStoreDto(
    Guid Id,
    string Name,
    string Slug,
    StoreCategoryRef Category,
    string? Description,
    string Phone,
    string City,
    string? Address,
    Guid? LogoImageId,
    string? LogoUrl,
    Guid? BannerImageId,
    string? BannerUrl,
    StoreStatus Status,
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
    string? ModerationReason,
    bool IsDeleted);

/// <summary>What visitors see of a store (active or temporarily closed ones only).</summary>
public sealed record PublicStoreDto(
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
    bool IsOpen,
    int DefaultAffiliatePercent,
    decimal DeliveryFee,
    IReadOnlyList<string> DeliveryCities,
    int DeliveryMinDays,
    int DeliveryMaxDays,
    string? FacebookUrl,
    string? InstagramUrl,
    string? WebsiteUrl,
    RatingDto Rating);
