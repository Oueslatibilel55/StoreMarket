using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Payouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "payout_number_seq");

            migrationBuilder.AddColumn<Guid>(
                name: "payout_request_id",
                table: "coin_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payout_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    affiliate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coins = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    coin_value_in_tnd = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    amount_tnd = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    account_holder = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    rib = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    scheduled_for = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    place = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paid_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payout_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_payout_requests_users_affiliate_id",
                        column: x => x.affiliate_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payout_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payout_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payout_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_payout_events_payout_requests_payout_request_id",
                        column: x => x.payout_request_id,
                        principalTable: "payout_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_payout_events_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_coin_entries_payout_request_id_type",
                table: "coin_entries",
                columns: new[] { "payout_request_id", "type" },
                unique: true,
                filter: "payout_request_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payout_events_actor_id",
                table: "payout_events",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_payout_events_payout_request_id_at",
                table: "payout_events",
                columns: new[] { "payout_request_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_payout_requests_affiliate_id_created_at",
                table: "payout_requests",
                columns: new[] { "affiliate_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payout_requests_affiliate_id_open",
                table: "payout_requests",
                column: "affiliate_id",
                unique: true,
                filter: "status IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "ix_payout_requests_reference",
                table: "payout_requests",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payout_requests_status_scheduled_for",
                table: "payout_requests",
                columns: new[] { "status", "scheduled_for" });

            migrationBuilder.AddForeignKey(
                name: "fk_coin_entries_payout_requests_payout_request_id",
                table: "coin_entries",
                column: "payout_request_id",
                principalTable: "payout_requests",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_coin_entries_payout_requests_payout_request_id",
                table: "coin_entries");

            migrationBuilder.DropTable(
                name: "payout_events");

            migrationBuilder.DropTable(
                name: "payout_requests");

            migrationBuilder.DropIndex(
                name: "ix_coin_entries_payout_request_id_type",
                table: "coin_entries");

            migrationBuilder.DropColumn(
                name: "payout_request_id",
                table: "coin_entries");

            migrationBuilder.DropSequence(
                name: "payout_number_seq");
        }
    }
}
