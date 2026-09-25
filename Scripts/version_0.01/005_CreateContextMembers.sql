USE [WorkNotes.db];
GO
-- Members of a context and their role in it. The creator of a context is added as Owner by the application.
-- Idempotent: creates only the missing objects and keeps existing data.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[ContextMembers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ContextMembers]
    (
        [ContextId] int NOT NULL,
        [UserId] nvarchar(128) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [AddedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_ContextMembers_AddedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ContextMembers] PRIMARY KEY ([ContextId], [UserId]),
        CONSTRAINT [FK_ContextMembers_WorkContexts_ContextId] FOREIGN KEY ([ContextId]) REFERENCES [dbo].[WorkContexts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ContextMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_ContextMembers_Role] CHECK ([Role] IN (N'Owner', N'Member'))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[ContextMembers]') AND name = N'IX_ContextMembers_UserId')
BEGIN
    CREATE INDEX [IX_ContextMembers_UserId] ON [dbo].[ContextMembers] ([UserId]);
END;

COMMIT TRANSACTION;
GO
