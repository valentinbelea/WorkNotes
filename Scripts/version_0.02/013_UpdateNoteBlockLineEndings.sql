USE [WorkNotes.db];
GO
-- Line endings of the stored paragraphs become \n, as the application saves them (NoteRules.NormalizeBlockContent):
-- \r\n and a lone \r are replaced by \n in dbo.NoteBlocks.Content. Paragraphs written before the save normalized
-- them, or by other tools, may hold \r\n; the editor counts a line break as one character, so their text and links
-- were placed wrong and typing in the note failed. Only the line endings change: the audit (ModifiedAtUtc,
-- ModifiedByUserId) stays as it is, since the text the user wrote is the same. The references of the paragraphs do not
-- change either (a reference is never split by a line break).
-- Shows the notes with such paragraphs and how many of their paragraphs change. With @Save = 0 nothing is saved.
-- Idempotent: a second run finds nothing to change.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Save bit = 1;

BEGIN TRANSACTION;

SELECT b.[NoteId], n.[Title], COUNT(*) AS [Paragraphs]
FROM [dbo].[NoteBlocks] AS b WITH (UPDLOCK, HOLDLOCK)
JOIN [dbo].[Notes] AS n ON n.[Id] = b.[NoteId]
WHERE CHARINDEX(NCHAR(13), b.[Content]) > 0
GROUP BY b.[NoteId], n.[Title]
ORDER BY b.[NoteId];

IF @Save = 1
BEGIN
    UPDATE [dbo].[NoteBlocks]
    SET [Content] = REPLACE(REPLACE([Content], NCHAR(13) + NCHAR(10), NCHAR(10)), NCHAR(13), NCHAR(10))
    WHERE CHARINDEX(NCHAR(13), [Content]) > 0;

    SELECT @@ROWCOUNT AS [ParagraphsUpdated];
    COMMIT TRANSACTION;
END
ELSE
    ROLLBACK TRANSACTION;
GO
