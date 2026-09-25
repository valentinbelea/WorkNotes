USE [WorkNotes.db];
GO
-- Notes organised by context. A Journal is daily: one per owner, context and date.
-- New notes are Private; only the owner edits them. Idempotent: creates only the missing objects.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[Notes]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Notes]
    (
        [Id] int NOT NULL IDENTITY(1, 1),
        [ContextId] int NOT NULL,
        [OwnerUserId] nvarchar(128) NOT NULL,
        [NoteType] nvarchar(20) NOT NULL,
        [Title] nvarchar(200) NULL,
        [JournalDate] date NULL,
        [Visibility] nvarchar(20) NOT NULL CONSTRAINT [DF_Notes_Visibility] DEFAULT (N'Private'),
        [CreatedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_Notes_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] nvarchar(128) NOT NULL,
        [ModifiedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_Notes_ModifiedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [ModifiedByUserId] nvarchar(128) NOT NULL,
        [ArchivedAtUtc] datetime2(0) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Notes] PRIMARY KEY ([Id]),
        -- No cascade: a context that still has notes cannot be deleted.
        CONSTRAINT [FK_Notes_WorkContexts_ContextId] FOREIGN KEY ([ContextId]) REFERENCES [dbo].[WorkContexts] ([Id]),
        CONSTRAINT [FK_Notes_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_Notes_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_Notes_Users_ModifiedByUserId] FOREIGN KEY ([ModifiedByUserId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [CK_Notes_NoteType] CHECK ([NoteType] IN (N'Journal', N'Article')),
        CONSTRAINT [CK_Notes_Visibility] CHECK ([Visibility] IN (N'Private', N'Context')),
        CONSTRAINT [CK_Notes_JournalDate] CHECK ([NoteType] <> N'Journal' OR [JournalDate] IS NOT NULL)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Notes]') AND name = N'UX_Notes_DailyJournal')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Notes_DailyJournal] ON [dbo].[Notes] ([OwnerUserId], [ContextId], [JournalDate]) WHERE [NoteType] = N''Journal''');
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Notes]') AND name = N'IX_Notes_ContextId_CreatedAtUtc')
BEGIN
    CREATE INDEX [IX_Notes_ContextId_CreatedAtUtc] ON [dbo].[Notes] ([ContextId], [CreatedAtUtc] DESC);
END;

COMMIT TRANSACTION;
GO
