using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Stores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    city = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    logo_image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    banner_image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    default_affiliate_percent = table.Column<int>(type: "integer", nullable: false),
                    delivery_fee = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    delivery_cities = table.Column<string[]>(type: "text[]", nullable: false),
                    delivery_min_days = table.Column<int>(type: "integer", nullable: false),
                    delivery_max_days = table.Column<int>(type: "integer", nullable: false),
                    facebook_url = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    instagram_url = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    website_url = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    subscription_monthly_price = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stores", x => x.id);
                    table.ForeignKey(
                        name: "fk_stores_images_banner_image_id",
                        column: x => x.banner_image_id,
                        principalTable: "images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stores_images_logo_image_id",
                        column: x => x.logo_image_id,
                        principalTable: "images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stores_store_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "store_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stores_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stores_banner_image_id",
                table: "stores",
                column: "banner_image_id");

            migrationBuilder.CreateIndex(
                name: "ix_stores_category_id",
                table: "stores",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_stores_logo_image_id",
                table: "stores",
                column: "logo_image_id");

            migrationBuilder.CreateIndex(
                name: "ix_stores_owner_id",
                table: "stores",
                column: "owner_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stores_slug",
                table: "stores",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stores_status_category_id",
                table: "stores",
                columns: new[] { "status", "category_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stores");
        }
    }
}
