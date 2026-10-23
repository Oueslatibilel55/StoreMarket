using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace WebApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Search : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            // unaccent() is only STABLE (its dictionary can change): a generated column needs an IMMUTABLE wrapper.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION immutable_unaccent(text) RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
                AS $$ SELECT public.unaccent('public.unaccent'::regdictionary, $1) $$;
                """);

            migrationBuilder.AddColumn<decimal>(
                name: "from_price",
                table: "products",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            // Same rule as Product.FromPrice: the cheapest variant (its own price, else the product's), else the price.
            migrationBuilder.Sql("""
                UPDATE products p SET from_price = coalesce(
                    (SELECT min(coalesce(v.price, p.price)) FROM product_variants v WHERE v.product_id = p.id),
                    p.price);
                """);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "stores",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector('simple', immutable_unaccent(coalesce(name, ''))), 'A') || setweight(to_tsvector('simple', immutable_unaccent(coalesce(description, ''))), 'B')",
                stored: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "products",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector('simple', immutable_unaccent(coalesce(name, ''))), 'A') || setweight(to_tsvector('simple', immutable_unaccent(coalesce(description, ''))), 'B')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_stores_search_vector",
                table: "stores",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_products_from_price",
                table: "products",
                column: "from_price");

            migrationBuilder.CreateIndex(
                name: "ix_products_search_vector",
                table: "products",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_stores_search_vector",
                table: "stores");

            migrationBuilder.DropIndex(
                name: "ix_products_from_price",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_search_vector",
                table: "products");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "stores");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "products");

            migrationBuilder.DropColumn(
                name: "from_price",
                table: "products");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS immutable_unaccent(text);");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");
        }
    }
}
