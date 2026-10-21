using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AffiliateCodeAndBlocking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "affiliate_code",
                table: "users",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "affiliate_terms_accepted_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "blocked_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "blocked_reason",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_affiliate_code",
                table: "users",
                column: "affiliate_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_affiliate_code",
                table: "users");

            migrationBuilder.DropColumn(
                name: "affiliate_code",
                table: "users");

            migrationBuilder.DropColumn(
                name: "affiliate_terms_accepted_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "blocked_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "blocked_reason",
                table: "users");
        }
    }
}
