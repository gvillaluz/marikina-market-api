using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedTicketEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_vendor_id_primary_category_issued_at",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "primary_category",
                table: "tickets");

            migrationBuilder.AlterColumn<string>(
                name: "payment_status",
                table: "tickets",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "categories",
                table: "tickets",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_vendor_id_categories_issued_at",
                table: "tickets",
                columns: new[] { "vendor_id", "categories", "issued_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_vendor_id_categories_issued_at",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "categories",
                table: "tickets");

            migrationBuilder.AlterColumn<string>(
                name: "payment_status",
                table: "tickets",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "primary_category",
                table: "tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_vendor_id_primary_category_issued_at",
                table: "tickets",
                columns: new[] { "vendor_id", "primary_category", "issued_at" });
        }
    }
}
