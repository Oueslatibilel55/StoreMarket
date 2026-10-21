using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoinLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "coin_accounts",
                columns: table => new
                {
                    affiliate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance = table.Column<decimal>(type: "numeric(16,3)", precision: 16, scale: 3, nullable: false),
                    total_earned = table.Column<decimal>(type: "numeric(16,3)", precision: 16, scale: 3, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coin_accounts", x => x.affiliate_id);
                    table.ForeignKey(
                        name: "fk_coin_accounts_users_affiliate_id",
                        column: x => x.affiliate_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coin_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    affiliate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    coins = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    store_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    client_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    client_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    store_id = table.Column<Guid>(type: "uuid", nullable: true),
                    store_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true),
                    line_total = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: true),
                    affiliate_percent = table.Column<int>(type: "integer", nullable: true),
                    coin_value_in_tnd = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    related_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coin_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_coin_entries_coin_entries_related_entry_id",
                        column: x => x.related_entry_id,
                        principalTable: "coin_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_coin_entries_order_line_order_line_id",
                        column: x => x.order_line_id,
                        principalTable: "order_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_coin_entries_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_coin_entries_users_affiliate_id",
                        column: x => x.affiliate_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_coin_entries_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_coin_entries_affiliate_id_created_at",
                table: "coin_entries",
                columns: new[] { "affiliate_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_coin_entries_created_by_id",
                table: "coin_entries",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_coin_entries_order_id",
                table: "coin_entries",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_coin_entries_order_line_id_type",
                table: "coin_entries",
                columns: new[] { "order_line_id", "type" },
                unique: true,
                filter: "order_line_id IS NOT NULL AND type = 1");

            migrationBuilder.CreateIndex(
                name: "ix_coin_entries_related_entry_id",
                table: "coin_entries",
                column: "related_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_coin_entries_store_id_created_at",
                table: "coin_entries",
                columns: new[] { "store_id", "created_at" });

            // Orders delivered before the ledger existed (016 shipped first): credit their affiliate lines now, with the
            // values the order line froze at checkout, then build the accounts from the entries.
            migrationBuilder.Sql("""
                INSERT INTO coin_entries (id, created_at, updated_at, affiliate_id, type, coins, order_id, store_order_id, order_line_id,
                    order_reference, client_user_id, client_name, client_phone, product_id, product_name, store_id, store_name,
                    quantity, line_total, affiliate_percent, coin_value_in_tnd)
                SELECT gen_random_uuid(), COALESCE(so.delivered_at, now()), now(), l.affiliate_id, 1, l.expected_coins, o.id, so.id, l.id,
                    o.reference, o.client_id, o.client_name, o.client_phone, l.product_id, l.product_name, so.store_id, so.store_name,
                    l.quantity, l.line_total, l.affiliate_percent, o.coin_value_in_tnd
                FROM order_lines l
                JOIN store_orders so ON so.id = l.store_order_id
                JOIN orders o ON o.id = so.order_id
                WHERE so.status = 4 AND l.affiliate_id IS NOT NULL AND l.expected_coins > 0
                  AND NOT EXISTS (SELECT 1 FROM coin_entries e WHERE e.order_line_id = l.id AND e.type = 1);

                INSERT INTO coin_accounts (affiliate_id, balance, total_earned, updated_at)
                SELECT affiliate_id, SUM(coins), SUM(CASE WHEN type = 1 THEN coins ELSE 0 END), now()
                FROM coin_entries GROUP BY affiliate_id
                ON CONFLICT (affiliate_id) DO UPDATE SET balance = EXCLUDED.balance, total_earned = EXCLUDED.total_earned, updated_at = now();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coin_accounts");

            migrationBuilder.DropTable(
                name: "coin_entries");
        }
    }
}
