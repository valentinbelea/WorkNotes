USE [WorkNotes.db];
GO
-- The reference types the application has read so far, CR and BUG, active, in dbo.ReferenceTypes
-- (009_CreateReferenceTypes.sql); and any other type already stored in dbo.NoteReferences or dbo.WorkReferences (none
-- until now: their checks allow only CR and BUG), so that 011_UpdateReferenceTypeKeys.sql can make them keys.
-- A type already in the table stays as it is, IsActive included. Then the list of the types.
-- Idempotent: only the missing types are added, in a transaction, with UPDLOCK and HOLDLOCK.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'[dbo].[ReferenceTypes]', N'U') IS NULL
    THROW 50000, N'dbo.ReferenceTypes does not exist: run 009_CreateReferenceTypes.sql first.', 1;
IF OBJECT_ID(N'[dbo].[WorkReferences]', N'U') IS NULL
    THROW 50000, N'dbo.WorkReferences does not exist: run 006_CreateWorkReferences.sql to 008 first.', 1;

DECLARE @Added TABLE (Code nvarchar(20) NOT NULL);

BEGIN TRANSACTION;

INSERT INTO [dbo].[ReferenceTypes] ([Code], [IsActive])
OUTPUT inserted.[Code] INTO @Added ([Code])
SELECT t.[Code], 1
FROM
(
    SELECT N'CR' AS [Code]
    UNION SELECT N'BUG'
    UNION SELECT [ReferenceType] FROM [dbo].[NoteReferences]
    UNION SELECT [ReferenceType] FROM [dbo].[WorkReferences]
) AS t
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ReferenceTypes] AS r WITH (UPDLOCK, HOLDLOCK) WHERE r.[Code] = t.[Code]);

COMMIT TRANSACTION;

SELECT r.[Code], r.[IsActive], r.[CreatedAtUtc], CAST(CASE WHEN a.[Code] IS NULL THEN 0 ELSE 1 END AS bit) AS [Added]
FROM [dbo].[ReferenceTypes] AS r
LEFT JOIN @Added AS a ON a.[Code] = r.[Code]
ORDER BY r.[Code];
GO
