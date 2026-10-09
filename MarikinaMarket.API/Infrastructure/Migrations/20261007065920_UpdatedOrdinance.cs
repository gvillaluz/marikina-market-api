using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedOrdinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "code",
                table: "ordinances",
                newName: "series");

            migrationBuilder.AddColumn<string>(
                name: "market_code",
                table: "ordinances",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "market_code", "series" },
                values: new object[] { "Market Code", "2014" });

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "market_code", "series" },
                values: new object[] { "Peace & Order Code", "2006" });

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "market_code", "series" },
                values: new object[] { "Market I.D.", "2007" });

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "market_code", "series" },
                values: new object[] { "Market Code", "2014" });

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "market_code", "series" },
                values: new object[] { "Market Code", "2014" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "market_code",
                table: "ordinances");

            migrationBuilder.RenameColumn(
                name: "series",
                table: "ordinances",
                newName: "code");

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 1,
                column: "code",
                value: "Market Code");

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 2,
                column: "code",
                value: "Peace & Order Code");

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 3,
                column: "code",
                value: "Market I.D.");

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 4,
                column: "code",
                value: "Market Code");

            migrationBuilder.UpdateData(
                table: "ordinances",
                keyColumn: "id",
                keyValue: 5,
                column: "code",
                value: "Market Code");
        }
    }
}
