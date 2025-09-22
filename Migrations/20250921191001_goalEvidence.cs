using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freelancing.Migrations
{
    /// <inheritdoc />
    public partial class goalEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CompletionType",
                table: "MentorshipGoalCompletions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<Guid>(
                name: "MenteeEvidenceId",
                table: "MentorshipGoalCompletions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MentorNoteId",
                table: "MentorshipGoalCompletions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MenteeSessionEvidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentorshipMatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    WhatWasDone = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AdditionalNotes = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    EvidenceFilePaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenteeSessionEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenteeSessionEvidences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MenteeSessionEvidences_Goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "Goals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MenteeSessionEvidences_MentorshipMatches_MentorshipMatchId",
                        column: x => x.MentorshipMatchId,
                        principalTable: "MentorshipMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MentorSessionNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentorshipMatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Feedback = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProgressRating = table.Column<int>(type: "int", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorSessionNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorSessionNotes_AspNetUsers_MentorId",
                        column: x => x.MentorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MentorSessionNotes_Goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "Goals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MentorSessionNotes_MentorshipMatches_MentorshipMatchId",
                        column: x => x.MentorshipMatchId,
                        principalTable: "MentorshipMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipGoalCompletions_MenteeEvidenceId",
                table: "MentorshipGoalCompletions",
                column: "MenteeEvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipGoalCompletions_MentorNoteId",
                table: "MentorshipGoalCompletions",
                column: "MentorNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_MenteeSessionEvidences_GoalId",
                table: "MenteeSessionEvidences",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_MenteeSessionEvidences_MentorshipMatchId_GoalId_UserId",
                table: "MenteeSessionEvidences",
                columns: new[] { "MentorshipMatchId", "GoalId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenteeSessionEvidences_SubmittedAt",
                table: "MenteeSessionEvidences",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MenteeSessionEvidences_UserId",
                table: "MenteeSessionEvidences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorSessionNotes_GoalId",
                table: "MentorSessionNotes",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorSessionNotes_MentorId",
                table: "MentorSessionNotes",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorSessionNotes_MentorshipMatchId_GoalId_MentorId",
                table: "MentorSessionNotes",
                columns: new[] { "MentorshipMatchId", "GoalId", "MentorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorSessionNotes_SubmittedAt",
                table: "MentorSessionNotes",
                column: "SubmittedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_MentorshipGoalCompletions_MenteeSessionEvidences_MenteeEvidenceId",
                table: "MentorshipGoalCompletions",
                column: "MenteeEvidenceId",
                principalTable: "MenteeSessionEvidences",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MentorshipGoalCompletions_MentorSessionNotes_MentorNoteId",
                table: "MentorshipGoalCompletions",
                column: "MentorNoteId",
                principalTable: "MentorSessionNotes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorshipGoalCompletions_MenteeSessionEvidences_MenteeEvidenceId",
                table: "MentorshipGoalCompletions");

            migrationBuilder.DropForeignKey(
                name: "FK_MentorshipGoalCompletions_MentorSessionNotes_MentorNoteId",
                table: "MentorshipGoalCompletions");

            migrationBuilder.DropTable(
                name: "MenteeSessionEvidences");

            migrationBuilder.DropTable(
                name: "MentorSessionNotes");

            migrationBuilder.DropIndex(
                name: "IX_MentorshipGoalCompletions_MenteeEvidenceId",
                table: "MentorshipGoalCompletions");

            migrationBuilder.DropIndex(
                name: "IX_MentorshipGoalCompletions_MentorNoteId",
                table: "MentorshipGoalCompletions");

            migrationBuilder.DropColumn(
                name: "MenteeEvidenceId",
                table: "MentorshipGoalCompletions");

            migrationBuilder.DropColumn(
                name: "MentorNoteId",
                table: "MentorshipGoalCompletions");

            migrationBuilder.AlterColumn<string>(
                name: "CompletionType",
                table: "MentorshipGoalCompletions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
