-- Splits the protected GitHub OAuth configuration into Development and Production rows.
-- Existing singleton data becomes Production; no credentials are deleted or rewritten.
USE [WorkNotes.db];
GO
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[GitHubConfigurations]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.GitHubConfigurations', N'EnvironmentName') IS NULL
        ALTER TABLE [dbo].[GitHubConfigurations] ADD [EnvironmentName] nvarchar(50) NULL;

    UPDATE [dbo].[GitHubConfigurations]
       SET [EnvironmentName] = N'Production'
     WHERE [EnvironmentName] IS NULL;

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[GitHubConfigurations]')
          AND [name] = N'CK_GitHubConfigurations_SingleRow'
    )
        ALTER TABLE [dbo].[GitHubConfigurations] DROP CONSTRAINT [CK_GitHubConfigurations_SingleRow];

    IF COLUMNPROPERTY(OBJECT_ID(N'[dbo].[GitHubConfigurations]'), N'EnvironmentName', 'AllowsNull') = 1
        ALTER TABLE [dbo].[GitHubConfigurations] ALTER COLUMN [EnvironmentName] nvarchar(50) NOT NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[GitHubConfigurations]')
          AND [name] = N'CK_GitHubConfigurations_Environment'
    )
        ALTER TABLE [dbo].[GitHubConfigurations] ADD CONSTRAINT [CK_GitHubConfigurations_Environment]
            CHECK (([Id] = 1 AND [EnvironmentName] = N'Production') OR
                   ([Id] = 2 AND [EnvironmentName] = N'Development'));

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE [object_id] = OBJECT_ID(N'[dbo].[GitHubConfigurations]')
          AND [name] = N'UX_GitHubConfigurations_EnvironmentName'
    )
        CREATE UNIQUE INDEX [UX_GitHubConfigurations_EnvironmentName]
            ON [dbo].[GitHubConfigurations] ([EnvironmentName]);
END;

COMMIT TRANSACTION;
GO
