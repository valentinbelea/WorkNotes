USE [WorkNotes.db];
GO
-- The Git accounts a WorkNotes user has connected through OAuth: one row per user and provider (only GitHub for now).
-- The tokens are never stored in clear: the application encrypts them with ASP.NET Core Data Protection before saving
-- them, so ProtectedAccessToken and ProtectedRefreshToken can be read back only with the application's keys.
-- The refresh token and the expiry dates exist only for providers that issue expiring tokens (a GitHub App); an OAuth App
-- token does not expire and has no refresh token. Disconnecting deletes the row; deleting the user deletes it too.
-- Idempotent: the table is created only when it is missing.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[GitConnections]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[GitConnections]
    (
        [UserId] nvarchar(128) NOT NULL,
        [Provider] nvarchar(20) NOT NULL,
        [AccountId] nvarchar(50) NOT NULL,
        [AccountLogin] nvarchar(100) NOT NULL,
        [ProtectedAccessToken] nvarchar(max) NOT NULL,
        [ProtectedRefreshToken] nvarchar(max) NULL,
        [AccessTokenExpiresAtUtc] datetime2(0) NULL,
        [RefreshTokenExpiresAtUtc] datetime2(0) NULL,
        [Scopes] nvarchar(500) NULL,
        [ConnectedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_GitConnections_ConnectedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [ValidatedAtUtc] datetime2(0) NOT NULL,
        CONSTRAINT [PK_GitConnections] PRIMARY KEY ([UserId], [Provider]),
        CONSTRAINT [FK_GitConnections_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_GitConnections_Provider] CHECK ([Provider] IN (N'GitHub'))
    );
END;

COMMIT TRANSACTION;

SELECT [name] AS [Column], TYPE_NAME([user_type_id]) AS [Type], [max_length] AS [MaxLength], [is_nullable] AS [Nullable]
FROM sys.columns
WHERE [object_id] = OBJECT_ID(N'[dbo].[GitConnections]')
ORDER BY [column_id];
GO
