USE [WorkNotes.db];
GO
-- The Git repositories a WorkNotes user imported from the repositories the provider lists for their connected account
-- (only GitHub for now). ExternalId is the provider's stable ID, which survives a rename or a transfer; FullName,
-- Description, IsPrivate, DefaultBranch and HtmlUrl are the provider's description of it, refreshed each time the user
-- saves their selection. A repository stays imported after the user disconnects their account; deleting the user
-- deletes their repositories.
-- Idempotent: the table and its index are created only when they are missing.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[GitRepositories]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[GitRepositories]
    (
        [Id] int IDENTITY(1, 1) NOT NULL,
        [UserId] nvarchar(128) NOT NULL,
        [Provider] nvarchar(20) NOT NULL,
        [ExternalId] nvarchar(50) NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [Description] nvarchar(400) NULL,
        [IsPrivate] bit NOT NULL,
        [DefaultBranch] nvarchar(255) NULL,
        [HtmlUrl] nvarchar(300) NOT NULL,
        [ImportedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_GitRepositories_ImportedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [RefreshedAtUtc] datetime2(0) NOT NULL,
        CONSTRAINT [PK_GitRepositories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GitRepositories_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_GitRepositories_Provider] CHECK ([Provider] IN (N'GitHub'))
    );
END;

-- One row per user, provider and repository; the key also serves the reading of a user's repositories.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[GitRepositories]')
    AND [name] = N'UX_GitRepositories_UserId_Provider_ExternalId')
BEGIN
    CREATE UNIQUE INDEX [UX_GitRepositories_UserId_Provider_ExternalId]
        ON [dbo].[GitRepositories] ([UserId], [Provider], [ExternalId]);
END;

COMMIT TRANSACTION;

SELECT [name] AS [Column], TYPE_NAME([user_type_id]) AS [Type], [max_length] AS [MaxLength], [is_nullable] AS [Nullable]
FROM sys.columns
WHERE [object_id] = OBJECT_ID(N'[dbo].[GitRepositories]')
ORDER BY [column_id];
GO
