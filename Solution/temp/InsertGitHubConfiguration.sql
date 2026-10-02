/*
    TEMPORARY OPERATOR TEMPLATE — DO NOT COMMIT CREDENTIAL VALUES.

    This script accepts only ASP.NET Core Data Protection payloads created by
    the target WorkNotes deployment with purpose:
        WorkNotes.GitHubConfiguration.v1

    Do not put a plaintext Client ID, Client secret, private key, PEM, access
    token or refresh token in this file. A plaintext value would be inserted by
    SQL Server but rejected by WorkNotes when IDataProtector.Unprotect is called.

    Preferred first-time setup: https://<worknotes-host>/admin/configuration
*/

USE [WorkNotes.db];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ProtectedClientId nvarchar(max) = NULL;     -- Data Protection payload, never plaintext.
DECLARE @ProtectedClientSecret nvarchar(max) = NULL; -- Data Protection payload, never plaintext.
DECLARE @Scopes nvarchar(500) = N'';                 -- Confirm before use.
DECLARE @CallbackUrl nvarchar(1000) = N'https://worknotes.eu/Account/GitHub/Callback';

IF OBJECT_ID(N'[dbo].[GitHubConfigurations]', N'U') IS NULL
    THROW 51000, 'dbo.GitHubConfigurations is missing. Apply version_0.03/004_CreateAdministration.sql first.', 1;

IF NULLIF(@ProtectedClientId, N'') IS NULL OR NULLIF(@ProtectedClientSecret, N'') IS NULL
    THROW 51001, 'Protected Data Protection payloads are required. Do not use plaintext OAuth credentials.', 1;

IF @CallbackUrl <> N'https://worknotes.eu/Account/GitHub/Callback'
    THROW 51002, 'Confirm the production callback URL before running this temporary script.', 1;

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM [dbo].[GitHubConfigurations] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = 1)
BEGIN
    UPDATE [dbo].[GitHubConfigurations]
    SET [ProtectedClientId] = @ProtectedClientId,
        [ProtectedClientSecret] = @ProtectedClientSecret,
        [Scopes] = @Scopes,
        [CallbackUrl] = @CallbackUrl,
        [UpdatedAtUtc] = SYSUTCDATETIME()
    WHERE [Id] = 1;
END
ELSE
BEGIN
    INSERT INTO [dbo].[GitHubConfigurations]
    (
        [Id], [ProtectedClientId], [ProtectedClientSecret], [Scopes], [CallbackUrl], [CreatedAtUtc], [UpdatedAtUtc]
    )
    VALUES
    (
        1, @ProtectedClientId, @ProtectedClientSecret, @Scopes, @CallbackUrl, SYSUTCDATETIME(), SYSUTCDATETIME()
    );
END;

COMMIT TRANSACTION;

SELECT
    [Id],
    CAST(CASE WHEN NULLIF([ProtectedClientId], N'') IS NULL THEN 0 ELSE 1 END AS bit) AS [HasProtectedClientId],
    CAST(CASE WHEN NULLIF([ProtectedClientSecret], N'') IS NULL THEN 0 ELSE 1 END AS bit) AS [HasProtectedClientSecret],
    [Scopes],
    [CallbackUrl],
    [CreatedAtUtc],
    [UpdatedAtUtc]
FROM [dbo].[GitHubConfigurations]
WHERE [Id] = 1;
GO
