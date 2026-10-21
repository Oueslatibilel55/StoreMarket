using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.UnitTests;

public sealed class ImageTests
{
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];
    public static readonly byte[] Webp = [.. "RIFF"u8, 0x24, 0, 0, 0, .. "WEBP"u8];

    [Fact]
    public void Detect_RecognisesAcceptedFormatsByTheirBytes()
    {
        Assert.Equal(ImageSignature.Png, ImageSignature.Detect(Png));
        Assert.Equal(ImageSignature.Jpeg, ImageSignature.Detect(Jpeg));
        Assert.Equal(ImageSignature.Webp, ImageSignature.Detect(Webp));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
    [InlineData("GIF89a......")]
    [InlineData("<html>hello</html>")]
    [InlineData("")]
    public void Detect_RejectsEverythingElse(string content) =>
        Assert.Null(ImageSignature.Detect(System.Text.Encoding.ASCII.GetBytes(content)));

    [Fact]
    public void Create_BuildsThePathFromTheId_NeverFromTheFileName()
    {
        var now = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

        var image = StoredImage.Create(Guid.CreateVersion7(), ImagePurpose.ProductPhoto, ImageSignature.Webp, 1234,
            @"..\..\etc\passwd.webp", now);

        Assert.Equal($"product-photos/2026/10/{image.Id:N}.webp", image.Path);
        Assert.Equal("passwd.webp", image.OriginalName); // folder part dropped, kept for display only
        Assert.Equal("image/webp", image.ContentType);
    }

    [Theory]
    [InlineData(ImagePurpose.StoreLogo, "store-logos")]
    [InlineData(ImagePurpose.StoreBanner, "store-banners")]
    [InlineData(ImagePurpose.CategoryImage, "category-images")]
    public void Folder_PerPurpose(ImagePurpose purpose, string folder) => Assert.Equal(folder, StoredImage.Folder(purpose));
}
