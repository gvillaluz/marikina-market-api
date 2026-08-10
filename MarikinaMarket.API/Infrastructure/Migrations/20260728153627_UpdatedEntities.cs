using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "captured_at",
                table: "ticket_evidences");

            migrationBuilder.AddColumn<int>(
                name: "market_section_id",
                table: "vendor_registration_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "stall_number",
                table: "vendor_registration_requests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_payment_amount",
                table: "tickets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "penalty_type",
                table: "tickets",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "payment_status",
                table: "tickets",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "highest_severity",
                table: "tickets",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "penalty_amount",
                table: "ticket_violations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "ordinances",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Enforcer", "ENFORCER" });

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 1,
                column: "code",
                value: "Market Code");

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 2,
                column: "code",
                value: "Peace & Order Code");

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 3,
                column: "code",
                value: "Market I.D.");

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 4,
                column: "code",
                value: "Market Code");

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 5,
                column: "code",
                value: "Market Code");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_registration_requests_market_section_id",
                table: "vendor_registration_requests",
                column: "market_section_id");

            migrationBuilder.AddForeignKey(
                name: "fk_vendor_registration_requests_market_sections_market_section",
                table: "vendor_registration_requests",
                column: "market_section_id",
                principalTable: "market_sections",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vendor_registration_requests_market_sections_market_section",
                table: "vendor_registration_requests");

            migrationBuilder.DropIndex(
                name: "ix_vendor_registration_requests_market_section_id",
                table: "vendor_registration_requests");

            migrationBuilder.DropColumn(
                name: "market_section_id",
                table: "vendor_registration_requests");

            migrationBuilder.DropColumn(
                name: "stall_number",
                table: "vendor_registration_requests");

            migrationBuilder.DropColumn(
                name: "code",
                table: "ordinances");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_payment_amount",
                table: "tickets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "penalty_type",
                table: "tickets",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "payment_status",
                table: "tickets",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "highest_severity",
                table: "tickets",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "penalty_amount",
                table: "ticket_violations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "captured_at",
                table: "ticket_evidences",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Inspector", "INSPECTOR" });
        }
    }
}
