using WebApp.Domain.Enums;

namespace WebApp.Application.DTOs;

/// <summary>An affiliate asks to work with a store, with an optional note for the owner.</summary>
public sealed record CreatePartnershipRequest(Guid StoreId, string? Message);

/// <summary>Optional reason of a refusal or an end (required when an admin ends one).</summary>
public sealed record PartnershipReasonRequest(string? Reason);

/// <summary>The store as an affiliate sees it in "My stores". <c>IsVisible</c> is false once an admin disabled or deleted it.</summary>
public sealed record PartnerStoreRef(Guid Id, string Name, string Slug, string? LogoUrl, bool IsOpen, bool IsVisible, int DefaultAffiliatePercent);

/// <summary>A partnership as its affiliate sees it.</summary>
public sealed record AffiliatePartnershipDto(
    Guid Id,
    PartnershipStatus Status,
    string? Message,
    string? DecisionReason,
    string? EndReason,
    DateTime CreatedAt,
    DateTime? DecidedAt,
    DateTime? EndedAt,
    PartnerStoreRef Store);

/// <summary>What the store owner sees of an affiliate: enough to decide, no contact details.</summary>
public sealed record PartnerAffiliate(Guid Id, string FullName, string? Code, bool CodeActive, DateTime MemberSince, int ActivePartnerships);

/// <summary>A partnership as the store owner sees it.</summary>
public sealed record StorePartnershipDto(
    Guid Id,
    PartnershipStatus Status,
    string? Message,
    string? DecisionReason,
    string? EndReason,
    DateTime CreatedAt,
    DateTime? DecidedAt,
    DateTime? EndedAt,
    PartnerAffiliate Affiliate);

public sealed record AdminPartnershipQuery(PartnershipStatus? Status, string? Search, Guid? StoreId, Guid? AffiliateId, int? Page, int? PageSize);

public sealed record AdminPartnershipItem(
    Guid Id,
    PartnershipStatus Status,
    Guid AffiliateId,
    string AffiliateName,
    string AffiliateEmail,
    string? AffiliateCode,
    Guid StoreId,
    string StoreName,
    string StoreSlug,
    Guid StoreOwnerId,
    string StoreOwnerName,
    string StoreOwnerEmail,
    string? Message,
    string? DecisionReason,
    string? EndReason,
    DateTime CreatedAt,
    DateTime? DecidedAt,
    DateTime? EndedAt);

/// <summary>A code typed on a product (cart 013, checkout 014). Accepts <c>#DRF-45</c>, <c>drf45</c>…</summary>
public sealed record CheckAffiliateCodeRequest(string Code, Guid ProductId);

/// <summary>
/// The code is valid on that product. <c>AffiliateName</c> is shortened ("Sana T.") so a valid code
/// reveals little about its owner; <c>AffiliatePercent</c> is the product's current commission.
/// </summary>
public sealed record AffiliateCodeCheckDto(string Code, string AffiliateName, Guid StoreId, int AffiliatePercent);

/// <summary>A valid code on a product, with what checkout freezes on the order line (014).</summary>
public sealed record AffiliateCodeMatch(string Code, Guid AffiliateId, Guid PartnershipId, string AffiliateName, Guid StoreId, int AffiliatePercent);

/// <summary>A visible product's store, as the code check needs it.</summary>
public sealed record ProductStoreInfo(Guid ProductId, Guid StoreId, Guid OwnerId, int AffiliatePercent);

/// <summary>Admin deactivates an affiliate code (fraud).</summary>
public sealed record DisableAffiliateCodeRequest(string Reason);
