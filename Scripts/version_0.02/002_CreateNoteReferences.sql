USE [WorkNotes.db];
GO
-- Internal references between notes. The text of a paragraph keeps each reference as [[note:{id}|{number}]] (the id of
-- the target note and the number shown); this table lists the references of every note as of its last save: the source
-- note, the target note, the number shown and when the reference was first saved. It is the base for a future
-- "references to this note" list. Only references to another note of the same context that the owner may see are
-- listed; the application checks that, as well as the owner.
-- Deleting the source note deletes its rows (cascade). A second cascade from Notes is not allowed on the same table, so
-- the application deletes the rows pointing at a note just before deleting that note.
-- Idempotent: creates only the missing objects.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[NoteReferences]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[NoteReferences]
    (
        [Id] int IDENTITY(1, 1) NOT NULL,
        [SourceNoteId] int NOT NULL,
        [TargetNoteId] int NOT NULL,
        [DisplayText] nvarchar(20) NOT NULL,
        [CreatedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_NoteReferences_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_NoteReferences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NoteReferences_Notes_SourceNoteId] FOREIGN KEY ([SourceNoteId]) REFERENCES [dbo].[Notes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_NoteReferences_Notes_TargetNoteId] FOREIGN KEY ([TargetNoteId]) REFERENCES [dbo].[Notes] ([Id]),
        CONSTRAINT [CK_NoteReferences_OtherNote] CHECK ([SourceNoteId] <> [TargetNoteId]),
        -- The number shown: digits only (NoteReferenceRules).
        CONSTRAINT [CK_NoteReferences_DisplayText] CHECK (LEN([DisplayText]) > 0 AND [DisplayText] NOT LIKE N'%[^0-9]%')
    );
END;

-- One row per source note, target note and number, however often the text repeats that reference.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NoteReferences]') AND name = N'UX_NoteReferences_SourceNoteId_TargetNoteId_DisplayText')
BEGIN
    CREATE UNIQUE INDEX [UX_NoteReferences_SourceNoteId_TargetNoteId_DisplayText]
        ON [dbo].[NoteReferences] ([SourceNoteId], [TargetNoteId], [DisplayText]);
END;

-- The notes that refer to a note: for deleting it and for a future list of them.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NoteReferences]') AND name = N'IX_NoteReferences_TargetNoteId')
BEGIN
    CREATE INDEX [IX_NoteReferences_TargetNoteId] ON [dbo].[NoteReferences] ([TargetNoteId]);
END;

COMMIT TRANSACTION;
GO
