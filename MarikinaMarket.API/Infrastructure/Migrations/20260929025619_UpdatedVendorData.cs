using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedVendorData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_vendor_profiles_market_section_id_stall_number",
                table: "vendor_profiles");

            migrationBuilder.AddColumn<string>(
                name: "business_id",
                table: "vendor_registration_requests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "business_id",
                table: "vendor_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_profiles_business_id",
                table: "vendor_profiles",
                column: "business_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vendor_profiles_market_section_id",
                table: "vendor_profiles",
                column: "market_section_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_vendor_profiles_business_id",
                table: "vendor_profiles");

            migrationBuilder.DropIndex(
                name: "ix_vendor_profiles_market_section_id",
                table: "vendor_profiles");

            migrationBuilder.DropColumn(
                name: "business_id",
                table: "vendor_registration_requests");

            migrationBuilder.DropColumn(
                name: "business_id",
                table: "vendor_profiles");

            migrationBuilder.CreateIndex(
                name: "ix_vendor_profiles_market_section_id_stall_number",
                table: "vendor_profiles",
                columns: new[] { "market_section_id", "stall_number" },
                unique: true);
        }
    }
}
