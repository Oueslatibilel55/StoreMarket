using WebApp.Application.DTOs;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.UnitTests;

public sealed class ProductCategoryTests
{
    private static ProductCategory New(string name = "Parfums", Guid? parent = null, int order = 1) =>
        ProductCategory.Create(parent, name, "عطور", "Perfumes", null, order).Value;

    private static AttributeInput Choice(string name, params string[] options) =>
        new(null, name, null, null, AttributeType.SingleChoice, null, Required: true, Filterable: true,
            [.. options.Select(o => new OptionInput(null, o, null, null))]);

    private static AttributeInput Number(string name, string? unit = null) =>
        new(null, name, null, null, AttributeType.Number, unit, Required: true, Filterable: false, null);

    [Fact]
    public void Create_MakesSlug_IsActive()
    {
        var c = New("Prêt-à-porter");

        Assert.Equal("pret-a-porter", c.Slug);
        Assert.True(c.IsActive);
    }

    [Fact]
    public void AddAttribute_ChoiceWithOptions_KeysFromNames()
    {
        var c = New();

        var added = c.AddAttribute(Choice("Concentration", "Eau de toilette", "Eau de parfum", "Parfum"));

        Assert.True(added.IsSuccess);
        Assert.Equal("concentration", added.Value.Key);
        Assert.Equal(["eau-de-toilette", "eau-de-parfum", "parfum"], added.Value.Options.Select(o => o.Key));
    }

    [Fact]
    public void AddAttribute_SingleLetterOptionKeys_AreFine()
    {
        var added = New("Vêtements").AddAttribute(Choice("Taille", "S", "M", "L", "XL"));

        Assert.Equal(["s", "m", "l", "xl"], added.Value.Options.Select(o => o.Key));
    }

    [Fact]
    public void AddAttribute_Rules()
    {
        var c = New();

        Assert.Equal(DomainErrors.ProductCategory.OptionsRequired, c.AddAttribute(Choice("Genre", "Homme")).Error);
        Assert.Equal(DomainErrors.ProductCategory.OptionKeyTaken, c.AddAttribute(Choice("Genre", "Homme", "homme")).Error);
        Assert.Equal(DomainErrors.ProductCategory.OptionsNotAllowed,
            c.AddAttribute(Number("Volume") with { Options = [new OptionInput(null, "50", null, null)] }).Error);
        Assert.Equal(DomainErrors.ProductCategory.UnitInvalid,
            c.AddAttribute(new AttributeInput(null, "Notes", null, null, AttributeType.Text, "ml", false, false, null)).Error);
        Assert.Equal(DomainErrors.ProductCategory.AttributeTypeInvalid, c.AddAttribute(Number("X") with { Type = (AttributeType)99 }).Error);

        Assert.True(c.AddAttribute(Number("Volume", "ml")).IsSuccess);
        Assert.Equal(DomainErrors.ProductCategory.AttributeKeyTaken, c.AddAttribute(Number("Volume")).Error);
    }

    [Fact]
    public void UpdateAttribute_KeepsKeyAndType()
    {
        var c = New();
        var volume = c.AddAttribute(Number("Volume", "ml")).Value;

        var updated = c.UpdateAttribute(volume.Id, Number("Contenance", "cl") with { Type = AttributeType.Text, Key = "autre" });

        Assert.True(updated.IsSuccess);
        Assert.Equal("volume", volume.Key); // product values point to the key
        Assert.Equal(AttributeType.Number, volume.Type);
        Assert.Equal("Contenance", volume.NameFr);
        Assert.Equal("cl", volume.Unit);
    }

    [Fact]
    public void UpdateAttribute_KeepsTheKeysOfExistingOptions()
    {
        var c = New();
        var gender = c.AddAttribute(Choice("Genre", "Homme", "Femme")).Value;

        c.UpdateAttribute(gender.Id, Choice("Genre", "Homme", "Femme", "Unisexe") with
        {
            Options = [new OptionInput("homme", "Hommes", null, null), new OptionInput("femme", "Femmes", null, null), new OptionInput(null, "Unisexe", null, null)],
        });

        Assert.Equal(["homme", "femme", "unisexe"], gender.Options.Select(o => o.Key));
        Assert.Equal("Hommes", gender.Options[0].NameFr);
    }

    [Fact]
    public void MoveAttribute_SwapsWithNeighbour()
    {
        var c = New();
        var a = c.AddAttribute(Number("Volume")).Value;
        var b = c.AddAttribute(Choice("Genre", "Homme", "Femme")).Value;

        c.MoveAttribute(b.Id, -1);

        Assert.True(b.SortOrder < a.SortOrder);
    }

    [Fact]
    public void Tree_Path_Depth_Height_Visibility()
    {
        var clothes = New("Vêtements");
        var women = New("Femme", clothes.Id);
        var dresses = New("Robes", women.Id);
        var tree = new CategoryTree([clothes, women, dresses]);

        Assert.Equal([clothes, women, dresses], tree.PathTo(dresses));
        Assert.Equal(3, tree.Depth(dresses));
        Assert.Equal(3, tree.Height(clothes));
        Assert.Equal([women, dresses], tree.Descendants(clothes));

        women.Deactivate();
        Assert.False(tree.IsVisible(dresses)); // a hidden category hides its branch
        Assert.True(tree.IsVisible(clothes));
    }
}
