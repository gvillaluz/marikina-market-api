using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    public partial class UpdateRolesAndAssignHeadAdmin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM ""AspNetUsers"" WHERE id = 3) THEN
                        RAISE EXCEPTION 'User ID 3 does not exist. No role changes were applied.';
                    END IF;
                END $$;");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "AdminOfficer", "ADMINOFFICER" });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "MarketEnforcer", "MARKETENFORCER" });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "MarketVendor", "MARKETVENDOR" });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "id", "name", "normalized_name", "concurrency_stamp" },
                values: new object[]
                {
                    4, "HeadAdmin", "HEADADMIN", "d05e6f7a-8b9c-0d1e-2f3a-4b5c6d7e8f90"
                });

            migrationBuilder.Sql(@"
                DELETE FROM ""AspNetUserRoles"" WHERE user_id = 3;
                INSERT INTO ""AspNetUserRoles"" (user_id, role_id) VALUES (3, 4);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The previous role assignments of user 3 are runtime data, not model seed data.
            // Do not invent assignments when rolling back this administrator promotion.
            throw new NotSupportedException(
                "Automatic rollback is unavailable because user ID 3's previous role assignments are unknown. " +
                "Restore those assignments and the previous role names through an explicit data migration.");
        }
    }
}
