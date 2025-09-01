using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freelancing.Migrations
{
    /// <inheritdoc />
    public partial class updaterelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Portfolios_AspNetUsers_UserAccountId",
                table: "Portfolios");

            migrationBuilder.DropIndex(
                name: "IX_Portfolios_UserAccountId",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "UserAccountId",
                table: "Portfolios");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserAccountId",
                table: "Portfolios",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_UserAccountId",
                table: "Portfolios",
                column: "UserAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Portfolios_AspNetUsers_UserAccountId",
                table: "Portfolios",
                column: "UserAccountId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
