USE [WorkNotes.db];

SET XACT_ABORT ON;

IF OBJECT_ID(N'[dbo].[DatabaseVersion]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DatabaseVersion]
    (
        [Version] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_DatabaseVersion] PRIMARY KEY ([Version])
    );
END;
