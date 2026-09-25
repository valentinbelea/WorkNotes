USE [WorkNotes.db];

-- Execute 000_CreateDatabaseVersion.sql before this script.
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT 1
    FROM [dbo].[DatabaseVersion] WITH (UPDLOCK, HOLDLOCK)
    WHERE [Version] = N'v.0.01'
)
BEGIN
    INSERT INTO [dbo].[DatabaseVersion] ([Version])
    VALUES (N'v.0.01');
END;

COMMIT TRANSACTION;
