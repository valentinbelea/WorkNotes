USE [WorkNotes.db];
GO
-- The Git references of the notes: the branches of a repository that a user linked to a reference (CR 30080, bug 1234)
-- written in one of their paragraphs, in the editor's "Git reference" option. The application looks for the branches
-- whose name contains the reference by the same rules as the text of a note (NoteReferenceRules), among the branches of a
-- repository the user imported (dbo.GitRepositories), and stores the one the user picks.
--   dbo.GitReferences: each branch once, whichever user or note links it. It is identified by the provider, the ID of its
--   repository at the provider (which survives a rename or a transfer) and its name, compared exactly (branch names differ
--   by case). The repository's name and address are the ones read the last time a user linked the branch.
--   dbo.NoteBlockGitReferences: the link of a paragraph to a branch, for one of the paragraph's references (its row in
--   dbo.WorkReferences, added there when it is not yet). It goes with the paragraph. A link whose reference the paragraph
--   no longer writes stays, without being shown, and is shown again if the paragraph writes the reference again.
-- Idempotent: the tables and their indexes are created only when they are missing.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[GitReferences]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[GitReferences]
    (
        [Id] int IDENTITY(1, 1) NOT NULL,
        [Provider] nvarchar(20) NOT NULL,
        [RepositoryExternalId] nvarchar(50) NOT NULL,
        [RepositoryFullName] nvarchar(200) NOT NULL,
        [RepositoryUrl] nvarchar(300) NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [Name] nvarchar(255) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [CreatedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_GitReferences_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_GitReferences] PRIMARY KEY ([Id]),
        -- GitProviders.
        CONSTRAINT [CK_GitReferences_Provider] CHECK ([Provider] IN (N'GitHub')),
        -- GitReferenceKinds: only branches for now.
        CONSTRAINT [CK_GitReferences_Kind] CHECK ([Kind] IN (N'Branch'))
    );
END;

-- One row per branch: the provider, the repository, the kind and the name (binary comparison: feature/x and Feature/x are two).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[GitReferences]') AND name = N'UX_GitReferences_Provider_RepositoryExternalId_Kind_Name')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_GitReferences_Provider_RepositoryExternalId_Kind_Name] ON [dbo].[GitReferences] ([Provider], [RepositoryExternalId], [Kind], [Name])');
END;

IF OBJECT_ID(N'[dbo].[NoteBlockGitReferences]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[NoteBlockGitReferences]
    (
        [Id] int IDENTITY(1, 1) NOT NULL,
        [NoteBlockId] uniqueidentifier NOT NULL,
        [GitReferenceId] int NOT NULL,
        [WorkReferenceId] int NOT NULL,
        [CreatedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_NoteBlockGitReferences_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] nvarchar(128) NOT NULL,
        CONSTRAINT [PK_NoteBlockGitReferences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NoteBlockGitReferences_NoteBlocks_NoteBlockId] FOREIGN KEY ([NoteBlockId]) REFERENCES [dbo].[NoteBlocks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_NoteBlockGitReferences_GitReferences_GitReferenceId] FOREIGN KEY ([GitReferenceId]) REFERENCES [dbo].[GitReferences] ([Id]),
        CONSTRAINT [FK_NoteBlockGitReferences_WorkReferences_WorkReferenceId] FOREIGN KEY ([WorkReferenceId]) REFERENCES [dbo].[WorkReferences] ([Id]),
        CONSTRAINT [FK_NoteBlockGitReferences_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([Id])
    );
END;

-- A paragraph links a branch once for each of its references; the key also serves the reading of a note's links.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NoteBlockGitReferences]') AND name = N'UX_NoteBlockGitReferences_NoteBlockId_GitReferenceId_WorkReferenceId')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_NoteBlockGitReferences_NoteBlockId_GitReferenceId_WorkReferenceId] ON [dbo].[NoteBlockGitReferences] ([NoteBlockId], [GitReferenceId], [WorkReferenceId])');
END;

-- The paragraphs that link a branch, and those that link a reference's branches.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NoteBlockGitReferences]') AND name = N'IX_NoteBlockGitReferences_GitReferenceId')
BEGIN
    EXEC(N'CREATE INDEX [IX_NoteBlockGitReferences_GitReferenceId] ON [dbo].[NoteBlockGitReferences] ([GitReferenceId])');
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NoteBlockGitReferences]') AND name = N'IX_NoteBlockGitReferences_WorkReferenceId')
BEGIN
    EXEC(N'CREATE INDEX [IX_NoteBlockGitReferences_WorkReferenceId] ON [dbo].[NoteBlockGitReferences] ([WorkReferenceId])');
END;

COMMIT TRANSACTION;

SELECT OBJECT_NAME([object_id]) AS [Table], [name] AS [Column], TYPE_NAME([user_type_id]) AS [Type], [max_length] AS [MaxLength], [is_nullable] AS [Nullable]
FROM sys.columns
WHERE [object_id] IN (OBJECT_ID(N'[dbo].[GitReferences]'), OBJECT_ID(N'[dbo].[NoteBlockGitReferences]'))
ORDER BY OBJECT_NAME([object_id]), [column_id];
GO
