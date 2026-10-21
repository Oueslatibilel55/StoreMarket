using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Images : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "image_id",
                table: "store_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<int>(type: "integer", nullable: false),
                    path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    original_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_images", x => x.id);
                    table.ForeignKey(
                        name: "fk_images_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_store_categories_image_id",
                table: "store_categories",
                column: "image_id");

            migrationBuilder.CreateIndex(
                name: "ix_images_path",
                table: "images",
                column: "path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_images_uploaded_by_id_created_at",
                table: "images",
                columns: new[] { "uploaded_by_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_store_categories_images_image_id",
                table: "store_categories",
                column: "image_id",
                principalTable: "images",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_store_categories_images_image_id",
                table: "store_categories");

            migrationBuilder.DropTable(
                name: "images");

            migrationBuilder.DropIndex(
                name: "ix_store_categories_image_id",
                table: "store_categories");

            migrationBuilder.DropColumn(
                name: "image_id",
                table: "store_categories");
        }
    }
}
