using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryApi.Migrations
{
    /// <inheritdoc />
    public partial class AdjustDeactivationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_DeactivatedById",
                table: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_DeactivatedById",
                table: "Users",
                column: "DeactivatedById",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_DeactivatedById",
                table: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_DeactivatedById",
                table: "Users",
                column: "DeactivatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
