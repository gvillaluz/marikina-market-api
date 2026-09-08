using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedUserEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "user_id1",
                table: "user_device_tokens",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_device_tokens_user_id1",
                table: "user_device_tokens",
                column: "user_id1");

            migrationBuilder.AddForeignKey(
                name: "fk_user_device_tokens_asp_net_users_user_id1",
                table: "user_device_tokens",
                column: "user_id1",
                principalTable: "AspNetUsers",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_device_tokens_asp_net_users_user_id1",
                table: "user_device_tokens");

            migrationBuilder.DropIndex(
                name: "ix_user_device_tokens_user_id1",
                table: "user_device_tokens");

            migrationBuilder.DropColumn(
                name: "user_id1",
                table: "user_device_tokens");
        }
    }
}
