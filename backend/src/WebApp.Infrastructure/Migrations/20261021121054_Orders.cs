using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Orders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "order_number_seq");

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<long>(type: "bigint", nullable: false),
                    reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    placed_by_affiliate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    client_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    city = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    coin_value_in_tnd = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    items_total = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    delivery_total = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    total = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_orders_users_client_id",
                        column: x => x.client_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_users_placed_by_affiliate_id",
                        column: x => x.placed_by_affiliate_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "store_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    delivery_fee = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    amount_to_collect = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_store_orders_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_store_orders_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    product_slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    affiliate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    partnership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    affiliate_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    affiliate_percent = table.Column<int>(type: "integer", nullable: true),
                    expected_coins = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: true),
                    store_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    options = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_lines_partnerships_partnership_id",
                        column: x => x.partnership_id,
                        principalTable: "partnerships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_lines_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_lines_store_order_store_order_id",
                        column: x => x.store_order_id,
                        principalTable: "store_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_order_lines_users_affiliate_id",
                        column: x => x.affiliate_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_lines_affiliate_id_created_at",
                table: "order_lines",
                columns: new[] { "affiliate_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_order_lines_partnership_id",
                table: "order_lines",
                column: "partnership_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_lines_product_id",
                table: "order_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_lines_store_order_id",
                table: "order_lines",
                column: "store_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_client_id_created_at",
                table: "orders",
                columns: new[] { "client_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_number",
                table: "orders",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_placed_by_affiliate_id_created_at",
                table: "orders",
                columns: new[] { "placed_by_affiliate_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_reference",
                table: "orders",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_orders_order_id",
                table: "store_orders",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_orders_store_id_status",
                table: "store_orders",
                columns: new[] { "store_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_lines");

            migrationBuilder.DropTable(
                name: "store_orders");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropSequence(
                name: "order_number_seq");
        }
    }
}
