using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    public partial class AddVendorNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "vendor_id", table: "notifications", type: "integer", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "vendor_is_read", table: "notifications", type: "boolean",
                nullable: false, defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE notifications AS notification
                SET vendor_id = ticket.vendor_id
                FROM tickets AS ticket
                WHERE notification.ticket_id = ticket.id;

                DO $check$
                BEGIN
                    IF EXISTS (SELECT 1 FROM notifications WHERE vendor_id IS NULL) THEN
                        RAISE EXCEPTION 'Vendor backfill incomplete. Review unmatched notification tickets before continuing.';
                    END IF;
                END;
                $check$;
                """);

            migrationBuilder.AlterColumn<int>(name: "vendor_id", table: "notifications", type: "integer", nullable: false,
                oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.CreateIndex(name: "ix_notifications_vendor_id_created_at_id", table: "notifications",
                columns: new[] { "vendor_id", "created_at", "id" }, descending: new[] { false, true, true });
            migrationBuilder.AddForeignKey(name: "fk_notifications_vendor_profiles_vendor_id", table: "notifications",
                column: "vendor_id", principalTable: "vendor_profiles", principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "fk_notifications_vendor_profiles_vendor_id", table: "notifications");
            migrationBuilder.DropIndex(name: "ix_notifications_vendor_id_created_at_id", table: "notifications");
            migrationBuilder.DropColumn(name: "vendor_is_read", table: "notifications");
            migrationBuilder.DropColumn(name: "vendor_id", table: "notifications");
        }
    }
}
