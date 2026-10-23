using WebApp.Application.Common;

namespace WebApp.UnitTests;

public sealed class AttributeFilterTests
{
    [Fact]
    public void Parse_FieldAndChoices()
    {
        var filters = AttributeFilter.Parse(["couleur:noir, rouge,noir", "bio:true"]);

        Assert.Equal(2, filters.Count);
        Assert.Equal("couleur", filters[0].Key);
        Assert.Equal(["noir", "rouge"], filters[0].Values);
        Assert.Equal(["true"], filters[1].Values);
    }

    [Fact]
    public void Parse_SkipsMalformed_AndRepeatedFields()
    {
        var filters = AttributeFilter.Parse(["", "noir", ":noir", "couleur:", "couleur:,", "taille:m", "taille:l"]);

        var only = Assert.Single(filters);
        Assert.Equal("taille", only.Key);
        Assert.Equal(["m"], only.Values);
    }

    [Fact]
    public void Parse_IsCapped()
    {
        var many = Enumerable.Range(0, 30).Select(i => $"f{i}:" + string.Join(',', Enumerable.Range(0, 50).Select(j => $"v{j}")));

        var filters = AttributeFilter.Parse(many);

        Assert.Equal(AttributeFilter.MaxFilters, filters.Count);
        Assert.All(filters, f => Assert.Equal(AttributeFilter.MaxValues, f.Values.Count));
        Assert.Empty(AttributeFilter.Parse(null));
    }
}
