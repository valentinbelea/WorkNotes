USE [WorkNotes.db];
GO
-- The types stored by dbo.NoteReferences and dbo.WorkReferences become keys of dbo.ReferenceTypes: the checks that
-- allowed only CR and BUG (CK_NoteReferences_ReferenceType from 004, CK_WorkReferences_ReferenceType from 006) are
-- replaced by foreign keys (FK_NoteReferences_ReferenceTypes_ReferenceType, FK_WorkReferences_ReferenceTypes_ReferenceType),
-- without cascade: a type references are stored with cannot be deleted, only deactivated (IsActive = 0). Every stored
-- type has to be in dbo.ReferenceTypes (010_InsertReferenceTypes.sql); otherwise the script stops, with nothing changed.
-- Each key is added before its check is dropped, so the column is never without a rule.
-- Idempotent: a key is added and a check dropped only when needed.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'[dbo].[ReferenceTypes]', N'U') IS NULL
    THROW 50000, N'dbo.ReferenceTypes does not exist: run 009_CreateReferenceTypes.sql and 010_InsertReferenceTypes.sql first.', 1;
IF OBJECT_ID(N'[dbo].[WorkReferences]', N'U') IS NULL
    THROW 50000, N'dbo.WorkReferences does not exist: run 006_CreateWorkReferences.sql to 008 first.', 1;

BEGIN TRANSACTION;

IF OBJECT_ID(N'[dbo].[FK_NoteReferences_ReferenceTypes_ReferenceType]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[NoteReferences] ADD CONSTRAINT [FK_NoteReferences_ReferenceTypes_ReferenceType]
        FOREIGN KEY ([ReferenceType]) REFERENCES [dbo].[ReferenceTypes] ([Code]);
END;

IF OBJECT_ID(N'[dbo].[CK_NoteReferences_ReferenceType]', N'C') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[NoteReferences] DROP CONSTRAINT [CK_NoteReferences_ReferenceType];
END;

IF OBJECT_ID(N'[dbo].[FK_WorkReferences_ReferenceTypes_ReferenceType]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorkReferences] ADD CONSTRAINT [FK_WorkReferences_ReferenceTypes_ReferenceType]
        FOREIGN KEY ([ReferenceType]) REFERENCES [dbo].[ReferenceTypes] ([Code]);
END;

IF OBJECT_ID(N'[dbo].[CK_WorkReferences_ReferenceType]', N'C') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[WorkReferences] DROP CONSTRAINT [CK_WorkReferences_ReferenceType];
END;

COMMIT TRANSACTION;

-- The rules of the two columns now: the two keys, no check on the type.
SELECT OBJECT_NAME([parent_object_id]) AS [Table], [name] AS [Constraint], [type_desc] AS [Type]
FROM sys.objects
WHERE [parent_object_id] IN (OBJECT_ID(N'[dbo].[NoteReferences]'), OBJECT_ID(N'[dbo].[WorkReferences]'))
    AND [name] IN (N'FK_NoteReferences_ReferenceTypes_ReferenceType', N'CK_NoteReferences_ReferenceType',
        N'FK_WorkReferences_ReferenceTypes_ReferenceType', N'CK_WorkReferences_ReferenceType')
ORDER BY [Table], [Constraint];
GO
