USE [WorkNotes.db];
GO
-- Manual order of the notes on the board: [Order] ascending, then the last change and the creation, newest first.
-- ORDER is a reserved word, so the column is always written as [Order].
-- The column is added once: the existing notes are numbered per context in their current board order (journals, then
-- articles, each by last change and id, newest first), so every month keeps its arrangement. Idempotent.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Notes', N'Order') IS NULL
BEGIN
    ALTER TABLE [dbo].[Notes] ADD [Order] int NOT NULL CONSTRAINT [DF_Notes_Order] DEFAULT (0);

    -- EXEC compiles the statement once the column exists. The last change is the board's ISNULL(modified, created):
    -- a note modified no later than its creation counts as never changed.
    EXEC(N'
        WITH [Numbered] AS
        (
            SELECT [Order], ROW_NUMBER() OVER (
                PARTITION BY [ContextId]
                ORDER BY CASE WHEN [NoteType] = N''Journal'' THEN 0 ELSE 1 END,
                    CASE WHEN [ModifiedAtUtc] > [CreatedAtUtc] THEN [ModifiedAtUtc] ELSE [CreatedAtUtc] END DESC,
                    [Id] DESC) AS [Position]
            FROM [dbo].[Notes]
        )
        UPDATE [Numbered] SET [Order] = [Position];');
END;

-- A new note takes the smallest order of its context minus one; the index serves that lookup and its lock.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Notes]') AND name = N'IX_Notes_ContextId_Order')
BEGIN
    EXEC(N'CREATE INDEX [IX_Notes_ContextId_Order] ON [dbo].[Notes] ([ContextId], [Order])');
END;

COMMIT TRANSACTION;
GO
