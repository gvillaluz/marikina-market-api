using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedEntitiesAndCommunityService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "vendor_registration_requests",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "business_id",
                table: "vendor_registration_requests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "community_service_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_id = table.Column<int>(type: "integer", nullable: false),
                    service_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    hours_worked = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    proof_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    recorded_by_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_community_service_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_community_service_logs_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_community_service_logs_users_recorded_by_id",
                        column: x => x.recorded_by_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vendor_registration_requests_business_id",
                table: "vendor_registration_requests",
                column: "business_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_service_logs_recorded_by_id",
                table: "community_service_logs",
                column: "recorded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_service_logs_ticket_id",
                table: "community_service_logs",
                column: "ticket_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_service_logs");

            migrationBuilder.DropIndex(
                name: "ix_vendor_registration_requests_business_id",
                table: "vendor_registration_requests");

            migrationBuilder.AlterColumn<int>(
                name: "type",
                table: "vendor_registration_requests",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "business_id",
                table: "vendor_registration_requests",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);
        }
    }
}
