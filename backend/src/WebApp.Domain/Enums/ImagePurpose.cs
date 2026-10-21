namespace WebApp.Domain.Enums;

/// <summary>What an image is for (decides its folder and who may upload it). Stored as int — append only.</summary>
public enum ImagePurpose
{
    StoreLogo = 1,
    StoreBanner = 2,
    ProductPhoto = 3,
    CategoryImage = 4,
}
