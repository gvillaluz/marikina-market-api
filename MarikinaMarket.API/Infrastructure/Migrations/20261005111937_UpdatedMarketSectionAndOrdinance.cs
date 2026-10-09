using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedMarketSectionAndOrdinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE vendor_registration_requests " +
                "ADD COLUMN IF NOT EXISTS review_reason character varying(150) NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE vendor_registration_requests " +
                "ADD COLUMN IF NOT EXISTS review_remarks character varying(2000) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "review_reason",
                table: "vendor_registration_requests");

            migrationBuilder.DropColumn(
                name: "review_remarks",
                table: "vendor_registration_requests");
        }
    }
}
