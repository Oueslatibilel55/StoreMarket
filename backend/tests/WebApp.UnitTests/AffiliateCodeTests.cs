using System.Globalization;
using WebApp.Application.Services;
using WebApp.Domain.Common;

namespace WebApp.UnitTests;

public sealed class AffiliateCodeTests
{
    [Theory]
    [InlineData("#DRF-45", "DRF-45")]
    [InlineData("drf-45", "DRF-45")]
    [InlineData("DRF45", "DRF-45")]
    [InlineData("  drf 45 ", "DRF-45")]
    [InlineData("abc-12345", "ABC-12345")]
    [InlineData("dr-45", "DR-45")]
    [InlineData("#abcde-123456", "ABCDE-123456")]
    public void Normalize_AcceptsUserFormats(string input, string expected) =>
        Assert.Equal(expected, AffiliateCode.Normalize(input));

    [Theory]
    [InlineData("D-45")]
    [InlineData("ABCDEF-45")]
    [InlineData("DRF-4")]
    [InlineData("DRF-1234567")]
    [InlineData("123-45")]
    [InlineData("")]
    public void Normalize_RejectsOtherShapes(string input) => Assert.Null(AffiliateCode.Normalize(input));

    [Fact]
    public void Create_ProducesValidCodesWithoutAmbiguousLetters()
    {
        for (var i = 0; i < 500; i++)
        {
            var code = AffiliateCodeGenerator.Create(digits: 2);

            Assert.True(AffiliateCode.IsValid(code), code);
            Assert.DoesNotContain('I', code[..3]);
            Assert.DoesNotContain('O', code[..3]);
            Assert.InRange(int.Parse(code[4..], CultureInfo.InvariantCulture), 10, 99);
        }
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 6)]
    public void Create_FollowsTheConfiguredShape(int letters, int digits)
    {
        var code = AffiliateCodeGenerator.Create(digits, letters);

        Assert.True(AffiliateCode.IsValid(code), code);
        var parts = code.Split('-');
        Assert.Equal(letters, parts[0].Length);
        Assert.Equal(digits, parts[1].Length);
    }

    [Fact]
    public void Create_OutsideTheBounds_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AffiliateCodeGenerator.Create(digits: 2, letterCount: 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => AffiliateCodeGenerator.Create(digits: 1));
    }
}
