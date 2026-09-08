using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "tickets",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    enforcer_id = table.Column<int>(type: "integer", nullable: false),
                    ticket_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_users_enforcer_id",
                        column: x => x.enforcer_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 1,
                column: "severity",
                value: "Minor");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 2,
                column: "severity",
                value: "Moderate");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 5,
                column: "severity",
                value: "Minor");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 6,
                column: "severity",
                value: "Moderate");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 9,
                column: "severity",
                value: "Minor");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 10,
                column: "severity",
                value: "Moderate");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 13,
                column: "severity",
                value: "Moderate");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 16,
                column: "severity",
                value: "Moderate");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_enforcer_id",
                table: "notifications",
                column: "enforcer_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_ticket_id",
                table: "notifications",
                column: "ticket_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "tickets",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 1,
                column: "severity",
                value: "Low");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 2,
                column: "severity",
                value: "Medium");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 5,
                column: "severity",
                value: "Low");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 6,
                column: "severity",
                value: "Medium");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 9,
                column: "severity",
                value: "Low");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 10,
                column: "severity",
                value: "Medium");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 13,
                column: "severity",
                value: "Medium");

            migrationBuilder.UpdateData(
                table: "ordinance_penalty_tiers",
                keyColumn: "id",
                keyValue: 16,
                column: "severity",
                value: "Medium");
        }
    }
}
