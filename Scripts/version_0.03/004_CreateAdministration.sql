-- Creates the separate administrator account and singleton GitHub configuration tables.
-- The script is idempotent and never inserts credentials or plaintext secrets.
USE [WorkNotes.db];
GO
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[AdminUsers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AdminUsers]
    (
        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AdminUsers] PRIMARY KEY,
        [UserName] nvarchar(100) COLLATE Latin1_General_100_CI_AS NOT NULL,
        [PasswordHash] nvarchar(1000) NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_AdminUsers_IsActive] DEFAULT (1),
        [CreatedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_AdminUsers_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(0) NULL,
        [LastLoginAtUtc] datetime2(0) NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'[dbo].[AdminUsers]') AND [name] = N'UX_AdminUsers_UserName')
    CREATE UNIQUE INDEX [UX_AdminUsers_UserName] ON [dbo].[AdminUsers] ([UserName]);

IF OBJECT_ID(N'[dbo].[GitHubConfigurations]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[GitHubConfigurations]
    (
        [Id] int NOT NULL CONSTRAINT [PK_GitHubConfigurations] PRIMARY KEY,
        [ProtectedClientId] nvarchar(max) NOT NULL,
        [ProtectedClientSecret] nvarchar(max) NOT NULL,
        [Scopes] nvarchar(500) NOT NULL,
        [CallbackUrl] nvarchar(1000) NOT NULL,
        [CreatedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_GitHubConfigurations_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(0) NOT NULL,
        CONSTRAINT [CK_GitHubConfigurations_SingleRow] CHECK ([Id] = 1)
    );
END;

COMMIT TRANSACTION;
GO
