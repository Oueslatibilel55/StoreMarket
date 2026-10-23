using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.UnitTests;

public sealed class ProductTests
{
    private static readonly Guid StoreId = Guid.CreateVersion7();

    /// <summary>Schema of a dress: Taille (one choice, required), Couleur (several), Matière (text), Longueur (number).</summary>
    private static readonly IReadOnlyList<AttributeDefinition> Schema = BuildSchema();

    private static IReadOnlyList<AttributeDefinition> BuildSchema()
    {
        var c = ProductCategory.Create(null, "Robes", null, null, null, 1).Value;
        static OptionInput O(string name) => new(null, name, null, null);
        c.AddAttribute(new AttributeInput(null, "Taille", null, null, AttributeType.SingleChoice, null, true, true, [O("S"), O("M"), O("L")]));
        c.AddAttribute(new AttributeInput(null, "Couleur", null, null, AttributeType.MultiChoice, null, false, true, [O("Noir"), O("Blanc"), O("Rouge")]));
        c.AddAttribute(new AttributeInput(null, "Matière", null, null, AttributeType.Text, null, false, false, null));
        c.AddAttribute(new AttributeInput(null, "Longueur", null, null, AttributeType.Number, "cm", false, false, null));
        c.AddAttribute(new AttributeInput(null, "Doublée", null, null, AttributeType.Boolean, null, false, false, null));
        return c.Attributes;
    }

    private static AttributeValueInput Choice(string key, params string[] options) => new(key, null, null, null, options);

    private static ProductDetails Details(Func<ProductDetails, ProductDetails>? change = null)
    {
        var d = new ProductDetails("Robe fleurie", null, Guid.CreateVersion7(), "Robe d'été", 89.9m, 120m, "RF-01", 12, null,
            ProductStatus.Published, [], [Choice("taille", "m"), Choice("couleur", "rouge", "noir"),
                new AttributeValueInput("matiere", "Coton", null, null, null), new AttributeValueInput("longueur", null, 95, null, null)],
            [], []);
        return change is null ? d : change(d);
    }

    private static Result<Product> Create(ProductDetails d) => Product.Create(StoreId, d, Schema, 0, 50);

    [Fact]
    public void Admin_Disable_KeepsOwnerStatus_OwnerEditsDontLiftIt()
    {
        var p = Create(Details()).Value;

        Assert.Equal(DomainErrors.Moderation.ReasonRequired, p.Disable(null, DateTime.UtcNow).Error);
        Assert.True(p.Disable("Prix absurde", DateTime.UtcNow).IsSuccess);
        Assert.True(p.IsDisabled);
        Assert.Equal(ProductStatus.Published, p.Status);

        Assert.True(p.Update(Details(d => d with { Price = 79m }), Schema, 0, 50).IsSuccess);
        Assert.True(p.IsDisabled); // only an admin enables it again
        Assert.Equal("Prix absurde", p.ModerationReason);

        Assert.True(p.Enable().IsSuccess);
        Assert.False(p.IsDisabled);
        Assert.Null(p.ModerationReason);
    }

    [Fact]
    public void Admin_Delete_IsSoft_AndFinal()
    {
        var p = Create(Details()).Value;

        Assert.Equal(DomainErrors.Moderation.ReasonRequired, p.DeleteByAdmin(" ", DateTime.UtcNow).Error);
        Assert.True(p.DeleteByAdmin("Contrefaçon", DateTime.UtcNow).IsSuccess);

        Assert.True(p.IsDeleted);
        Assert.Equal("Contrefaçon", p.ModerationReason);
        Assert.Equal(DomainErrors.Product.Deleted, p.Enable().Error);
        Assert.Equal(DomainErrors.Product.Deleted, p.Disable("x", DateTime.UtcNow).Error);
    }

    [Fact]
    public void Create_StoresCanonicalValues()
    {
        var p = Create(Details()).Value;

        Assert.Equal("robe-fleurie", p.Slug);
        Assert.Equal("m", p.Values.Single(v => v.Key == "taille").Value);
        Assert.Equal("noir,rouge", p.Values.Single(v => v.Key == "couleur").Value); // the field's order of choices
        Assert.Equal("95", p.Values.Single(v => v.Key == "longueur").Value);
        Assert.Equal(12, p.TotalStock);
        Assert.True(p.UsesOption("couleur", "rouge"));
        Assert.False(p.UsesOption("couleur", "blanc"));
    }

    [Fact]
    public void Values_AreCheckedAgainstTheSchema()
    {
        Assert.Equal(DomainErrors.Product.UnknownAttribute, Create(Details(d => d with { Values = [Choice("pointure", "40")] })).Error);
        Assert.Equal(DomainErrors.Product.AttributeValueInvalid, Create(Details(d => d with { Values = [Choice("taille", "xxl")] })).Error);
        Assert.Equal(DomainErrors.Product.AttributeValueInvalid, Create(Details(d => d with { Values = [Choice("taille", "s", "m")] })).Error);
        Assert.Equal(DomainErrors.Product.AttributeValueInvalid,
            Create(Details(d => d with { Values = [Choice("taille", "m"), new AttributeValueInput("longueur", null, -5, null, null)] })).Error);
    }

    [Fact]
    public void RequiredFields_OnlyForPublishedProducts()
    {
        Assert.Equal(DomainErrors.Product.RequiredAttributeMissing, Create(Details(d => d with { Values = [] })).Error);
        Assert.True(Create(Details(d => d with { Values = [], Status = ProductStatus.Draft })).IsSuccess);
    }

    [Fact]
    public void Prices_Stock_Percent()
    {
        Assert.Equal(DomainErrors.Product.PriceInvalid, Create(Details(d => d with { Price = 0 })).Error);
        Assert.Equal(DomainErrors.Product.PriceInvalid, Create(Details(d => d with { Price = 10.0001m })).Error);
        Assert.Equal(DomainErrors.Product.CompareAtPriceInvalid, Create(Details(d => d with { CompareAtPrice = 50m })).Error);
        Assert.Equal(DomainErrors.Product.StockInvalid, Create(Details(d => d with { Stock = -1 })).Error);
        Assert.Equal(DomainErrors.Product.PercentOutOfRange, Create(Details(d => d with { AffiliatePercent = 60 })).Error);
        Assert.Equal(DomainErrors.Product.TooManyImages,
            Create(Details(d => d with { ImageIds = [.. Enumerable.Range(0, 9).Select(_ => Guid.CreateVersion7())] })).Error);
    }

    [Fact]
    public void Variants_OneOptionPerAxis_StockSummed()
    {
        var p = Create(Details(d => d with
        {
            VariantAxes = ["taille", "couleur"],
            Variants =
            [
                new VariantInput(null, new Dictionary<string, string> { ["taille"] = "s", ["couleur"] = "noir" }, 3, null, null),
                new VariantInput(null, new Dictionary<string, string> { ["taille"] = "m", ["couleur"] = "noir" }, 5, 99m, "RF-M-N"),
            ],
        })).Value;

        Assert.True(p.HasVariants);
        Assert.Equal(8, p.TotalStock);
        Assert.Equal(0, p.Stock);
        Assert.DoesNotContain(p.Values, v => v.Key is "taille" or "couleur"); // given by the variants
        Assert.True(p.UsesOption("taille", "s"));
        Assert.True(p.UsesAttribute("couleur"));
    }

    [Fact]
    public void Variants_Rules()
    {
        var axes = new[] { "taille" };
        VariantInput V(string size) => new(null, new Dictionary<string, string> { ["taille"] = size }, 1, null, null);

        Assert.Equal(DomainErrors.Product.VariantAxesInvalid, Create(Details(d => d with { VariantAxes = ["matiere"], Variants = [V("s")] })).Error);
        Assert.Equal(DomainErrors.Product.VariantAxesInvalid,
            Create(Details(d => d with { VariantAxes = ["taille", "couleur", "longueur"], Variants = [V("s")] })).Error);
        Assert.Equal(DomainErrors.Product.VariantsRequired, Create(Details(d => d with { VariantAxes = axes, Variants = [] })).Error);
        Assert.Equal(DomainErrors.Product.VariantInvalid, Create(Details(d => d with { VariantAxes = axes, Variants = [V("xl")] })).Error);
        Assert.Equal(DomainErrors.Product.VariantDuplicate, Create(Details(d => d with { VariantAxes = axes, Variants = [V("s"), V("s")] })).Error);
    }

    [Fact]
    public void FromPrice_IsTheCheapestVariant_OrThePrice()
    {
        VariantInput V(string size, decimal? price) => new(null, new Dictionary<string, string> { ["taille"] = size }, 1, price, null);
        var p = Create(Details(d => d with { Price = 60m, VariantAxes = ["taille"], Variants = [V("s", 45m), V("m", null), V("l", 70m)] })).Value;
        Assert.Equal(45m, p.FromPrice);

        p.Update(Details(d => d with { Price = 40m, VariantAxes = ["taille"], Variants = [V("s", 45m), V("m", null)] }), Schema, 0, 50);
        Assert.Equal(40m, p.FromPrice); // the variant without its own price costs the product price

        p.Update(Details(d => d with { Price = 30m }), Schema, 0, 50);
        Assert.Equal(30m, p.FromPrice); // no variants any more
    }

    [Fact]
    public void Update_KeepsVariantIds_ForTheSameCombination()
    {
        VariantInput V(string size, int stock) => new(null, new Dictionary<string, string> { ["taille"] = size }, stock, null, null);
        var p = Create(Details(d => d with { VariantAxes = ["taille"], Variants = [V("s", 1), V("m", 1)] })).Value;
        var small = p.Variants.Single(v => v.Selection[0].Value == "s").Id;

        p.Update(Details(d => d with { VariantAxes = ["taille"], Variants = [V("s", 7), V("l", 2)] }), Schema, 0, 50);

        Assert.Equal(small, p.Variants.Single(v => v.Selection[0].Value == "s").Id);
        Assert.Equal(7, p.Variants.Single(v => v.Id == small).Stock);
        Assert.Equal(["s", "l"], p.Variants.Select(v => v.Selection[0].Value));
    }

    [Fact]
    public void Delete_IsSoft_AndBlocksEditing()
    {
        var p = Create(Details()).Value;

        p.Delete(DateTime.UtcNow);

        Assert.True(p.IsDeleted);
        Assert.Equal(ProductStatus.Archived, p.Status);
        Assert.Equal(DomainErrors.Product.Deleted, p.Update(Details(), Schema, 0, 50).Error);
    }
}
