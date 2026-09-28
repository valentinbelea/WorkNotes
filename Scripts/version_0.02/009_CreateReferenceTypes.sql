USE [WorkNotes.db];
GO
-- The reference types: the prefixes a reference is written with in the notes (CR 30080, bug 1234...). The application
-- reads every text with the active ones (NoteReferenceParser), kept in memory and read again every few minutes
-- (ReferenceTypeCache.Duration). Code is the type as dbo.NoteReferences and dbo.WorkReferences store it: 1 to 10 capital
-- ASCII letters (NoteReferenceRules.ValidType); in a text it is read in any case. A type with IsActive = 0 is no longer
-- read in the texts; it stays for the references already stored with it, which keep it through a foreign key
-- (011_UpdateReferenceTypeKeys.sql). 010_InsertReferenceTypes.sql adds CR and BUG.
-- A type is added with INSERT INTO dbo.ReferenceTypes (Code, IsActive) VALUES (N'TASK', 1); a new or deactivated type
-- reaches the texts already written when 012_RefreshNoteReferences.sql runs.
-- Idempotent: the table is created only when it is missing.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[ReferenceTypes]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ReferenceTypes]
    (
        [Code] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_ReferenceTypes_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ReferenceTypes] PRIMARY KEY ([Code]),
        -- 1 to 10 capital ASCII letters, without trailing spaces (LEN leaves them out, DATALENGTH does not): with 18 digits
        -- the type fits NormalizedReference (nvarchar(30)).
        CONSTRAINT [CK_ReferenceTypes_Code] CHECK (LEN([Code]) BETWEEN 1 AND 10 AND DATALENGTH([Code]) = 2 * LEN([Code])
            AND [Code] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z]%')
    );
END;

COMMIT TRANSACTION;

SELECT [name] AS [Column], TYPE_NAME([user_type_id]) AS [Type], [max_length] AS [MaxLength], [is_nullable] AS [Nullable]
FROM sys.columns
WHERE [object_id] = OBJECT_ID(N'[dbo].[ReferenceTypes]')
ORDER BY [column_id];
GO
