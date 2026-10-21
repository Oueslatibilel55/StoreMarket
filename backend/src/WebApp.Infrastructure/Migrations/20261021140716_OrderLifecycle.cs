using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrderLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "delivered_at",
                table: "store_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "not_delivered_reason",
                table: "store_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "paid_at",
                table: "store_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "payment_method",
                table: "store_orders",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "payment_status",
                table: "store_orders",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "shipped_at",
                table: "store_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "store_order_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from = table.Column<int>(type: "integer", nullable: false),
                    to = table.Column<int>(type: "integer", nullable: false),
                    actor_role = table.Column<int>(type: "integer", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    not_delivered_reason = table.Column<int>(type: "integer", nullable: true),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    store_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_order_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_store_order_events_store_orders_store_order_id",
                        column: x => x.store_order_id,
                        principalTable: "store_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_store_order_events_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_store_order_events_actor_id",
                table: "store_order_events",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_order_events_store_order_id_at",
                table: "store_order_events",
                columns: new[] { "store_order_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "store_order_events");

            migrationBuilder.DropColumn(
                name: "delivered_at",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "not_delivered_reason",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "paid_at",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "payment_status",
                table: "store_orders");

            migrationBuilder.DropColumn(
                name: "shipped_at",
                table: "store_orders");
        }
    }
}
