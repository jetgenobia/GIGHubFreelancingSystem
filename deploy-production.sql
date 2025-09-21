IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FirstName] nvarchar(max) NOT NULL,
        [LastName] nvarchar(max) NOT NULL,
        [Photo] nvarchar(max) NULL,
        [Role] nvarchar(max) NOT NULL,
        [Bio] nvarchar(max) NULL,
        [ExperienceLevel] nvarchar(max) NULL,
        [MentorshipId] uniqueidentifier NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [ContractTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Category] nvarchar(450) NOT NULL,
        [TemplateContent] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [LastModifiedAt] datetime2 NULL,
        [PreviewImagePath] nvarchar(max) NULL,
        [UsageCount] int NOT NULL,
        [TemplateVersion] nvarchar(max) NULL,
        CONSTRAINT [PK_ContractTemplates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [Goals] (
        [Id] uniqueidentifier NOT NULL,
        [GoalName] nvarchar(max) NOT NULL,
        [GoalDescription] nvarchar(max) NOT NULL,
        [Order] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IconSvg] nvarchar(max) NULL,
        CONSTRAINT [PK_Goals] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [HiringOutcomes] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [FreelancerId] nvarchar(450) NOT NULL,
        [WasSuccessful] bit NOT NULL,
        [RecordedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_HiringOutcomes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [UserSkills] (
        [Id] uniqueidentifier NOT NULL,
        [Category] nvarchar(max) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_UserSkills] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [IdentityVerifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserAccountId] nvarchar(450) NOT NULL,
        [IdDocumentType] nvarchar(max) NULL,
        [IdDocumentExpiryDate] datetime2 NULL,
        [IdDocumentVerified] bit NULL,
        [IdDocumentConfidence] real NULL,
        [FaceVerified] bit NULL,
        [FaceConfidence] real NULL,
        [Status] nvarchar(max) NOT NULL,
        [RejectionReason] nvarchar(max) NULL,
        [VerifiedAt] datetime2 NULL,
        [RejectedAt] datetime2 NULL,
        [IsEncrypted] bit NOT NULL,
        [EncryptionMethod] nvarchar(max) NOT NULL,
        [EncryptedIdDocumentNumber] nvarchar(max) NULL,
        [EncryptedIdDocumentImage] nvarchar(max) NULL,
        [EncryptedFaceImage] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_IdentityVerifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IdentityVerifications_AspNetUsers_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Title] nvarchar(100) NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [IconSvg] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [IsRead] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ReadAt] datetime2 NULL,
        [RelatedUrl] nvarchar(max) NULL,
        [IsEncrypted] bit NOT NULL,
        [EncryptionMethod] nvarchar(max) NULL,
        [EncryptedTitle] nvarchar(max) NULL,
        [EncryptedMessage] nvarchar(max) NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [PeerMentorships] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [FirstName] nvarchar(max) NOT NULL,
        [LastName] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [Role] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_PeerMentorships] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PeerMentorships_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [Portfolios] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [ProjectImages] nvarchar(max) NULL,
        [ProjectLink] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Portfolios] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Portfolios_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [UserAccountSkills] (
        [UserAccountId] nvarchar(450) NOT NULL,
        [UserSkillId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserAccountSkills] PRIMARY KEY ([UserAccountId], [UserSkillId]),
        CONSTRAINT [FK_UserAccountSkills_AspNetUsers_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserAccountSkills_UserSkills_UserSkillId] FOREIGN KEY ([UserSkillId]) REFERENCES [UserSkills] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [MentorshipMatches] (
        [Id] uniqueidentifier NOT NULL,
        [MentorId] nvarchar(450) NOT NULL,
        [MenteeId] nvarchar(450) NOT NULL,
        [MentorMentorshipId] uniqueidentifier NOT NULL,
        [MenteeMentorshipId] uniqueidentifier NOT NULL,
        [MatchedDate] datetime2 NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [DeclinedDate] datetime2 NULL,
        [Notes] nvarchar(1000) NULL,
        CONSTRAINT [PK_MentorshipMatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MentorshipMatches_AspNetUsers_MenteeId] FOREIGN KEY ([MenteeId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_MentorshipMatches_AspNetUsers_MentorId] FOREIGN KEY ([MentorId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_MentorshipMatches_PeerMentorships_MenteeMentorshipId] FOREIGN KEY ([MenteeMentorshipId]) REFERENCES [PeerMentorships] ([Id]),
        CONSTRAINT [FK_MentorshipMatches_PeerMentorships_MentorMentorshipId] FOREIGN KEY ([MentorMentorshipId]) REFERENCES [PeerMentorships] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [MentorReviews] (
        [Id] uniqueidentifier NOT NULL,
        [MentorshipMatchId] uniqueidentifier NOT NULL,
        [MentorId] nvarchar(450) NOT NULL,
        [MenteeId] nvarchar(450) NOT NULL,
        [Rating] int NOT NULL,
        [WouldRecommend] bit NOT NULL,
        [Comments] nvarchar(2000) NULL,
        [Strengths] nvarchar(500) NULL,
        [AreasForImprovement] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_MentorReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MentorReviews_AspNetUsers_MenteeId] FOREIGN KEY ([MenteeId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_MentorReviews_AspNetUsers_MentorId] FOREIGN KEY ([MentorId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_MentorReviews_MentorshipMatches_MentorshipMatchId] FOREIGN KEY ([MentorshipMatchId]) REFERENCES [MentorshipMatches] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [MentorshipChatMessages] (
        [Id] uniqueidentifier NOT NULL,
        [MentorshipMatchId] uniqueidentifier NOT NULL,
        [SenderId] nvarchar(450) NOT NULL,
        [Message] nvarchar(2000) NOT NULL,
        [MessageType] nvarchar(20) NOT NULL,
        [FileUrl] nvarchar(max) NULL,
        [FileType] nvarchar(max) NULL,
        [FileSize] bigint NULL,
        [SentAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [IsRead] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ReadAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_MentorshipChatMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MentorshipChatMessages_AspNetUsers_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_MentorshipChatMessages_MentorshipMatches_MentorshipMatchId] FOREIGN KEY ([MentorshipMatchId]) REFERENCES [MentorshipMatches] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [MentorshipGoalCompletions] (
        [Id] uniqueidentifier NOT NULL,
        [MentorshipMatchId] uniqueidentifier NOT NULL,
        [GoalId] uniqueidentifier NOT NULL,
        [CompletedByUserId] nvarchar(450) NOT NULL,
        [CompletedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [CompletionType] nvarchar(20) NOT NULL,
        [IsCompletedByMentor] bit NOT NULL,
        [IsCompletedByMentee] bit NOT NULL,
        CONSTRAINT [PK_MentorshipGoalCompletions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MentorshipGoalCompletions_AspNetUsers_CompletedByUserId] FOREIGN KEY ([CompletedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_MentorshipGoalCompletions_Goals_GoalId] FOREIGN KEY ([GoalId]) REFERENCES [Goals] ([Id]),
        CONSTRAINT [FK_MentorshipGoalCompletions_MentorshipMatches_MentorshipMatchId] FOREIGN KEY ([MentorshipMatchId]) REFERENCES [MentorshipMatches] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [MentorshipSessions] (
        [Id] uniqueidentifier NOT NULL,
        [MentorshipMatchId] uniqueidentifier NOT NULL,
        [CreatedByUserId] nvarchar(max) NOT NULL,
        [ScheduledStartUtc] datetime2 NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [Title] nvarchar(150) NULL,
        [Notes] nvarchar(2000) NULL,
        [TimeZone] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_MentorshipSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MentorshipSessions_MentorshipMatches_MentorshipMatchId] FOREIGN KEY ([MentorshipMatchId]) REFERENCES [MentorshipMatches] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [MentorshipChatFiles] (
        [Id] uniqueidentifier NOT NULL,
        [MessageId] uniqueidentifier NOT NULL,
        [OriginalFileName] nvarchar(255) NOT NULL,
        [StoredFileName] nvarchar(500) NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [FileSize] bigint NOT NULL,
        [UploadedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_MentorshipChatFiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MentorshipChatFiles_MentorshipChatMessages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [MentorshipChatMessages] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [Biddings] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Budget] int NOT NULL,
        [Delivery] nvarchar(max) NOT NULL,
        [Proposal] nvarchar(max) NOT NULL,
        [IsAccepted] bit NOT NULL,
        [BiddingAcceptedDate] datetime2 NULL,
        [PreviousWorksPaths] nvarchar(max) NULL,
        [RepositoryLinks] nvarchar(max) NULL,
        CONSTRAINT [PK_Biddings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Biddings_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [FreelancerFeedbacks] (
        [Id] uniqueidentifier NOT NULL,
        [AcceptBidId] uniqueidentifier NOT NULL,
        [FreelancerId] nvarchar(450) NOT NULL,
        [Rating] int NOT NULL,
        [WouldRecommend] bit NOT NULL,
        [Comments] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_FreelancerFeedbacks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FreelancerFeedbacks_AspNetUsers_FreelancerId] FOREIGN KEY ([FreelancerId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_FreelancerFeedbacks_Biddings_AcceptBidId] FOREIGN KEY ([AcceptBidId]) REFERENCES [Biddings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [Projects] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [ProjectName] nvarchar(max) NOT NULL,
        [ProjectDescription] nvarchar(max) NOT NULL,
        [Budget] nvarchar(max) NOT NULL,
        [Category] nvarchar(max) NOT NULL,
        [ImagePaths] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [AcceptedBidId] uniqueidentifier NULL,
        CONSTRAINT [PK_Projects] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Projects_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Projects_Biddings_AcceptedBidId] FOREIGN KEY ([AcceptedBidId]) REFERENCES [Biddings] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [ChatRooms] (
        [Id] uniqueidentifier NOT NULL,
        [User1Id] nvarchar(450) NOT NULL,
        [User2Id] nvarchar(450) NOT NULL,
        [RoomType] nvarchar(max) NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [MentorshipMatchId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastActivityAt] datetime2 NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ChatRooms] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatRooms_AspNetUsers_User1Id] FOREIGN KEY ([User1Id]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_ChatRooms_AspNetUsers_User2Id] FOREIGN KEY ([User2Id]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_ChatRooms_MentorshipMatches_MentorshipMatchId] FOREIGN KEY ([MentorshipMatchId]) REFERENCES [MentorshipMatches] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ChatRooms_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [Contracts] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [BiddingId] uniqueidentifier NOT NULL,
        [ContractTitle] nvarchar(max) NOT NULL,
        [ContractContent] nvarchar(max) NOT NULL,
        [ContractTemplateUsed] nvarchar(max) NULL,
        [Status] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [LastModifiedAt] datetime2 NULL,
        [TerminatedAt] datetime2 NULL,
        [ClientSignedAt] datetime2 NULL,
        [ClientSignatureType] nvarchar(max) NULL,
        [ClientSignatureData] nvarchar(max) NULL,
        [ClientIPAddress] nvarchar(max) NULL,
        [ClientUserAgent] nvarchar(max) NULL,
        [FreelancerSignedAt] datetime2 NULL,
        [FreelancerSignatureType] nvarchar(max) NULL,
        [FreelancerSignatureData] nvarchar(max) NULL,
        [FreelancerIPAddress] nvarchar(max) NULL,
        [FreelancerUserAgent] nvarchar(max) NULL,
        [PaymentTerms] nvarchar(max) NULL,
        [DeliverableRequirements] nvarchar(max) NULL,
        [RevisionPolicy] nvarchar(max) NULL,
        [ClientMarkedCompleteAt] datetime2 NULL,
        [FreelancerMarkedCompleteAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [Timeline] nvarchar(max) NULL,
        [DocumentPath] nvarchar(max) NULL,
        [DocumentHash] nvarchar(max) NULL,
        [DocumentSize] bigint NULL,
        CONSTRAINT [PK_Contracts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Contracts_Biddings_BiddingId] FOREIGN KEY ([BiddingId]) REFERENCES [Biddings] ([Id]),
        CONSTRAINT [FK_Contracts_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [ProjectSkills] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [UserSkillId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ProjectSkills] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectSkills_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectSkills_UserSkills_UserSkillId] FOREIGN KEY ([UserSkillId]) REFERENCES [UserSkills] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [ChatMessages] (
        [Id] uniqueidentifier NOT NULL,
        [ChatRoomId] uniqueidentifier NOT NULL,
        [SenderId] nvarchar(450) NOT NULL,
        [Message] nvarchar(2000) NOT NULL,
        [MessageType] nvarchar(20) NOT NULL,
        [FileUrl] nvarchar(max) NULL,
        [FileType] nvarchar(max) NULL,
        [FileSize] bigint NULL,
        [SentAt] datetime2 NOT NULL,
        [IsRead] bit NOT NULL,
        [ReadAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatMessages_AspNetUsers_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_ChatMessages_ChatRooms_ChatRoomId] FOREIGN KEY ([ChatRoomId]) REFERENCES [ChatRooms] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [ContractAuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Details] nvarchar(max) NULL,
        [Timestamp] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [IPAddress] nvarchar(max) NULL,
        [UserAgent] nvarchar(max) NULL,
        [PreviousStatus] nvarchar(max) NULL,
        [NewStatus] nvarchar(max) NULL,
        CONSTRAINT [PK_ContractAuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContractAuditLogs_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_ContractAuditLogs_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [ContractRevisions] (
        [Id] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [RevisionNumber] int NOT NULL,
        [RevisionContent] nvarchar(max) NOT NULL,
        [RevisionNotes] nvarchar(max) NULL,
        [CreatedByUserId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [PreviousHash] nvarchar(max) NULL,
        [CurrentHash] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ContractRevisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContractRevisions_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_ContractRevisions_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [ContractTerminations] (
        [Id] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [TerminationReason] nvarchar(max) NOT NULL,
        [TerminationDetails] nvarchar(max) NOT NULL,
        [RequestedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [RequestedByUserId] nvarchar(450) NOT NULL,
        [RequestedByUserRole] nvarchar(max) NOT NULL,
        [Status] nvarchar(450) NOT NULL,
        [CompletedAt] datetime2 NULL,
        [FinalPayment] decimal(18,2) NOT NULL,
        [ClientSignedAt] datetime2 NULL,
        [ClientSignatureType] nvarchar(max) NULL,
        [ClientSignatureData] nvarchar(max) NULL,
        [ClientIPAddress] nvarchar(max) NULL,
        [ClientUserAgent] nvarchar(max) NULL,
        [FreelancerSignedAt] datetime2 NULL,
        [FreelancerSignatureType] nvarchar(max) NULL,
        [FreelancerSignatureData] nvarchar(max) NULL,
        [FreelancerIPAddress] nvarchar(max) NULL,
        [FreelancerUserAgent] nvarchar(max) NULL,
        [TerminationTerms] nvarchar(max) NULL,
        [SettlementDetails] nvarchar(max) NULL,
        [SettlementNotes] nvarchar(max) NULL,
        [DocumentPath] nvarchar(max) NULL,
        [DocumentHash] nvarchar(max) NULL,
        [DocumentSize] bigint NULL,
        CONSTRAINT [PK_ContractTerminations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContractTerminations_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [Deliverables] (
        [Id] uniqueidentifier NOT NULL,
        [ContractId] uniqueidentifier NOT NULL,
        [SubmittedByUserId] nvarchar(450) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Status] nvarchar(450) NOT NULL,
        [SubmittedFilesPaths] nvarchar(max) NULL,
        [RepositoryLinks] nvarchar(max) NULL,
        [SubmittedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [ReviewedAt] datetime2 NULL,
        [ReviewComments] nvarchar(max) NULL,
        [ReviewedByUserId] nvarchar(450) NULL,
        [Version] int NOT NULL,
        [PreviousVersionId] uniqueidentifier NULL,
        CONSTRAINT [PK_Deliverables] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Deliverables_AspNetUsers_ReviewedByUserId] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_Deliverables_AspNetUsers_SubmittedByUserId] FOREIGN KEY ([SubmittedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_Deliverables_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Deliverables_Deliverables_PreviousVersionId] FOREIGN KEY ([PreviousVersionId]) REFERENCES [Deliverables] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [ChatFiles] (
        [Id] uniqueidentifier NOT NULL,
        [MessageId] uniqueidentifier NOT NULL,
        [OriginalFileName] nvarchar(255) NOT NULL,
        [StoredFileName] nvarchar(500) NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [FileSize] bigint NOT NULL,
        [UploadedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ChatFiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatFiles_ChatMessages_MessageId] FOREIGN KEY ([MessageId]) REFERENCES [ChatMessages] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE TABLE [ContractTerminationAuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [ContractTerminationId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Details] nvarchar(max) NULL,
        [Timestamp] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [IPAddress] nvarchar(max) NULL,
        [UserAgent] nvarchar(max) NULL,
        CONSTRAINT [PK_ContractTerminationAuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContractTerminationAuditLogs_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_ContractTerminationAuditLogs_ContractTerminations_ContractTerminationId] FOREIGN KEY ([ContractTerminationId]) REFERENCES [ContractTerminations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AspNetUsers_Email] ON [AspNetUsers] ([Email]) WHERE [Email] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AspNetUsers_UserName] ON [AspNetUsers] ([UserName]) WHERE [UserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Biddings_ProjectId] ON [Biddings] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Biddings_UserId_ProjectId] ON [Biddings] ([UserId], [ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ChatFiles_MessageId] ON [ChatFiles] ([MessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_ChatRoomId] ON [ChatMessages] ([ChatRoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_IsRead] ON [ChatMessages] ([IsRead]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_SenderId] ON [ChatMessages] ([SenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_SentAt] ON [ChatMessages] ([SentAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ChatRooms_LastActivityAt] ON [ChatRooms] ([LastActivityAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ChatRooms_MentorshipMatchId] ON [ChatRooms] ([MentorshipMatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ChatRooms_ProjectId] ON [ChatRooms] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ChatRooms_User1Id_User2Id] ON [ChatRooms] ([User1Id], [User2Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ChatRooms_User2Id] ON [ChatRooms] ([User2Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ContractAuditLogs_ContractId] ON [ContractAuditLogs] ([ContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ContractAuditLogs_Timestamp] ON [ContractAuditLogs] ([Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ContractAuditLogs_UserId] ON [ContractAuditLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ContractRevisions_ContractId_RevisionNumber] ON [ContractRevisions] ([ContractId], [RevisionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ContractRevisions_CreatedByUserId] ON [ContractRevisions] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Contracts_BiddingId] ON [Contracts] ([BiddingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Contracts_CreatedAt] ON [Contracts] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Contracts_ProjectId] ON [Contracts] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Contracts_Status] ON [Contracts] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ContractTemplates_Category] ON [ContractTemplates] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ContractTemplates_IsActive] ON [ContractTemplates] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ContractTerminationAuditLogs_ContractTerminationId] ON [ContractTerminationAuditLogs] ([ContractTerminationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ContractTerminationAuditLogs_Timestamp] ON [ContractTerminationAuditLogs] ([Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ContractTerminationAuditLogs_UserId] ON [ContractTerminationAuditLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ContractTerminations_ContractId] ON [ContractTerminations] ([ContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ContractTerminations_RequestedAt] ON [ContractTerminations] ([RequestedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ContractTerminations_RequestedByUserId] ON [ContractTerminations] ([RequestedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ContractTerminations_Status] ON [ContractTerminations] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Deliverables_ContractId] ON [Deliverables] ([ContractId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Deliverables_PreviousVersionId] ON [Deliverables] ([PreviousVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Deliverables_ReviewedByUserId] ON [Deliverables] ([ReviewedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Deliverables_Status] ON [Deliverables] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Deliverables_SubmittedAt] ON [Deliverables] ([SubmittedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Deliverables_SubmittedByUserId] ON [Deliverables] ([SubmittedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FreelancerFeedbacks_AcceptBidId] ON [FreelancerFeedbacks] ([AcceptBidId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_FreelancerFeedbacks_CreatedAt] ON [FreelancerFeedbacks] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_FreelancerFeedbacks_FreelancerId] ON [FreelancerFeedbacks] ([FreelancerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_HiringOutcomes_FreelancerId] ON [HiringOutcomes] ([FreelancerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_HiringOutcomes_ProjectId] ON [HiringOutcomes] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_HiringOutcomes_RecordedAt] ON [HiringOutcomes] ([RecordedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_IdentityVerifications_UserAccountId] ON [IdentityVerifications] ([UserAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorReviews_CreatedAt] ON [MentorReviews] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorReviews_MenteeId] ON [MentorReviews] ([MenteeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorReviews_MentorId] ON [MentorReviews] ([MentorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MentorReviews_MentorshipMatchId] ON [MentorReviews] ([MentorshipMatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipChatFiles_MessageId] ON [MentorshipChatFiles] ([MessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipChatMessages_MentorshipMatchId] ON [MentorshipChatMessages] ([MentorshipMatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipChatMessages_MentorshipMatchId_SentAt] ON [MentorshipChatMessages] ([MentorshipMatchId], [SentAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipChatMessages_SenderId] ON [MentorshipChatMessages] ([SenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipChatMessages_SentAt] ON [MentorshipChatMessages] ([SentAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipGoalCompletions_CompletedAt] ON [MentorshipGoalCompletions] ([CompletedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipGoalCompletions_CompletedByUserId] ON [MentorshipGoalCompletions] ([CompletedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipGoalCompletions_GoalId] ON [MentorshipGoalCompletions] ([GoalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipGoalCompletions_MentorshipMatchId_GoalId] ON [MentorshipGoalCompletions] ([MentorshipMatchId], [GoalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipMatches_MatchedDate] ON [MentorshipMatches] ([MatchedDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipMatches_MenteeId] ON [MentorshipMatches] ([MenteeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipMatches_MenteeMentorshipId] ON [MentorshipMatches] ([MenteeMentorshipId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MentorshipMatches_MentorId_MenteeId] ON [MentorshipMatches] ([MentorId], [MenteeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipMatches_MentorMentorshipId] ON [MentorshipMatches] ([MentorMentorshipId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipMatches_Status] ON [MentorshipMatches] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_MentorshipSessions_MentorshipMatchId] ON [MentorshipSessions] ([MentorshipMatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Notifications_CreatedAt] ON [Notifications] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Notifications_IsRead] ON [Notifications] ([IsRead]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PeerMentorships_UserId] ON [PeerMentorships] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Portfolios_UserId] ON [Portfolios] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Projects_AcceptedBidId] ON [Projects] ([AcceptedBidId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_Projects_UserId] ON [Projects] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProjectSkills_ProjectId_UserSkillId] ON [ProjectSkills] ([ProjectId], [UserSkillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_ProjectSkills_UserSkillId] ON [ProjectSkills] ([UserSkillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    CREATE INDEX [IX_UserAccountSkills_UserSkillId] ON [UserAccountSkills] ([UserSkillId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    ALTER TABLE [Biddings] ADD CONSTRAINT [FK_Biddings_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250901171639_initial'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250901171639_initial', N'9.0.6');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250902194603_frole'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [FRole] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250902194603_frole'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250902194603_frole', N'9.0.6');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250915144222_SmartHiring'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250915144222_SmartHiring', N'9.0.6');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250918184031_addDeadline'
)
BEGIN
    ALTER TABLE [Projects] ADD [Deadline] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250918184031_addDeadline'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250918184031_addDeadline', N'9.0.6');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919064606_deleteAccount'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919064606_deleteAccount'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [DeletionReason] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919064606_deleteAccount'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919064606_deleteAccount'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250919064606_deleteAccount', N'9.0.6');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    ALTER TABLE [Notifications] DROP CONSTRAINT [FK_Notifications_AspNetUsers_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    ALTER TABLE [PeerMentorships] DROP CONSTRAINT [FK_PeerMentorships_AspNetUsers_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    ALTER TABLE [Portfolios] DROP CONSTRAINT [FK_Portfolios_AspNetUsers_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    ALTER TABLE [Projects] DROP CONSTRAINT [FK_Projects_AspNetUsers_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    ALTER TABLE [UserAccountSkills] DROP CONSTRAINT [FK_UserAccountSkills_AspNetUsers_UserAccountId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContractTerminationAuditLogs]') AND [c].[name] = N'Timestamp');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [ContractTerminationAuditLogs] DROP CONSTRAINT [' + @var + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Contracts]') AND [c].[name] = N'CreatedAt');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Contracts] DROP CONSTRAINT [' + @var1 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContractRevisions]') AND [c].[name] = N'CreatedAt');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [ContractRevisions] DROP CONSTRAINT [' + @var2 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContractAuditLogs]') AND [c].[name] = N'Timestamp');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [ContractAuditLogs] DROP CONSTRAINT [' + @var3 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    ALTER TABLE [PeerMentorships] ADD CONSTRAINT [FK_PeerMentorships_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    ALTER TABLE [Portfolios] ADD CONSTRAINT [FK_Portfolios_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    ALTER TABLE [Projects] ADD CONSTRAINT [FK_Projects_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    ALTER TABLE [UserAccountSkills] ADD CONSTRAINT [FK_UserAccountSkills_AspNetUsers_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [AspNetUsers] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250919081612_updateRs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250919081612_updateRs', N'9.0.6');
END;

COMMIT;
GO

