using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedBackups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backup_schedules",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    frequency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    day_of_week = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    retention_days = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_attempt_scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_backup_schedules", x => x.id);
                    table.CheckConstraint("ck_backup_schedule_frequency", "(frequency = 'Daily' AND day_of_week IS NULL) OR (frequency = 'Weekly' AND day_of_week IS NOT NULL AND day_of_week IN ('Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'))");
                    table.CheckConstraint("ck_backup_schedule_retention", "retention_days BETWEEN 1 AND 365");
                    table.CheckConstraint("ck_backup_schedule_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "backups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    b2key = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_finalized = table.Column<bool>(type: "boolean", nullable: false),
                    storage_cleanup_pending = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_backups", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_backups_b2key",
                table: "backups",
                column: "b2key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_backups_date_time",
                table: "backups",
                column: "date_time");

            migrationBuilder.CreateIndex(
                name: "ix_backups_is_finalized_status_expires_at",
                table: "backups",
                columns: new[] { "is_finalized", "status", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backup_schedules");

            migrationBuilder.DropTable(
                name: "backups");
        }
    }
}
