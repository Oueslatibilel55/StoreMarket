using WebApp.Application.Common;

namespace WebApp.UnitTests;

public sealed class SearchTextTests
{
    [Theory]
    [InlineData("parf", "parf:*")]
    [InlineData("  Crème   HYDRATANTE ", "crème:* & hydratante:*")]
    [InlineData("l'huile d'olive", "huile:* & olive:*")]
    [InlineData("x", "x:*")]
    [InlineData("pack 3 x", "pack:* & 3:*")]
    [InlineData("عطر رجالي", "عطر:* & رجالي:*")]
    [InlineData("100ml", "100ml:*")]
    [InlineData("savon savon", "savon:*")]
    public void Words_BecomePrefixes_AllRequired(string input, string expected) =>
        Assert.Equal(expected, SearchText.ToTsQuery(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! & | :* ()")]
    [InlineData("َ")]
    public void NothingSearchable_IsNull(string? input) => Assert.Null(SearchText.ToTsQuery(input));

    [Fact]
    public void TsQueryOperators_AreNeverPassedThrough() =>
        Assert.Equal("ab:* & cd:*", SearchText.ToTsQuery("ab & !cd | :*"));

    [Fact]
    public void WordsAndLength_AreCapped()
    {
        var query = SearchText.ToTsQuery(string.Join(' ', Enumerable.Range(1, 20).Select(i => $"w{i}")))!;
        Assert.Equal(SearchText.MaxWords, query.Split(" & ").Length);

        Assert.Equal(new string('x', SearchText.MaxWordLength) + ":*", SearchText.ToTsQuery(new string('x', 100)));
    }
}
