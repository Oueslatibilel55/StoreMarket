using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace WebApp.Infrastructure.Persistence;

/// <summary>
/// Shop full-text search (018). Products and stores carry a stored generated <c>search_vector</c> (name weight A,
/// description weight B) with a GIN index. The <c>simple</c> config fits mixed FR / AR / EN text (no stemming);
/// accents are removed on both sides by <c>immutable_unaccent</c>, created by the <c>Search</c> migration
/// (<c>unaccent</c> itself is not immutable, so it can't feed a generated column).
/// </summary>
public static class FullTextSearch
{
    /// <summary>Shadow property holding the vector; read it with <c>EF.Property&lt;NpgsqlTsVector&gt;(e, Vector)</c>.</summary>
    public const string Vector = "SearchVector";
    public const string Config = "simple";

    private const string VectorSql =
        "setweight(to_tsvector('simple', immutable_unaccent(coalesce(name, ''))), 'A') || " +
        "setweight(to_tsvector('simple', immutable_unaccent(coalesce(description, ''))), 'B')";

    /// <summary>The database's <c>immutable_unaccent(text)</c>; only usable inside a query.</summary>
    public static string Unaccent(string text) => throw new NotSupportedException("Database function, only valid in a LINQ query.");

    /// <summary>The entity's text matches <paramref name="tsQuery"/> (built by <c>SearchText.ToTsQuery</c>).</summary>
    internal static Expression<Func<T, bool>> Matches<T>(string tsQuery) where T : class =>
        e => EF.Property<NpgsqlTsVector>(e, Vector).Matches(EF.Functions.ToTsQuery(Config, Unaccent(tsQuery)));

    /// <summary>Relevance (<c>ts_rank</c>): name words weigh more than description words.</summary>
    internal static Expression<Func<T, float>> Rank<T>(string tsQuery) where T : class =>
        e => EF.Property<NpgsqlTsVector>(e, Vector).Rank(EF.Functions.ToTsQuery(Config, Unaccent(tsQuery)));

    internal static void HasSearchVector<T>(this EntityTypeBuilder<T> builder) where T : class
    {
        builder.Property<NpgsqlTsVector>(Vector).HasComputedColumnSql(VectorSql, stored: true);
        builder.HasIndex(Vector).HasMethod("GIN");
    }

    internal static void MapFunctions(ModelBuilder modelBuilder) =>
        modelBuilder.HasDbFunction(typeof(FullTextSearch).GetMethod(nameof(Unaccent))!).HasName("immutable_unaccent").IsBuiltIn();
}
