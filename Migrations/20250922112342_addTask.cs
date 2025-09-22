using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freelancing.Migrations
{
    /// <inheritdoc />
    public partial class addTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTaskAssigned",
                table: "MentorSessionNotes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TaskDescription",
                table: "MentorSessionNotes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskTitle",
                table: "MentorSessionNotes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTaskAssigned",
                table: "MentorSessionNotes");

            migrationBuilder.DropColumn(
                name: "TaskDescription",
                table: "MentorSessionNotes");

            migrationBuilder.DropColumn(
                name: "TaskTitle",
                table: "MentorSessionNotes");
        }
    }
}
