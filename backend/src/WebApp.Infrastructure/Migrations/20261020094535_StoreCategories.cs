using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StoreCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "store_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_fr = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    name_en = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    icon = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_categories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_store_categories_is_active_sort_order",
                table: "store_categories",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_store_categories_slug",
                table: "store_categories",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "store_categories");
        }
    }
}
