USE [WorkNotes.db];
GO
-- dbo.NoteReferences gets, next to the text of each reference it stores, the id of that reference in dbo.WorkReferences
-- (006_CreateWorkReferences.sql, 007_InsertWorkReferences.sql): the column WorkReferenceId is added, filled for every
-- row from its type and number, then required, with its foreign key and index. A reference stored after 007 ran is
-- added to dbo.WorkReferences first. The texts and everything else stay as they are.
-- After this script the application writes WorkReferenceId with every reference it stores; 005 is not run again.
-- Idempotent: run again, it adds and fills only what is missing. With @Save = 0 nothing is saved, the column included.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Save bit = 1;

IF OBJECT_ID(N'[dbo].[WorkReferences]', N'U') IS NULL
    THROW 50000, N'dbo.WorkReferences does not exist: run 006_CreateWorkReferences.sql first.', 1;

-- What the transaction did; table variables keep their rows when it is rolled back (@Save = 0).
DECLARE @Added TABLE (Id int NOT NULL, NormalizedReference nvarchar(30) NOT NULL);
DECLARE @ColumnAdded bit = 0, @Filled int = 0, @Rows int = 0, @WithoutId int = 0;

BEGIN TRANSACTION;

-- 1. The column, nullable until every row has its id.
IF COL_LENGTH(N'dbo.NoteReferences', N'WorkReferenceId') IS NULL
BEGIN
    EXEC(N'ALTER TABLE [dbo].[NoteReferences] ADD [WorkReferenceId] int NULL;');
    SET @ColumnAdded = 1;
END;

-- 2. The references dbo.WorkReferences does not have yet (stored after 007, or 007 not run).
INSERT INTO [dbo].[WorkReferences] ([ReferenceType], [ReferenceNumber], [NormalizedReference])
OUTPUT inserted.[Id], inserted.[NormalizedReference] INTO @Added ([Id], [NormalizedReference])
SELECT DISTINCT r.[ReferenceType], r.[ReferenceNumber], r.[NormalizedReference]
FROM [dbo].[NoteReferences] AS r
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[WorkReferences] AS w WITH (UPDLOCK, HOLDLOCK)
    WHERE w.[ReferenceType] = r.[ReferenceType] AND w.[ReferenceNumber] = r.[ReferenceNumber])
ORDER BY r.[ReferenceType], r.[ReferenceNumber];

-- 3. Each row gets the id of its type and number. EXEC compiles the statements that use the new column once it exists.
EXEC sp_executesql N'
UPDATE r
SET [WorkReferenceId] = w.[Id]
FROM [dbo].[NoteReferences] AS r
JOIN [dbo].[WorkReferences] AS w ON w.[ReferenceType] = r.[ReferenceType] AND w.[ReferenceNumber] = r.[ReferenceNumber]
WHERE r.[WorkReferenceId] IS NULL OR r.[WorkReferenceId] <> w.[Id];
SET @Filled = @@ROWCOUNT;', N'@Filled int OUTPUT', @Filled = @Filled OUTPUT;

-- 4. Required, with its key (no cascade: a reference that paragraphs store cannot be deleted) and its index (the
-- paragraphs that store a reference).
IF EXISTS (SELECT 1 FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[dbo].[NoteReferences]') AND [name] = N'WorkReferenceId' AND [is_nullable] = 1)
BEGIN
    EXEC(N'ALTER TABLE [dbo].[NoteReferences] ALTER COLUMN [WorkReferenceId] int NOT NULL;');
END;

IF OBJECT_ID(N'[dbo].[FK_NoteReferences_WorkReferences_WorkReferenceId]', N'F') IS NULL
BEGIN
    EXEC(N'ALTER TABLE [dbo].[NoteReferences] ADD CONSTRAINT [FK_NoteReferences_WorkReferences_WorkReferenceId]
        FOREIGN KEY ([WorkReferenceId]) REFERENCES [dbo].[WorkReferences] ([Id]);');
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[NoteReferences]') AND [name] = N'IX_NoteReferences_WorkReferenceId')
BEGIN
    EXEC(N'CREATE INDEX [IX_NoteReferences_WorkReferenceId] ON [dbo].[NoteReferences] ([WorkReferenceId]);');
END;

EXEC sp_executesql N'
SELECT @Rows = COUNT(*), @WithoutId = COUNT(*) - COUNT([WorkReferenceId]) FROM [dbo].[NoteReferences];',
    N'@Rows int OUTPUT, @WithoutId int OUTPUT', @Rows = @Rows OUTPUT, @WithoutId = @WithoutId OUTPUT;

IF @Save = 1
    COMMIT TRANSACTION;
ELSE
    ROLLBACK TRANSACTION;

-- The summary: WithoutId is always 0 once the column is required.
SELECT @ColumnAdded AS ColumnAdded, (SELECT COUNT(*) FROM @Added) AS ReferencesAdded, @Filled AS RowsFilled,
    @Rows AS RowsStored, @WithoutId AS RowsWithoutId, @Save AS Saved;

-- The references this run added to dbo.WorkReferences (none when 007 ran just before).
SELECT [Id], [NormalizedReference] FROM @Added ORDER BY [Id];
GO
