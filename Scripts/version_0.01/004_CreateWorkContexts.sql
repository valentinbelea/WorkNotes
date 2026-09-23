USE [WorkNotes.db];
GO
-- Contexts (e.g. SD Worx, TopDev) in which notes are organised.
-- Idempotent: creates only the missing objects and keeps existing data.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[WorkContexts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkContexts]
    (
        [Id] int NOT NULL IDENTITY(1, 1),
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(1000) NULL,
        CONSTRAINT [PK_WorkContexts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[WorkContexts]') AND name = N'UX_WorkContexts_Name')
BEGIN
    CREATE UNIQUE INDEX [UX_WorkContexts_Name] ON [dbo].[WorkContexts] ([Name]);
END;

COMMIT TRANSACTION;
GO
