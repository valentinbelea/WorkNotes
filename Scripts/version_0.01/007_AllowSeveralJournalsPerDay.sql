USE [WorkNotes.db];
GO
-- Several journals per day are allowed: removes the one-journal-per-owner-context-and-day index created by 006.
-- Idempotent; notes are not changed.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Notes]') AND name = N'UX_Notes_DailyJournal')
BEGIN
    DROP INDEX [UX_Notes_DailyJournal] ON [dbo].[Notes];
END;

COMMIT TRANSACTION;
GO
