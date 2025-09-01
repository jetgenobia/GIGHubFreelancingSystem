using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freelancing.Migrations
{
    /// <inheritdoc />
    public partial class updaters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Portfolios_PortfolioId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PortfolioId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PortfolioId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Portfolios",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Portfolios_AspNetUsers_UserAccountId",
                table: "Portfolios");

            migrationBuilder.DropIndex(
                name: "IX_Portfolios_UserAccountId",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "UserAccountId",
                table: "Portfolios");

            migrationBuilder.AddColumn<Guid>(
                name: "PortfolioId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PortfolioId",
                table: "AspNetUsers",
                column: "PortfolioId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Portfolios_PortfolioId",
                table: "AspNetUsers",
                column: "PortfolioId",
                principalTable: "Portfolios",
                principalColumn: "Id");
        }
    }
}
