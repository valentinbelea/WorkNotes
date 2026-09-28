USE [WorkNotes.db];
GO
-- The references of the notes, each once: a CR or a bug is its type and its number (CR 30080, BUG 1234), whatever the
-- text a paragraph writes it with (CR_30080, cr-30080, Bug-1234...). The type and the number are the key; the row has
-- its own id, which dbo.NoteReferences gets next to the text of each reference it stores
-- (008_UpdateNoteReferencesWorkReferenceId.sql). 007_InsertWorkReferences.sql adds the references stored so far; the
-- application adds a reference the first time a paragraph stores it. A row stays when no paragraph writes its reference
-- any more, so a reference keeps its id.
-- Idempotent: the table and its indexes are created only when they are missing.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[WorkReferences]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkReferences]
    (
        [Id] int IDENTITY(1, 1) NOT NULL,
        [ReferenceType] nvarchar(20) NOT NULL,
        [ReferenceNumber] bigint NOT NULL,
        [NormalizedReference] nvarchar(30) NOT NULL,
        [CreatedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_WorkReferences_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_WorkReferences] PRIMARY KEY ([Id]),
        -- NoteReferenceTypes.
        CONSTRAINT [CK_WorkReferences_ReferenceType] CHECK ([ReferenceType] IN (N'CR', N'BUG')),
        CONSTRAINT [CK_WorkReferences_ReferenceNumber] CHECK ([ReferenceNumber] >= 0 AND [ReferenceNumber] < 1000000000000000000),
        -- CR:30080, BUG:1234: the type and the number without leading zeros (NoteReferenceRules.Normalize).
        CONSTRAINT [CK_WorkReferences_NormalizedReference] CHECK ([NormalizedReference] = [ReferenceType] + N':' + CONVERT(nvarchar(20), [ReferenceNumber]))
    );
END;

-- The key of a reference: its type and its number, once.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[WorkReferences]') AND name = N'UX_WorkReferences_ReferenceType_ReferenceNumber')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_WorkReferences_ReferenceType_ReferenceNumber] ON [dbo].[WorkReferences] ([ReferenceType], [ReferenceNumber])');
END;

-- The same key as the application writes it (CR:30080), which it looks the references up by.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[WorkReferences]') AND name = N'UX_WorkReferences_NormalizedReference')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_WorkReferences_NormalizedReference] ON [dbo].[WorkReferences] ([NormalizedReference])');
END;

COMMIT TRANSACTION;

SELECT [name] AS [Column], TYPE_NAME([user_type_id]) AS [Type], [max_length] AS [MaxLength], [is_nullable] AS [Nullable]
FROM sys.columns
WHERE [object_id] = OBJECT_ID(N'[dbo].[WorkReferences]')
ORDER BY [column_id];
GO
