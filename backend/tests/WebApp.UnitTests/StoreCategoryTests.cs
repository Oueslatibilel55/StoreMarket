using WebApp.Domain.Common;
using WebApp.Domain.Entities;

namespace WebApp.UnitTests;

public sealed class StoreCategoryTests
{
    private static StoreCategory New(string nameFr = "Parfums", string? slug = null, int order = 1) =>
        StoreCategory.Create(nameFr, "عطور", "Perfumes", slug, "🌸", order).Value;

    [Theory]
    [InlineData("Parfums", "parfums")]
    [InlineData("  Beauté & Soins  ", "beaute-soins")]
    [InlineData("Électronique / High-Tech", "electronique-high-tech")]
    [InlineData("Prêt-à-porter", "pret-a-porter")]
    public void Slugify_MakesUrlNames(string name, string slug) => Assert.Equal(slug, Slug.From(name));

    [Fact]
    public void Create_TrimsNames_MakesSlug_IsActive()
    {
        var c = StoreCategory.Create("  Vêtements ", " ", null, null, null, 3).Value;

        Assert.Equal("Vêtements", c.NameFr);
        Assert.Null(c.NameAr);
        Assert.Equal("vetements", c.Slug);
        Assert.True(c.IsActive);
        Assert.Equal(3, c.SortOrder);
    }

    [Fact]
    public void Create_KeepsGivenSlug_LowerCased() => Assert.Equal("beaute", New(slug: " BEAUTE ").Slug);

    [Theory]
    [InlineData("", null, DomainErrors.StoreCategory.NameRequired)]
    [InlineData("عطور", null, DomainErrors.StoreCategory.SlugInvalid)] // nothing Latin to make a slug from
    [InlineData("Parfums", "parfums & co", DomainErrors.StoreCategory.SlugInvalid)]
    [InlineData("Parfums", "p", DomainErrors.StoreCategory.SlugInvalid)]
    public void Create_RejectsBadInput(string name, string? slug, string error) =>
        Assert.Equal(error, StoreCategory.Create(name, null, null, slug, null, 1).Error);

    [Fact]
    public void Create_RejectsLongNamesAndIcons()
    {
        Assert.Equal(DomainErrors.StoreCategory.NameTooLong,
            StoreCategory.Create(new string('a', StoreCategory.MaxNameLength + 1), null, null, null, null, 1).Error);
        Assert.Equal(DomainErrors.StoreCategory.IconTooLong,
            StoreCategory.Create("Parfums", null, null, null, new string('x', StoreCategory.MaxIconLength + 1), 1).Error);
    }

    [Fact]
    public void Update_InvalidInput_KeepsOldValues()
    {
        var c = New();

        Assert.False(c.Update("", null, null, null, null).IsSuccess);
        Assert.Equal("Parfums", c.NameFr);
        Assert.Equal("parfums", c.Slug);
    }

    [Fact]
    public void Deactivate_ThenActivate()
    {
        var c = New();
        c.Deactivate();
        Assert.False(c.IsActive);
        c.Activate();
        Assert.True(c.IsActive);
    }

    [Fact]
    public void SwapOrderWith_ExchangesPositions()
    {
        var a = New("Alpha", order: 1);
        var b = New("Beta", order: 2);

        b.SwapOrderWith(a);

        Assert.Equal(2, a.SortOrder);
        Assert.Equal(1, b.SortOrder);
    }
}
