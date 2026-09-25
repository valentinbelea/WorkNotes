USE [WorkNotes.db];
GO
-- Paragraphs of a note. The id is created by the editor and stays with the paragraph when it is edited or moved;
-- creation and last change are audited per paragraph. The block is not the editor's visual line.
-- Idempotent: creates only the missing objects.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[NoteBlocks]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[NoteBlocks]
    (
        [Id] uniqueidentifier NOT NULL,
        [NoteId] int NOT NULL,
        [Position] int NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [ActivityDate] date NULL,
        [IsImportant] bit NOT NULL CONSTRAINT [DF_NoteBlocks_IsImportant] DEFAULT (0),
        [CreatedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_NoteBlocks_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] nvarchar(128) NOT NULL,
        [ModifiedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_NoteBlocks_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [ModifiedByUserId] nvarchar(128) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_NoteBlocks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NoteBlocks_Notes_NoteId] FOREIGN KEY ([NoteId]) REFERENCES [dbo].[Notes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_NoteBlocks_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_NoteBlocks_Users_ModifiedByUserId] FOREIGN KEY ([ModifiedByUserId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [CK_NoteBlocks_Position] CHECK ([Position] >= 0)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NoteBlocks]') AND name = N'IX_NoteBlocks_NoteId_Position')
BEGIN
    CREATE INDEX [IX_NoteBlocks_NoteId_Position] ON [dbo].[NoteBlocks] ([NoteId], [Position]);
END;

COMMIT TRANSACTION;
GO
