using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Freelancing.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSQL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Photo = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: false),
                    FRole = table.Column<string>(type: "text", nullable: false),
                    Bio = table.Column<string>(type: "text", nullable: true),
                    ExperienceLevel = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletionReason = table.Column<string>(type: "text", nullable: true),
                    MentorshipId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContractTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    TemplateContent = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreviewImagePath = table.Column<string>(type: "text", nullable: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    TemplateVersion = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Goals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GoalDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IconSvg = table.Column<string>(type: "text", nullable: true),
                    IsCustom = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: true),
                    TargetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    SuccessCriteria = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HiringOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreelancerId = table.Column<string>(type: "text", nullable: false),
                    WasSuccessful = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiringOutcomes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSkills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdentityVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<string>(type: "text", nullable: false),
                    IdDocumentType = table.Column<string>(type: "text", nullable: true),
                    IdDocumentExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IdDocumentVerified = table.Column<bool>(type: "boolean", nullable: true),
                    IdDocumentConfidence = table.Column<float>(type: "real", nullable: true),
                    FaceVerified = table.Column<bool>(type: "boolean", nullable: true),
                    FaceConfidence = table.Column<float>(type: "real", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEncrypted = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptionMethod = table.Column<string>(type: "text", nullable: false),
                    EncryptedIdDocumentNumber = table.Column<string>(type: "text", nullable: true),
                    EncryptedIdDocumentImage = table.Column<string>(type: "text", nullable: true),
                    EncryptedFaceImage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdentityVerifications_AspNetUsers_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IconSvg = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RelatedUrl = table.Column<string>(type: "text", nullable: true),
                    IsEncrypted = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptionMethod = table.Column<string>(type: "text", nullable: true),
                    EncryptedTitle = table.Column<string>(type: "text", nullable: true),
                    EncryptedMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PeerMentorships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerMentorships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeerMentorships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Portfolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ProjectImages = table.Column<string>(type: "text", nullable: true),
                    ProjectLink = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Portfolios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Portfolios_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserAccountSkills",
                columns: table => new
                {
                    UserAccountId = table.Column<string>(type: "text", nullable: false),
                    UserSkillId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccountSkills", x => new { x.UserAccountId, x.UserSkillId });
                    table.ForeignKey(
                        name: "FK_UserAccountSkills_AspNetUsers_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserAccountSkills_UserSkills_UserSkillId",
                        column: x => x.UserSkillId,
                        principalTable: "UserSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MentorshipMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorId = table.Column<string>(type: "text", nullable: false),
                    MenteeId = table.Column<string>(type: "text", nullable: false),
                    MentorMentorshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenteeMentorshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeclinedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorshipMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorshipMatches_AspNetUsers_MenteeId",
                        column: x => x.MenteeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MentorshipMatches_AspNetUsers_MentorId",
                        column: x => x.MentorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MentorshipMatches_PeerMentorships_MenteeMentorshipId",
                        column: x => x.MenteeMentorshipId,
                        principalTable: "PeerMentorships",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MentorshipMatches_PeerMentorships_MentorMentorshipId",
                        column: x => x.MentorMentorshipId,
                        principalTable: "PeerMentorships",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MenteeSessionEvidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorshipMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    WhatWasDone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AdditionalNotes = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    EvidenceFilePaths = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()")
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
                name: "MentorReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorshipMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorId = table.Column<string>(type: "text", nullable: false),
                    MenteeId = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    WouldRecommend = table.Column<bool>(type: "boolean", nullable: false),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Strengths = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AreasForImprovement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorReviews_AspNetUsers_MenteeId",
                        column: x => x.MenteeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MentorReviews_AspNetUsers_MentorId",
                        column: x => x.MentorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MentorReviews_MentorshipMatches_MentorshipMatchId",
                        column: x => x.MentorshipMatchId,
                        principalTable: "MentorshipMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MentorSessionNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorshipMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorId = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Feedback = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProgressRating = table.Column<int>(type: "integer", nullable: true),
                    IsTaskAssigned = table.Column<bool>(type: "boolean", nullable: false),
                    TaskTitle = table.Column<string>(type: "text", nullable: true),
                    TaskDescription = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()")
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

            migrationBuilder.CreateTable(
                name: "MentorshipChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorshipMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MessageType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    FileType = table.Column<string>(type: "text", nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorshipChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorshipChatMessages_AspNetUsers_SenderId",
                        column: x => x.SenderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MentorshipChatMessages_MentorshipMatches_MentorshipMatchId",
                        column: x => x.MentorshipMatchId,
                        principalTable: "MentorshipMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MentorshipSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorshipMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    ScheduledStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorshipSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorshipSessions_MentorshipMatches_MentorshipMatchId",
                        column: x => x.MentorshipMatchId,
                        principalTable: "MentorshipMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MentorshipGoalCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorshipMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedByUserId = table.Column<string>(type: "text", nullable: false),
                    CompletionType = table.Column<string>(type: "text", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    MenteeEvidenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    MentorNoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsCompletedByMentor = table.Column<bool>(type: "boolean", nullable: false),
                    IsCompletedByMentee = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorshipGoalCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorshipGoalCompletions_AspNetUsers_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MentorshipGoalCompletions_Goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "Goals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MentorshipGoalCompletions_MenteeSessionEvidences_MenteeEvid~",
                        column: x => x.MenteeEvidenceId,
                        principalTable: "MenteeSessionEvidences",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MentorshipGoalCompletions_MentorSessionNotes_MentorNoteId",
                        column: x => x.MentorNoteId,
                        principalTable: "MentorSessionNotes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MentorshipGoalCompletions_MentorshipMatches_MentorshipMatch~",
                        column: x => x.MentorshipMatchId,
                        principalTable: "MentorshipMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MentorshipChatFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorshipChatFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorshipChatFiles_MentorshipChatMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "MentorshipChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Biddings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Budget = table.Column<int>(type: "integer", nullable: false),
                    Delivery = table.Column<string>(type: "text", nullable: false),
                    Proposal = table.Column<string>(type: "text", nullable: false),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    BiddingAcceptedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreviousWorksPaths = table.Column<string>(type: "text", nullable: true),
                    RepositoryLinks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Biddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Biddings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FreelancerFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptBidId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreelancerId = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    WouldRecommend = table.Column<bool>(type: "boolean", nullable: false),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreelancerFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FreelancerFeedbacks_AspNetUsers_FreelancerId",
                        column: x => x.FreelancerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FreelancerFeedbacks_Biddings_AcceptBidId",
                        column: x => x.AcceptBidId,
                        principalTable: "Biddings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ProjectName = table.Column<string>(type: "text", nullable: false),
                    ProjectDescription = table.Column<string>(type: "text", nullable: false),
                    Budget = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    ImagePaths = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AcceptedBidId = table.Column<Guid>(type: "uuid", nullable: true),
                    Deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Projects_Biddings_AcceptedBidId",
                        column: x => x.AcceptedBidId,
                        principalTable: "Biddings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChatRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    User1Id = table.Column<string>(type: "text", nullable: false),
                    User2Id = table.Column<string>(type: "text", nullable: false),
                    RoomType = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    MentorshipMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatRooms_AspNetUsers_User1Id",
                        column: x => x.User1Id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatRooms_AspNetUsers_User2Id",
                        column: x => x.User2Id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatRooms_MentorshipMatches_MentorshipMatchId",
                        column: x => x.MentorshipMatchId,
                        principalTable: "MentorshipMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChatRooms_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    BiddingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractTitle = table.Column<string>(type: "text", nullable: false),
                    ContractContent = table.Column<string>(type: "text", nullable: false),
                    ContractTemplateUsed = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TerminatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClientSignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClientSignatureType = table.Column<string>(type: "text", nullable: true),
                    ClientSignatureData = table.Column<string>(type: "text", nullable: true),
                    ClientIPAddress = table.Column<string>(type: "text", nullable: true),
                    ClientUserAgent = table.Column<string>(type: "text", nullable: true),
                    FreelancerSignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FreelancerSignatureType = table.Column<string>(type: "text", nullable: true),
                    FreelancerSignatureData = table.Column<string>(type: "text", nullable: true),
                    FreelancerIPAddress = table.Column<string>(type: "text", nullable: true),
                    FreelancerUserAgent = table.Column<string>(type: "text", nullable: true),
                    PaymentTerms = table.Column<string>(type: "text", nullable: true),
                    DeliverableRequirements = table.Column<string>(type: "text", nullable: true),
                    RevisionPolicy = table.Column<string>(type: "text", nullable: true),
                    ClientMarkedCompleteAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FreelancerMarkedCompleteAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Timeline = table.Column<string>(type: "text", nullable: true),
                    DocumentPath = table.Column<string>(type: "text", nullable: true),
                    DocumentHash = table.Column<string>(type: "text", nullable: true),
                    DocumentSize = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_Biddings_BiddingId",
                        column: x => x.BiddingId,
                        principalTable: "Biddings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contracts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSkillId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSkills_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectSkills_UserSkills_UserSkillId",
                        column: x => x.UserSkillId,
                        principalTable: "UserSkills",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatRoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MessageType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    FileType = table.Column<string>(type: "text", nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_AspNetUsers_SenderId",
                        column: x => x.SenderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatRooms_ChatRoomId",
                        column: x => x.ChatRoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IPAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    PreviousStatus = table.Column<string>(type: "text", nullable: true),
                    NewStatus = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAuditLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractAuditLogs_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    RevisionContent = table.Column<string>(type: "text", nullable: false),
                    RevisionNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PreviousHash = table.Column<string>(type: "text", nullable: true),
                    CurrentHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractRevisions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractRevisions_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractTerminations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    TerminationReason = table.Column<string>(type: "text", nullable: false),
                    TerminationDetails = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    RequestedByUserId = table.Column<string>(type: "text", nullable: false),
                    RequestedByUserRole = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalPayment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ClientSignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClientSignatureType = table.Column<string>(type: "text", nullable: true),
                    ClientSignatureData = table.Column<string>(type: "text", nullable: true),
                    ClientIPAddress = table.Column<string>(type: "text", nullable: true),
                    ClientUserAgent = table.Column<string>(type: "text", nullable: true),
                    FreelancerSignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FreelancerSignatureType = table.Column<string>(type: "text", nullable: true),
                    FreelancerSignatureData = table.Column<string>(type: "text", nullable: true),
                    FreelancerIPAddress = table.Column<string>(type: "text", nullable: true),
                    FreelancerUserAgent = table.Column<string>(type: "text", nullable: true),
                    TerminationTerms = table.Column<string>(type: "text", nullable: true),
                    SettlementDetails = table.Column<string>(type: "text", nullable: true),
                    SettlementNotes = table.Column<string>(type: "text", nullable: true),
                    DocumentPath = table.Column<string>(type: "text", nullable: true),
                    DocumentHash = table.Column<string>(type: "text", nullable: true),
                    DocumentSize = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTerminations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractTerminations_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Deliverables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SubmittedFilesPaths = table.Column<string>(type: "text", nullable: true),
                    RepositoryLinks = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewComments = table.Column<string>(type: "text", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PreviousVersionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deliverables_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Deliverables_AspNetUsers_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Deliverables_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Deliverables_Deliverables_PreviousVersionId",
                        column: x => x.PreviousVersionId,
                        principalTable: "Deliverables",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChatFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatFiles_ChatMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractTerminationAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractTerminationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IPAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTerminationAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractTerminationAuditLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContractTerminationAuditLogs_ContractTerminations_ContractT~",
                        column: x => x.ContractTerminationId,
                        principalTable: "ContractTerminations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Email",
                table: "AspNetUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_UserName",
                table: "AspNetUsers",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Biddings_ProjectId",
                table: "Biddings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Biddings_UserId_ProjectId",
                table: "Biddings",
                columns: new[] { "UserId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatFiles_MessageId",
                table: "ChatFiles",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatRoomId",
                table: "ChatMessages",
                column: "ChatRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_IsRead",
                table: "ChatMessages",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SenderId",
                table: "ChatMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SentAt",
                table: "ChatMessages",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_LastActivityAt",
                table: "ChatRooms",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_MentorshipMatchId",
                table: "ChatRooms",
                column: "MentorshipMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_ProjectId",
                table: "ChatRooms",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_User1Id_User2Id",
                table: "ChatRooms",
                columns: new[] { "User1Id", "User2Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_User2Id",
                table: "ChatRooms",
                column: "User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAuditLogs_ContractId",
                table: "ContractAuditLogs",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAuditLogs_Timestamp",
                table: "ContractAuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAuditLogs_UserId",
                table: "ContractAuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractRevisions_ContractId_RevisionNumber",
                table: "ContractRevisions",
                columns: new[] { "ContractId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractRevisions_CreatedByUserId",
                table: "ContractRevisions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_BiddingId",
                table: "Contracts",
                column: "BiddingId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CreatedAt",
                table: "Contracts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ProjectId",
                table: "Contracts",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Status",
                table: "Contracts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_Category",
                table: "ContractTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTemplates_IsActive",
                table: "ContractTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTerminationAuditLogs_ContractTerminationId",
                table: "ContractTerminationAuditLogs",
                column: "ContractTerminationId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTerminationAuditLogs_Timestamp",
                table: "ContractTerminationAuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTerminationAuditLogs_UserId",
                table: "ContractTerminationAuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTerminations_ContractId",
                table: "ContractTerminations",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTerminations_RequestedAt",
                table: "ContractTerminations",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTerminations_RequestedByUserId",
                table: "ContractTerminations",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTerminations_Status",
                table: "ContractTerminations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_ContractId",
                table: "Deliverables",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_PreviousVersionId",
                table: "Deliverables",
                column: "PreviousVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_ReviewedByUserId",
                table: "Deliverables",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_Status",
                table: "Deliverables",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_SubmittedAt",
                table: "Deliverables",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_SubmittedByUserId",
                table: "Deliverables",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerFeedbacks_AcceptBidId",
                table: "FreelancerFeedbacks",
                column: "AcceptBidId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerFeedbacks_CreatedAt",
                table: "FreelancerFeedbacks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerFeedbacks_FreelancerId",
                table: "FreelancerFeedbacks",
                column: "FreelancerId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringOutcomes_FreelancerId",
                table: "HiringOutcomes",
                column: "FreelancerId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringOutcomes_ProjectId",
                table: "HiringOutcomes",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringOutcomes_RecordedAt",
                table: "HiringOutcomes",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityVerifications_UserAccountId",
                table: "IdentityVerifications",
                column: "UserAccountId");

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
                name: "IX_MentorReviews_CreatedAt",
                table: "MentorReviews",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MentorReviews_MenteeId",
                table: "MentorReviews",
                column: "MenteeId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorReviews_MentorId",
                table: "MentorReviews",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorReviews_MentorshipMatchId",
                table: "MentorReviews",
                column: "MentorshipMatchId",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipChatFiles_MessageId",
                table: "MentorshipChatFiles",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipChatMessages_MentorshipMatchId",
                table: "MentorshipChatMessages",
                column: "MentorshipMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipChatMessages_MentorshipMatchId_SentAt",
                table: "MentorshipChatMessages",
                columns: new[] { "MentorshipMatchId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipChatMessages_SenderId",
                table: "MentorshipChatMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipChatMessages_SentAt",
                table: "MentorshipChatMessages",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipGoalCompletions_CompletedAt",
                table: "MentorshipGoalCompletions",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipGoalCompletions_CompletedByUserId",
                table: "MentorshipGoalCompletions",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipGoalCompletions_GoalId",
                table: "MentorshipGoalCompletions",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipGoalCompletions_MenteeEvidenceId",
                table: "MentorshipGoalCompletions",
                column: "MenteeEvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipGoalCompletions_MentorNoteId",
                table: "MentorshipGoalCompletions",
                column: "MentorNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipGoalCompletions_MentorshipMatchId_GoalId",
                table: "MentorshipGoalCompletions",
                columns: new[] { "MentorshipMatchId", "GoalId" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipMatches_MatchedDate",
                table: "MentorshipMatches",
                column: "MatchedDate");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipMatches_MenteeId",
                table: "MentorshipMatches",
                column: "MenteeId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipMatches_MenteeMentorshipId",
                table: "MentorshipMatches",
                column: "MenteeMentorshipId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipMatches_MentorId_MenteeId",
                table: "MentorshipMatches",
                columns: new[] { "MentorId", "MenteeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipMatches_MentorMentorshipId",
                table: "MentorshipMatches",
                column: "MentorMentorshipId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipMatches_Status",
                table: "MentorshipMatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MentorshipSessions_MentorshipMatchId",
                table: "MentorshipSessions",
                column: "MentorshipMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAt",
                table: "Notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IsRead",
                table: "Notifications",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerMentorships_UserId",
                table: "PeerMentorships",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_UserId",
                table: "Portfolios",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_AcceptedBidId",
                table: "Projects",
                column: "AcceptedBidId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_UserId",
                table: "Projects",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSkills_ProjectId_UserSkillId",
                table: "ProjectSkills",
                columns: new[] { "ProjectId", "UserSkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSkills_UserSkillId",
                table: "ProjectSkills",
                column: "UserSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccountSkills_UserSkillId",
                table: "UserAccountSkills",
                column: "UserSkillId");

            migrationBuilder.AddForeignKey(
                name: "FK_Biddings_Projects_ProjectId",
                table: "Biddings",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Biddings_AspNetUsers_UserId",
                table: "Biddings");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_AspNetUsers_UserId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Biddings_Projects_ProjectId",
                table: "Biddings");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "ChatFiles");

            migrationBuilder.DropTable(
                name: "ContractAuditLogs");

            migrationBuilder.DropTable(
                name: "ContractRevisions");

            migrationBuilder.DropTable(
                name: "ContractTemplates");

            migrationBuilder.DropTable(
                name: "ContractTerminationAuditLogs");

            migrationBuilder.DropTable(
                name: "Deliverables");

            migrationBuilder.DropTable(
                name: "FreelancerFeedbacks");

            migrationBuilder.DropTable(
                name: "HiringOutcomes");

            migrationBuilder.DropTable(
                name: "IdentityVerifications");

            migrationBuilder.DropTable(
                name: "MentorReviews");

            migrationBuilder.DropTable(
                name: "MentorshipChatFiles");

            migrationBuilder.DropTable(
                name: "MentorshipGoalCompletions");

            migrationBuilder.DropTable(
                name: "MentorshipSessions");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Portfolios");

            migrationBuilder.DropTable(
                name: "ProjectSkills");

            migrationBuilder.DropTable(
                name: "UserAccountSkills");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ContractTerminations");

            migrationBuilder.DropTable(
                name: "MentorshipChatMessages");

            migrationBuilder.DropTable(
                name: "MenteeSessionEvidences");

            migrationBuilder.DropTable(
                name: "MentorSessionNotes");

            migrationBuilder.DropTable(
                name: "UserSkills");

            migrationBuilder.DropTable(
                name: "ChatRooms");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "Goals");

            migrationBuilder.DropTable(
                name: "MentorshipMatches");

            migrationBuilder.DropTable(
                name: "PeerMentorships");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Biddings");
        }
    }
}
