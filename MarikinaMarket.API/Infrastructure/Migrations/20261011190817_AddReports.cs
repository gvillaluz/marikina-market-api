using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    from = table.Column<DateOnly>(type: "date", nullable: false),
                    to = table.Column<DateOnly>(type: "date", nullable: false),
                    date_generated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    generated_by_id = table.Column<int>(type: "integer", nullable: false),
                    generated_by_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    warning_records = table.Column<int>(type: "integer", nullable: false),
                    resolved_ticket_records = table.Column<int>(type: "integer", nullable: false),
                    pending_ticket_records = table.Column<int>(type: "integer", nullable: false),
                    resolved_ticket_penalties = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    pending_ticket_penalties = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_reports_asp_net_users_generated_by_id",
                        column: x => x.generated_by_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reports_date_generated_id",
                table: "reports",
                columns: new[] { "date_generated", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_generated_by_id",
                table: "reports",
                column: "generated_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reports");
        }
    }
}
