using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EditedReceiptUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "receipt_url",
                table: "tickets");

            migrationBuilder.AddColumn<List<string>>(
                name: "receipt_urls",
                table: "tickets",
                type: "text[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "receipt_urls",
                table: "tickets");

            migrationBuilder.AddColumn<string>(
                name: "receipt_url",
                table: "tickets",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
