using WebApp.Domain.Common;
using WebApp.Domain.Entities;

namespace WebApp.UnitTests;

public sealed class CartTests
{
    private static readonly Guid ProductA = Guid.CreateVersion7();
    private static readonly Guid ProductB = Guid.CreateVersion7();
    private static readonly Guid Variant38 = Guid.CreateVersion7();
    private static readonly Guid Variant39 = Guid.CreateVersion7();

    private static Cart NewCart() => Cart.For(Guid.CreateVersion7());

    [Fact]
    public void Add_NewProduct_CreatesALine_WithCanonicalCode()
    {
        var cart = NewCart();

        var line = cart.Add(ProductA, null, 2, "#drf45").Value;

        Assert.Single(cart.Lines);
        Assert.Equal(2, line.Quantity);
        Assert.Equal("DRF-45", line.AffiliateCode);
    }

    [Fact]
    public void Add_SameProductAndVariant_AddsUpQuantity_OtherVariantIsAnotherLine()
    {
        var cart = NewCart();
        cart.Add(ProductA, Variant38, 1, null);
        cart.Add(ProductA, Variant38, 2, null);
        cart.Add(ProductA, Variant39, 1, null);

        Assert.Equal(2, cart.Lines.Count);
        Assert.Equal(3, cart.Find(ProductA, Variant38)!.Quantity);
    }

    [Fact]
    public void Add_WithACode_ReplacesTheLineCode_WithoutKeepsIt()
    {
        var cart = NewCart();
        cart.Add(ProductA, null, 1, "DRF-45");

        cart.Add(ProductA, null, 1, null);
        Assert.Equal("DRF-45", cart.Lines[0].AffiliateCode);

        cart.Add(ProductA, null, 1, "abc-12");
        Assert.Equal("ABC-12", cart.Lines[0].AffiliateCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Cart.MaxQuantity + 1)]
    public void Add_QuantityOutOfRange_Fails(int quantity) =>
        Assert.Equal(DomainErrors.Cart.QuantityInvalid, NewCart().Add(ProductA, null, quantity, null).Error);

    [Fact]
    public void Add_AboveMaxQuantityByAddingUp_Fails()
    {
        var cart = NewCart();
        cart.Add(ProductA, null, Cart.MaxQuantity, null);
        Assert.Equal(DomainErrors.Cart.QuantityInvalid, cart.Add(ProductA, null, 1, null).Error);
    }

    [Fact]
    public void Add_MalformedCode_Fails() =>
        Assert.Equal(DomainErrors.Cart.CodeInvalid, NewCart().Add(ProductA, null, 1, "pas un code").Error);

    [Fact]
    public void Add_BeyondMaxLines_Fails()
    {
        var cart = NewCart();
        for (var i = 0; i < Cart.MaxLines; i++) cart.Add(Guid.CreateVersion7(), null, 1, null);
        Assert.Equal(DomainErrors.Cart.TooManyLines, cart.Add(ProductB, null, 1, null).Error);
    }

    [Fact]
    public void SetQuantity_SetCode_Remove()
    {
        var cart = NewCart();
        var line = cart.Add(ProductA, null, 1, null).Value;

        Assert.True(cart.SetQuantity(line.Id, 5).IsSuccess);
        Assert.Equal(5, line.Quantity);
        Assert.Equal(DomainErrors.Cart.QuantityInvalid, cart.SetQuantity(line.Id, 0).Error);

        Assert.True(cart.SetCode(line.Id, " drf-45 ").IsSuccess);
        Assert.Equal("DRF-45", line.AffiliateCode);
        Assert.True(cart.SetCode(line.Id, "  ").IsSuccess);
        Assert.Null(line.AffiliateCode);

        Assert.True(cart.Remove(line.Id).IsSuccess);
        Assert.Empty(cart.Lines);
        Assert.Equal(DomainErrors.Cart.LineNotFound, cart.Remove(line.Id).Error);
    }

    [Fact]
    public void Merge_AddsGuestLines_SumsQuantities_KeepsExistingCode_DropsBadCodes()
    {
        var cart = NewCart();
        cart.Add(ProductA, null, 2, "DRF-45");

        cart.Merge([
            (ProductA, null, 98, "ABC-12"),     // same line: capped at the max, own code kept
            (ProductB, Variant38, 1, "bad code"), // new line, malformed code dropped
            (ProductB, Variant39, 0, null),     // nothing to add
        ]);

        Assert.Equal(2, cart.Lines.Count);
        Assert.Equal(Cart.MaxQuantity, cart.Find(ProductA, null)!.Quantity);
        Assert.Equal("DRF-45", cart.Find(ProductA, null)!.AffiliateCode);
        Assert.Null(cart.Find(ProductB, Variant38)!.AffiliateCode);
    }

    [Fact]
    public void Merge_GivesACodeToALineThatHadNone()
    {
        var cart = NewCart();
        cart.Add(ProductA, null, 1, null);
        cart.Merge([(ProductA, null, 1, "drf45")]);
        Assert.Equal("DRF-45", cart.Lines[0].AffiliateCode);
    }

    [Fact]
    public void Clear_EmptiesTheCart()
    {
        var cart = NewCart();
        cart.Add(ProductA, null, 1, null);
        cart.Clear();
        Assert.Empty(cart.Lines);
    }
}
