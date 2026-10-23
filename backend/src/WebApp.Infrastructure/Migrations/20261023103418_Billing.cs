using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Billing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "invoice_id",
                table: "coin_entries");

            migrationBuilder.CreateSequence(
                name: "commission_invoice_number_seq");

            migrationBuilder.CreateSequence(
                name: "store_payment_number_seq");

            migrationBuilder.AddColumn<DateOnly>(
                name: "paid_until",
                table: "stores",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateTime>(
                name: "suspended_at",
                table: "stores",
                type: "timestamp with time zone",
                nullable: true);

            // Billing starts now: existing stores get the 30 trial days from the deploy, so none is suspended right away.
            migrationBuilder.Sql("UPDATE stores SET paid_until = CURRENT_DATE + 30;");

            migrationBuilder.CreateTable(
                name: "billing_offers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_fr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    months = table.Column<int>(type: "integer", nullable: true),
                    discount_percent = table.Column<int>(type: "integer", nullable: true),
                    discount_amount = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: true),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    target = table.Column<int>(type: "integer", nullable: false),
                    target_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    first_payment_only = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_billing_offers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "commission_invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    month = table.Column<DateOnly>(type: "date", nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    total = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commission_invoices", x => x.id);
                    table.ForeignKey(
                        name: "fk_commission_invoices_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "commission_invoice_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    coin_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    credited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    order_reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    product_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true),
                    line_total = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: true),
                    affiliate_percent = table.Column<int>(type: "integer", nullable: true),
                    affiliate_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    coins = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    coin_value_in_tnd = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commission_invoice_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_commission_invoice_lines_coin_entries_coin_entry_id",
                        column: x => x.coin_entry_id,
                        principalTable: "coin_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_commission_invoice_lines_commission_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "commission_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: false),
                    receipt_image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    months = table.Column<int>(type: "integer", nullable: true),
                    monthly_price = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    offer_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    discount = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    covers_from = table.Column<DateOnly>(type: "date", nullable: true),
                    covers_to = table.Column<DateOnly>(type: "date", nullable: true),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    declared_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_store_payments_billing_offers_offer_id",
                        column: x => x.offer_id,
                        principalTable: "billing_offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_store_payments_commission_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "commission_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_store_payments_images_receipt_image_id",
                        column: x => x.receipt_image_id,
                        principalTable: "images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_store_payments_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_store_payments_users_decided_by_id",
                        column: x => x.decided_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_store_payments_users_declared_by_id",
                        column: x => x.declared_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_commission_invoice_lines_coin_entry_id",
                table: "commission_invoice_lines",
                column: "coin_entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_commission_invoice_lines_invoice_id",
                table: "commission_invoice_lines",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_commission_invoices_reference",
                table: "commission_invoices",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_commission_invoices_status_due_on",
                table: "commission_invoices",
                columns: new[] { "status", "due_on" });

            migrationBuilder.CreateIndex(
                name: "ix_commission_invoices_store_id_month",
                table: "commission_invoices",
                columns: new[] { "store_id", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_payments_decided_by_id",
                table: "store_payments",
                column: "decided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_payments_declared_by_id",
                table: "store_payments",
                column: "declared_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_payments_invoice_id",
                table: "store_payments",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_payments_number",
                table: "store_payments",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_payments_offer_id",
                table: "store_payments",
                column: "offer_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_payments_receipt_image_id",
                table: "store_payments",
                column: "receipt_image_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_payments_status_created_at",
                table: "store_payments",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_store_payments_store_id_created_at",
                table: "store_payments",
                columns: new[] { "store_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commission_invoice_lines");

            migrationBuilder.DropTable(
                name: "store_payments");

            migrationBuilder.DropTable(
                name: "billing_offers");

            migrationBuilder.DropTable(
                name: "commission_invoices");

            migrationBuilder.DropColumn(
                name: "paid_until",
                table: "stores");

            migrationBuilder.DropColumn(
                name: "suspended_at",
                table: "stores");

            migrationBuilder.DropSequence(
                name: "commission_invoice_number_seq");

            migrationBuilder.DropSequence(
                name: "store_payment_number_seq");

            migrationBuilder.AddColumn<Guid>(
                name: "invoice_id",
                table: "coin_entries",
                type: "uuid",
                nullable: true);
        }
    }
}
