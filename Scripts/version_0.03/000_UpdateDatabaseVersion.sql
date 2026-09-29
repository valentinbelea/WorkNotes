USE [WorkNotes.db];
GO
-- Version 0.03: records v.0.03 in dbo.DatabaseVersion. Earlier versions are kept; the application shows the highest one.
-- Execute the version_0.01 and version_0.02 scripts before this script. Idempotent: the version is inserted only if it is missing.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT 1
    FROM [dbo].[DatabaseVersion] WITH (UPDLOCK, HOLDLOCK)
    WHERE [Version] = N'v.0.03'
)
BEGIN
    INSERT INTO [dbo].[DatabaseVersion] ([Version])
    VALUES (N'v.0.03');
END;

COMMIT TRANSACTION;
GO
