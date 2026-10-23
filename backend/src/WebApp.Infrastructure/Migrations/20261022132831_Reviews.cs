using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Reviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "average_rating",
                table: "stores",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ranking_score",
                table: "stores",
                type: "numeric(6,4)",
                precision: 6,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rating_count",
                table: "stores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "average_rating",
                table: "products",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ranking_score",
                table: "products",
                type: "numeric(6,4)",
                precision: 6,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rating_count",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target = table.Column<int>(type: "integer", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stars = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    edited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    editable_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reply = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    replied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    hidden_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    hidden_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviews", x => x.id);
                    table.CheckConstraint("ck_reviews_stars", "stars BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_reviews_order_lines_order_line_id",
                        column: x => x.order_line_id,
                        principalTable: "order_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reviews_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reviews_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reviews_store_order_store_order_id",
                        column: x => x.store_order_id,
                        principalTable: "store_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reviews_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reviews_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_products_ranking_score",
                table: "products",
                column: "ranking_score");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_author_id_created_at",
                table: "reviews",
                columns: new[] { "author_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_order_id",
                table: "reviews",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_order_line_id_product",
                table: "reviews",
                column: "order_line_id",
                unique: true,
                filter: "target = 1");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_product_id_created_at",
                table: "reviews",
                columns: new[] { "product_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_store_id_target_created_at",
                table: "reviews",
                columns: new[] { "store_id", "target", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_store_order_id_store",
                table: "reviews",
                column: "store_order_id",
                unique: true,
                filter: "target = 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropIndex(
                name: "ix_products_ranking_score",
                table: "products");

            migrationBuilder.DropColumn(
                name: "average_rating",
                table: "stores");

            migrationBuilder.DropColumn(
                name: "ranking_score",
                table: "stores");

            migrationBuilder.DropColumn(
                name: "rating_count",
                table: "stores");

            migrationBuilder.DropColumn(
                name: "average_rating",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ranking_score",
                table: "products");

            migrationBuilder.DropColumn(
                name: "rating_count",
                table: "products");
        }
    }
}
