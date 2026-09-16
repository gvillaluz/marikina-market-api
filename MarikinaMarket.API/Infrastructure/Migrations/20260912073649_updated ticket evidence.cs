using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class updatedticketevidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "evidence_url",
                table: "ticket_evidences",
                newName: "file_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "file_key",
                table: "ticket_evidences",
                newName: "evidence_url");
        }
    }
}
