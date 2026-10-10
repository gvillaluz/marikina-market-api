using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    public partial class AddAuditLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    module = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    result = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table => table.PrimaryKey("pk_audit_logs", x => x.id));

            migrationBuilder.CreateIndex("ix_audit_logs_timestamp_id", "audit_logs", new[] { "timestamp", "id" });
            migrationBuilder.CreateIndex("ix_audit_logs_user_id_timestamp", "audit_logs", new[] { "user_id", "timestamp" });
            migrationBuilder.CreateIndex("ix_audit_logs_module_timestamp", "audit_logs", new[] { "module", "timestamp" });
            migrationBuilder.CreateIndex("ix_audit_logs_result_timestamp", "audit_logs", new[] { "result", "timestamp" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.DropTable(name: "audit_logs");
    }
}
