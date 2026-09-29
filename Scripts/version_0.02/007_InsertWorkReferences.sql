USE [WorkNotes.db];
GO
-- The references stored so far, each once, in dbo.WorkReferences (006_CreateWorkReferences.sql): every type and number
-- of dbo.NoteReferences that the table does not have yet, in the order of their type and number. Nothing else changes.
-- Then the lists: a summary and the references added, with how many paragraphs store each.
-- Idempotent: run again, it adds only the references stored since. With @Save = 0 nothing is saved: the lists show what
-- the script would add (the ids are those it would give).
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Save bit = 1;

IF OBJECT_ID(N'[dbo].[WorkReferences]', N'U') IS NULL
    THROW 50000, N'dbo.WorkReferences does not exist: run 006_CreateWorkReferences.sql first.', 1;

-- What the transaction added; a table variable keeps its rows when the transaction is rolled back (@Save = 0).
DECLARE @Added TABLE (Id int NOT NULL, NormalizedReference nvarchar(30) NOT NULL);
DECLARE @Stored int;

BEGIN TRANSACTION;

-- UPDLOCK, HOLDLOCK: a reference the application adds meanwhile is not added twice.
INSERT INTO [dbo].[WorkReferences] ([ReferenceType], [ReferenceNumber], [NormalizedReference])
OUTPUT inserted.[Id], inserted.[NormalizedReference] INTO @Added ([Id], [NormalizedReference])
SELECT DISTINCT r.[ReferenceType], r.[ReferenceNumber], r.[NormalizedReference]
FROM [dbo].[NoteReferences] AS r
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[WorkReferences] AS w WITH (UPDLOCK, HOLDLOCK)
    WHERE w.[ReferenceType] = r.[ReferenceType] AND w.[ReferenceNumber] = r.[ReferenceNumber])
ORDER BY r.[ReferenceType], r.[ReferenceNumber];

SELECT @Stored = COUNT(*) FROM [dbo].[WorkReferences];

IF @Save = 1
    COMMIT TRANSACTION;
ELSE
    ROLLBACK TRANSACTION;

SELECT
    (SELECT COUNT(*) FROM [dbo].[NoteReferences]) AS StoredParagraphReferences,
    (SELECT COUNT(*) FROM @Added) AS ReferencesAdded,
    @Stored AS ReferencesStored,
    @Save AS Saved;

-- The references added, with the paragraphs that store each (one row per paragraph and reference in NoteReferences).
SELECT a.[Id], a.[NormalizedReference], COUNT(r.[Id]) AS Paragraphs
FROM @Added AS a
LEFT JOIN [dbo].[NoteReferences] AS r ON r.[NormalizedReference] = a.[NormalizedReference]
GROUP BY a.[Id], a.[NormalizedReference]
ORDER BY a.[Id];
GO
