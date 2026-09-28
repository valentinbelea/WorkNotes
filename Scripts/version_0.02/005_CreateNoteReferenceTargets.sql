USE [WorkNotes.db];
GO
-- Internal references, several notes per reference. A reference is a CR or a bug written in a paragraph with its number
-- (CR 30080, CR-30080, CR_30080, CR30080, bug 1234, Bug-1234...: NoteReferenceRules); it opens every note of the board
-- whose title names the same type and number, other than the paragraph's own note: one, two or more.
-- dbo.NoteReferences keeps one row per paragraph and reference (the type, the number, the text as it is first written in
-- the paragraph and the normalized form, CR:30080); the notes each reference opens move to dbo.NoteReferenceTargets, one
-- row per reference and note (NoteReferenceId, TargetNoteId). The text of the paragraphs is never changed to hold a link.
--
-- The script follows 004_ReplaceNoteReferences.sql and takes its place for the runs that come later: 004 is not run again
-- after it. The texts are read first; then, in one transaction:
--   1. dbo.NoteReferenceTargets is created, with its keys and index;
--   2. the note of each row of dbo.NoteReferences moves there, and the column NoteReferences.TargetNoteId goes, with its
--      key and index;
--   3. every paragraph that can have references is read (those of notes that are not archived, whose owner still belongs
--      to the note's context), as well as every title, and the rows are made from them with the rule above: a reference
--      several notes have in their title, which had no link, now opens all of them. Missing rows are added, rows no
--      longer valid are removed, the others stay. A paragraph changed while the script runs keeps the rows its save gave
--      it.
-- Then the lists: a summary, the references without a note, those with several notes (each note listed) and the CRs
-- journal.
-- Idempotent: run again, it changes only what no longer matches the text and the titles, and lists the state again.
-- With @Save = 0 nothing is saved, the table included: the lists show what the script would do.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Save bit = 1;

-- The table of 004 is needed: the one of 002 (SourceNoteId, DisplayText) has no paragraph to hang the rows on.
IF COL_LENGTH(N'dbo.NoteReferences', N'NoteBlockId') IS NULL
    THROW 50000, N'dbo.NoteReferences does not have the columns of 004_ReplaceNoteReferences.sql: run 004 first.', 1;

IF OBJECT_ID(N'tempdb..#WordRanges') IS NOT NULL DROP TABLE #WordRanges;
IF OBJECT_ID(N'tempdb..#Texts') IS NOT NULL DROP TABLE #Texts;
IF OBJECT_ID(N'tempdb..#Found') IS NOT NULL DROP TABLE #Found;
IF OBJECT_ID(N'tempdb..#Titles') IS NOT NULL DROP TABLE #Titles;
IF OBJECT_ID(N'tempdb..#Mentions') IS NOT NULL DROP TABLE #Mentions;
IF OBJECT_ID(N'tempdb..#Candidates') IS NOT NULL DROP TABLE #Candidates;
IF OBJECT_ID(N'tempdb..#Wanted') IS NOT NULL DROP TABLE #Wanted;
IF OBJECT_ID(N'tempdb..#Resolved') IS NOT NULL DROP TABLE #Resolved;
IF OBJECT_ID(N'tempdb..#TargetsMoved') IS NOT NULL DROP TABLE #TargetsMoved;
IF OBJECT_ID(N'tempdb..#TargetsRemoved') IS NOT NULL DROP TABLE #TargetsRemoved;
IF OBJECT_ID(N'tempdb..#ReferencesRemoved') IS NOT NULL DROP TABLE #ReferencesRemoved;
IF OBJECT_ID(N'tempdb..#ReferencesUpdated') IS NOT NULL DROP TABLE #ReferencesUpdated;
IF OBJECT_ID(N'tempdb..#ReferencesInserted') IS NOT NULL DROP TABLE #ReferencesInserted;
IF OBJECT_ID(N'tempdb..#TargetsInserted') IS NOT NULL DROP TABLE #TargetsInserted;

-- What the transaction did; table variables keep their rows when it is rolled back (@Save = 0).
DECLARE @TargetsMoved TABLE (NoteReferenceId int NOT NULL, TargetNoteId int NOT NULL);
DECLARE @TargetsRemoved TABLE (NoteReferenceId int NOT NULL, TargetNoteId int NOT NULL);
DECLARE @ReferencesRemoved TABLE (Id int NOT NULL);
DECLARE @ReferencesUpdated TABLE (Id int NOT NULL);
DECLARE @ReferencesInserted TABLE (Id int NOT NULL);
DECLARE @TargetsInserted TABLE (NoteReferenceId int NOT NULL, TargetNoteId int NOT NULL);
DECLARE @ReferencesStored int, @TargetsStored int;

-- 1. The characters that make a word (NoteReferenceRules: letters, digits and combining marks, \p{L} \p{N} \p{M}), as in
-- 004. SQL Server has no Unicode categories: these ranges give the Latin letters, the digits and the combining marks, and
-- the other letters are those with a case (UPPER <> LOWER). That covers Latin, Greek and Cyrillic text; a paragraph in
-- another script is read again by the application at its next save.
CREATE TABLE #WordRanges (FromCode int NOT NULL, ToCode int NOT NULL);
INSERT INTO #WordRanges (FromCode, ToCode)
VALUES
    (48, 57), (65, 90), (97, 122),              -- 0-9, A-Z, a-z
    (170, 170), (178, 179), (181, 181), (185, 186), (188, 190), -- U+00AA, U+00B2-U+00BE: ordinals, superscripts, fractions
    (192, 214), (216, 246), (248, 705),         -- Latin letters with diacritics, extended Latin and IPA, U+00C0-U+02C1
    (710, 721), (736, 740), (748, 748), (750, 750), -- modifier letters, U+02C6-U+02EE
    (768, 879),                                 -- combining diacritical marks, U+0300-U+036F
    (1155, 1161),                               -- Cyrillic combining marks, U+0483-U+0489
    (1632, 1641), (1776, 1785),                 -- Arabic-Indic digits
    (6832, 6911), (7616, 7679),                 -- combining marks, extended and supplement
    (8304, 8305), (8308, 8313), (8319, 8329),   -- superscript and subscript digits and letters, U+2070-U+2089
    (8400, 8447),                               -- combining marks for symbols, U+20D0-U+20FF
    (8528, 8584),                               -- fractions and Roman numerals, U+2150-U+2188
    (65056, 65071),                             -- combining half marks, U+FE20-U+FE2F
    (65296, 65305);                             -- fullwidth digits, U+FF10-U+FF19

-- 2. The texts read: the paragraphs that can have references and the titles of the notes that are not archived, the
-- notes a reference can open. The collation is the one of the search below: case-insensitive, accent-, kana- and
-- width-sensitive (CR and cr are the same word, a fullwidth CR is not).
CREATE TABLE #Texts
(
    TextId int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    NoteId int NOT NULL,
    NoteBlockId uniqueidentifier NULL,
    RowVersion binary(8) NULL,
    Content nvarchar(max) COLLATE Latin1_General_100_CI_AS_KS_WS NOT NULL
);
INSERT INTO #Texts (NoteId, NoteBlockId, RowVersion, Content)
SELECT b.NoteId, b.Id, CAST(b.RowVersion AS binary(8)), b.Content COLLATE Latin1_General_100_CI_AS_KS_WS
FROM dbo.NoteBlocks AS b
JOIN dbo.Notes AS n ON n.Id = b.NoteId
WHERE n.ArchivedAtUtc IS NULL
    AND EXISTS (SELECT 1 FROM dbo.ContextMembers AS m WHERE m.ContextId = n.ContextId AND m.UserId = n.OwnerUserId);

INSERT INTO #Texts (NoteId, NoteBlockId, RowVersion, Content)
SELECT n.Id, NULL, NULL, n.Title COLLATE Latin1_General_100_CI_AS_KS_WS
FROM dbo.Notes AS n
WHERE n.ArchivedAtUtc IS NULL AND n.Title IS NOT NULL;

-- 3. The references in the texts. Every place where CR or BUG is written, in any case, is checked as the application
-- checks it: the type in ASCII letters, no word character right before it; then 1 to 50 spaces (or no-break spaces),
-- one - or _, or nothing; then 1 to 18 ASCII digits with no word character right after them.
CREATE TABLE #Found
(
    TextId int NOT NULL,
    Position bigint NOT NULL,
    ReferenceText nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL,
    ReferenceType nvarchar(20) COLLATE DATABASE_DEFAULT NOT NULL,
    ReferenceNumber bigint NOT NULL,
    NormalizedReference nvarchar(30) COLLATE DATABASE_DEFAULT NOT NULL
);

WITH [Types] AS (SELECT [Word] FROM (VALUES (N'CR'), (N'BUG')) AS v ([Word])),
[Occurrences] AS
(
    SELECT t.TextId, y.Word, CAST(CHARINDEX(y.Word, t.Content) AS bigint) AS Position
    FROM #Texts AS t
    CROSS JOIN [Types] AS y
    UNION ALL
    SELECT o.TextId, o.Word, CAST(CHARINDEX(o.Word, t.Content, o.Position + 1) AS bigint)
    FROM [Occurrences] AS o
    JOIN #Texts AS t ON t.TextId = o.TextId
    WHERE o.Position > 0
)
INSERT INTO #Found (TextId, Position, ReferenceText, ReferenceType, ReferenceNumber, NormalizedReference)
SELECT o.TextId, o.Position, SUBSTRING(t.Content, o.Position, LEN(o.Word) + s.SeparatorLength + d.Digits), o.Word,
    x.ReferenceNumber, o.Word + N':' + CONVERT(nvarchar(20), x.ReferenceNumber)
FROM [Occurrences] AS o
JOIN #Texts AS t ON t.TextId = o.TextId
CROSS APPLY (SELECT
    CASE WHEN o.Position > 1 THEN SUBSTRING(t.Content, o.Position - 1, 1) ELSE N'' END AS Before,
    SUBSTRING(t.Content, o.Position + LEN(o.Word), 52) AS Rest) AS e
CROSS APPLY (SELECT PATINDEX(N'%[^ ' + NCHAR(160) + N']%', e.Rest COLLATE Latin1_General_100_BIN2 + N'x') - 1 AS Spaces) AS sp
CROSS APPLY (SELECT CASE
    WHEN sp.Spaces > 0 THEN sp.Spaces
    WHEN LEFT(e.Rest, 1) COLLATE Latin1_General_100_BIN2 IN (N'-', N'_') THEN 1
    ELSE 0 END AS SeparatorLength) AS s
CROSS APPLY (SELECT o.Position + LEN(o.Word) + s.SeparatorLength AS NumberStart) AS ns
CROSS APPLY (SELECT PATINDEX(N'%[^0-9]%', SUBSTRING(t.Content, ns.NumberStart, 19) COLLATE Latin1_General_100_BIN2 + N'x') - 1 AS Digits) AS d
CROSS APPLY (SELECT
    SUBSTRING(t.Content, ns.NumberStart + d.Digits, 1) AS After,
    CAST(CASE WHEN d.Digits BETWEEN 1 AND 18 THEN SUBSTRING(t.Content, ns.NumberStart, d.Digits) END AS bigint) AS ReferenceNumber) AS x
WHERE o.Position > 0
    AND SUBSTRING(t.Content, o.Position, LEN(o.Word)) COLLATE Latin1_General_100_BIN2
        LIKE CASE o.Word WHEN N'CR' THEN N'[Cc][Rr]' ELSE N'[Bb][Uu][Gg]' END
    AND sp.Spaces <= 50
    AND d.Digits BETWEEN 1 AND 18
    AND NOT (UPPER(e.Before) COLLATE Latin1_General_100_BIN2 <> LOWER(e.Before) COLLATE Latin1_General_100_BIN2
        OR EXISTS (SELECT 1 FROM #WordRanges AS w WHERE UNICODE(e.Before) BETWEEN w.FromCode AND w.ToCode))
    AND NOT (UPPER(x.After) COLLATE Latin1_General_100_BIN2 <> LOWER(x.After) COLLATE Latin1_General_100_BIN2
        OR EXISTS (SELECT 1 FROM #WordRanges AS w WHERE UNICODE(x.After) BETWEEN w.FromCode AND w.ToCode))
OPTION (MAXRECURSION 0);

-- 4. The references each title names, and those of each paragraph: once per paragraph, with the text they are first
-- written with and how many times the paragraph writes them.
SELECT DISTINCT t.NoteId, f.NormalizedReference
INTO #Titles
FROM #Found AS f
JOIN #Texts AS t ON t.TextId = f.TextId
WHERE t.NoteBlockId IS NULL;

SELECT t.NoteBlockId, t.NoteId, t.RowVersion, f.NormalizedReference, f.ReferenceType, f.ReferenceNumber, f.ReferenceText, f.Occurrences
INTO #Mentions
FROM
(
    SELECT TextId, NormalizedReference, ReferenceType, ReferenceNumber, ReferenceText,
        ROW_NUMBER() OVER (PARTITION BY TextId, NormalizedReference ORDER BY Position) AS [Place],
        COUNT(*) OVER (PARTITION BY TextId, NormalizedReference) AS Occurrences
    FROM #Found
) AS f
JOIN #Texts AS t ON t.TextId = f.TextId
WHERE t.NoteBlockId IS NOT NULL AND f.[Place] = 1;

-- 5. For each reference of a paragraph, the notes of the board the paragraph's owner may see that have it in their
-- title: the owner's own notes and those shared with the context, the paragraph's own note included. Wanted: all of
-- them but the paragraph's own note, one row each. Linked: at least one such note. WithoutTarget: none at all. OwnNote:
-- only the paragraph's own note.
SELECT m.NoteBlockId, m.NormalizedReference, c.Id AS CandidateNoteId
INTO #Candidates
FROM #Mentions AS m
JOIN dbo.Notes AS s ON s.Id = m.NoteId
JOIN #Titles AS tt ON tt.NormalizedReference = m.NormalizedReference
JOIN dbo.Notes AS c ON c.Id = tt.NoteId AND c.ContextId = s.ContextId AND (c.OwnerUserId = s.OwnerUserId OR c.Visibility = N'Context');

SELECT c.NoteBlockId, c.NormalizedReference, c.CandidateNoteId AS TargetNoteId
INTO #Wanted
FROM #Candidates AS c
JOIN #Mentions AS m ON m.NoteBlockId = c.NoteBlockId AND m.NormalizedReference = c.NormalizedReference
WHERE c.CandidateNoteId <> m.NoteId;

SELECT m.NoteBlockId, m.NoteId, m.RowVersion, m.NormalizedReference, m.ReferenceType, m.ReferenceNumber, m.ReferenceText,
    m.Occurrences, k.Candidates, g.Targets,
    CASE WHEN k.Candidates = 0 THEN N'WithoutTarget'
        WHEN g.Targets = 0 THEN N'OwnNote'
        ELSE N'Linked' END AS [Status]
INTO #Resolved
FROM #Mentions AS m
OUTER APPLY (SELECT COUNT(*) AS Candidates FROM #Candidates AS c
    WHERE c.NoteBlockId = m.NoteBlockId AND c.NormalizedReference = m.NormalizedReference) AS k
OUTER APPLY (SELECT COUNT(*) AS Targets FROM #Wanted AS w
    WHERE w.NoteBlockId = m.NoteBlockId AND w.NormalizedReference = m.NormalizedReference) AS g;

CREATE TABLE #TargetsMoved (NoteReferenceId int NOT NULL, TargetNoteId int NOT NULL);
CREATE TABLE #TargetsRemoved (NoteReferenceId int NOT NULL, TargetNoteId int NOT NULL);
CREATE TABLE #ReferencesRemoved (Id int NOT NULL);
CREATE TABLE #ReferencesUpdated (Id int NOT NULL);
CREATE TABLE #ReferencesInserted (Id int NOT NULL);
CREATE TABLE #TargetsInserted (NoteReferenceId int NOT NULL, TargetNoteId int NOT NULL);

BEGIN TRANSACTION;

-- 6. The table of the notes a reference opens. Deleting a reference deletes its rows (cascade); the note a row opens
-- stays. A second cascade from Notes (through TargetNoteId) is not allowed, so the application deletes the rows that open
-- a note just before deleting the note.
IF OBJECT_ID(N'[dbo].[NoteReferenceTargets]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[NoteReferenceTargets]
    (
        [NoteReferenceId] int NOT NULL,
        [TargetNoteId] int NOT NULL,
        -- When the reference got this note.
        [CreatedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_NoteReferenceTargets_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        -- One row per reference and note; also the index of a reference's notes.
        CONSTRAINT [PK_NoteReferenceTargets] PRIMARY KEY ([NoteReferenceId], [TargetNoteId]),
        CONSTRAINT [FK_NoteReferenceTargets_NoteReferences_NoteReferenceId] FOREIGN KEY ([NoteReferenceId]) REFERENCES [dbo].[NoteReferences] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_NoteReferenceTargets_Notes_TargetNoteId] FOREIGN KEY ([TargetNoteId]) REFERENCES [dbo].[Notes] ([Id])
    );
END;

-- EXEC compiles each statement once the new table is in place (and the old column still there, or already gone).
-- The rows that open a note: for deleting it and for a future list of the notes that refer to it.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NoteReferenceTargets]') AND name = N'IX_NoteReferenceTargets_TargetNoteId')
BEGIN
    EXEC(N'CREATE INDEX [IX_NoteReferenceTargets_TargetNoteId] ON [dbo].[NoteReferenceTargets] ([TargetNoteId])');
END;

-- 7. The note of each row of 004 moves to the new table, with the time the paragraph got it; then the column goes, with
-- its key and index.
IF COL_LENGTH(N'dbo.NoteReferences', N'TargetNoteId') IS NOT NULL
BEGIN
    EXEC(N'
    INSERT INTO [dbo].[NoteReferenceTargets] (NoteReferenceId, TargetNoteId, CreatedAtUtc)
    OUTPUT inserted.NoteReferenceId, inserted.TargetNoteId INTO #TargetsMoved (NoteReferenceId, TargetNoteId)
    SELECT r.Id, r.TargetNoteId, r.CreatedAtUtc
    FROM [dbo].[NoteReferences] AS r
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[NoteReferenceTargets] AS g WHERE g.NoteReferenceId = r.Id AND g.TargetNoteId = r.TargetNoteId);');

    IF OBJECT_ID(N'[dbo].[FK_NoteReferences_Notes_TargetNoteId]', N'F') IS NOT NULL
        EXEC(N'ALTER TABLE [dbo].[NoteReferences] DROP CONSTRAINT [FK_NoteReferences_Notes_TargetNoteId];');
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NoteReferences]') AND name = N'IX_NoteReferences_TargetNoteId')
        EXEC(N'DROP INDEX [IX_NoteReferences_TargetNoteId] ON [dbo].[NoteReferences];');
    EXEC(N'ALTER TABLE [dbo].[NoteReferences] DROP COLUMN [TargetNoteId];');
END;

-- 8. The rows. Removed: those of paragraphs that can no longer have references (archived note, owner no longer in the
-- context), and those of the paragraphs read, unchanged since, that the rule above does not give: first the notes a
-- reference no longer opens, then the references that open none. The text of a kept reference follows the paragraph;
-- the missing references and notes are added. A paragraph changed since it was read, or added since, keeps its rows.
EXEC(N'
DELETE g
OUTPUT deleted.NoteReferenceId, deleted.TargetNoteId INTO #TargetsRemoved (NoteReferenceId, TargetNoteId)
FROM [dbo].[NoteReferenceTargets] AS g
JOIN [dbo].[NoteReferences] AS r ON r.Id = g.NoteReferenceId
JOIN [dbo].[NoteBlocks] AS b ON b.Id = r.NoteBlockId
JOIN [dbo].[Notes] AS n ON n.Id = b.NoteId
LEFT JOIN #Texts AS t ON t.NoteBlockId = r.NoteBlockId
WHERE (n.ArchivedAtUtc IS NOT NULL
        OR NOT EXISTS (SELECT 1 FROM [dbo].[ContextMembers] AS m WHERE m.ContextId = n.ContextId AND m.UserId = n.OwnerUserId)
        OR t.RowVersion = CAST(b.RowVersion AS binary(8)))
    AND NOT EXISTS (SELECT 1 FROM #Wanted AS w
        WHERE w.NoteBlockId = r.NoteBlockId AND w.NormalizedReference = r.NormalizedReference AND w.TargetNoteId = g.TargetNoteId);');

EXEC(N'
DELETE r
OUTPUT deleted.Id INTO #ReferencesRemoved (Id)
FROM [dbo].[NoteReferences] AS r
JOIN [dbo].[NoteBlocks] AS b ON b.Id = r.NoteBlockId
JOIN [dbo].[Notes] AS n ON n.Id = b.NoteId
LEFT JOIN #Texts AS t ON t.NoteBlockId = r.NoteBlockId
WHERE (n.ArchivedAtUtc IS NOT NULL
        OR NOT EXISTS (SELECT 1 FROM [dbo].[ContextMembers] AS m WHERE m.ContextId = n.ContextId AND m.UserId = n.OwnerUserId)
        OR t.RowVersion = CAST(b.RowVersion AS binary(8)))
    AND NOT EXISTS (SELECT 1 FROM #Wanted AS w
        WHERE w.NoteBlockId = r.NoteBlockId AND w.NormalizedReference = r.NormalizedReference);');

EXEC(N'
UPDATE r
SET ReferenceText = x.ReferenceText
OUTPUT inserted.Id INTO #ReferencesUpdated (Id)
FROM [dbo].[NoteReferences] AS r
JOIN #Resolved AS x ON x.NoteBlockId = r.NoteBlockId AND x.NormalizedReference = r.NormalizedReference AND x.Targets > 0
JOIN [dbo].[NoteBlocks] AS b ON b.Id = r.NoteBlockId AND CAST(b.RowVersion AS binary(8)) = x.RowVersion
WHERE r.ReferenceText <> x.ReferenceText COLLATE Latin1_General_100_BIN2;');

EXEC(N'
INSERT INTO [dbo].[NoteReferences] (NoteBlockId, ReferenceType, ReferenceNumber, ReferenceText, NormalizedReference)
OUTPUT inserted.Id INTO #ReferencesInserted (Id)
SELECT x.NoteBlockId, x.ReferenceType, x.ReferenceNumber, x.ReferenceText, x.NormalizedReference
FROM #Resolved AS x
JOIN [dbo].[NoteBlocks] AS b ON b.Id = x.NoteBlockId AND CAST(b.RowVersion AS binary(8)) = x.RowVersion
WHERE x.Targets > 0
    AND EXISTS (SELECT 1 FROM #Wanted AS w JOIN [dbo].[Notes] AS tn ON tn.Id = w.TargetNoteId
        WHERE w.NoteBlockId = x.NoteBlockId AND w.NormalizedReference = x.NormalizedReference)
    AND NOT EXISTS (SELECT 1 FROM [dbo].[NoteReferences] AS r WITH (UPDLOCK, HOLDLOCK)
        WHERE r.NoteBlockId = x.NoteBlockId AND r.NormalizedReference = x.NormalizedReference);');

EXEC(N'
INSERT INTO [dbo].[NoteReferenceTargets] (NoteReferenceId, TargetNoteId)
OUTPUT inserted.NoteReferenceId, inserted.TargetNoteId INTO #TargetsInserted (NoteReferenceId, TargetNoteId)
SELECT r.Id, w.TargetNoteId
FROM #Wanted AS w
JOIN #Resolved AS x ON x.NoteBlockId = w.NoteBlockId AND x.NormalizedReference = w.NormalizedReference
JOIN [dbo].[NoteBlocks] AS b ON b.Id = w.NoteBlockId AND CAST(b.RowVersion AS binary(8)) = x.RowVersion
JOIN [dbo].[NoteReferences] AS r ON r.NoteBlockId = w.NoteBlockId AND r.NormalizedReference = w.NormalizedReference
WHERE EXISTS (SELECT 1 FROM [dbo].[Notes] AS tn WHERE tn.Id = w.TargetNoteId)
    AND NOT EXISTS (SELECT 1 FROM [dbo].[NoteReferenceTargets] AS g WITH (UPDLOCK, HOLDLOCK)
        WHERE g.NoteReferenceId = r.Id AND g.TargetNoteId = w.TargetNoteId);');

EXEC sp_executesql N'SELECT @ReferencesStored = COUNT(*) FROM [dbo].[NoteReferences]; SELECT @TargetsStored = COUNT(*) FROM [dbo].[NoteReferenceTargets];',
    N'@ReferencesStored int OUTPUT, @TargetsStored int OUTPUT', @ReferencesStored = @ReferencesStored OUTPUT, @TargetsStored = @TargetsStored OUTPUT;

INSERT INTO @TargetsMoved (NoteReferenceId, TargetNoteId) SELECT NoteReferenceId, TargetNoteId FROM #TargetsMoved;
INSERT INTO @TargetsRemoved (NoteReferenceId, TargetNoteId) SELECT NoteReferenceId, TargetNoteId FROM #TargetsRemoved;
INSERT INTO @ReferencesRemoved (Id) SELECT Id FROM #ReferencesRemoved;
INSERT INTO @ReferencesUpdated (Id) SELECT Id FROM #ReferencesUpdated;
INSERT INTO @ReferencesInserted (Id) SELECT Id FROM #ReferencesInserted;
INSERT INTO @TargetsInserted (NoteReferenceId, TargetNoteId) SELECT NoteReferenceId, TargetNoteId FROM #TargetsInserted;

IF @Save = 1
    COMMIT TRANSACTION;
ELSE
    ROLLBACK TRANSACTION;

-- The summary. ParagraphsRead leaves out those that cannot have references; ParagraphReferences counts each reference
-- once per paragraph (ReferencesFound counts every place) and NotesOpened each note of each of them. With @Save = 0 the
-- rows are those that would change; TargetsMoved are the rows of 004 that moved to the new table.
SELECT
    (SELECT COUNT(*) FROM dbo.NoteBlocks) AS ParagraphsInDatabase,
    (SELECT COUNT(*) FROM #Texts WHERE NoteBlockId IS NOT NULL) AS ParagraphsRead,
    (SELECT COUNT(*) FROM #Texts WHERE NoteBlockId IS NULL) AS TitlesRead,
    (SELECT COUNT(*) FROM #Found AS f JOIN #Texts AS t ON t.TextId = f.TextId WHERE t.NoteBlockId IS NOT NULL) AS ReferencesFound,
    (SELECT COUNT(*) FROM #Resolved) AS ParagraphReferences,
    (SELECT COUNT(*) FROM #Resolved WHERE [Status] = N'Linked') AS Linked,
    (SELECT COUNT(*) FROM #Resolved WHERE Targets > 1) AS WithSeveralNotes,
    (SELECT COUNT(*) FROM #Resolved WHERE [Status] = N'WithoutTarget') AS WithoutTarget,
    (SELECT COUNT(*) FROM #Resolved WHERE [Status] = N'OwnNote') AS OwnNote,
    (SELECT COUNT(*) FROM #Wanted) AS NotesOpened,
    (SELECT COUNT(*) FROM @TargetsMoved) AS TargetsMoved,
    (SELECT COUNT(*) FROM @ReferencesInserted) AS ReferencesInserted,
    (SELECT COUNT(*) FROM @ReferencesUpdated) AS ReferencesUpdated,
    (SELECT COUNT(*) FROM @ReferencesRemoved) AS ReferencesRemoved,
    (SELECT COUNT(*) FROM @TargetsInserted) AS TargetsInserted,
    (SELECT COUNT(*) FROM @TargetsRemoved) AS TargetsRemoved,
    @ReferencesStored AS ReferencesStored,
    @TargetsStored AS TargetsStored,
    @Save AS Saved;

-- The references no note of the board has in its title (as the paragraph's owner sees the board): no link.
SELECT r.NoteId, n.Title AS NoteTitle, n.NoteType, r.NormalizedReference, MIN(r.ReferenceText) AS ReferenceText,
    COUNT(*) AS Paragraphs, SUM(r.Occurrences) AS Occurrences
FROM #Resolved AS r
JOIN dbo.Notes AS n ON n.Id = r.NoteId
WHERE r.[Status] = N'WithoutTarget'
GROUP BY r.NoteId, n.Title, n.NoteType, r.NormalizedReference
ORDER BY r.NoteId, r.NormalizedReference;

-- The references several notes of the board have in their title: the link opens all of them; each note is listed.
SELECT r.NoteId, n.Title AS NoteTitle, r.NormalizedReference, MIN(r.ReferenceText) AS ReferenceText,
    COUNT(DISTINCT r.NoteBlockId) AS Paragraphs, w.TargetNoteId, tn.Title AS TargetTitle, tn.NoteType AS TargetType
FROM #Resolved AS r
JOIN dbo.Notes AS n ON n.Id = r.NoteId
JOIN #Wanted AS w ON w.NoteBlockId = r.NoteBlockId AND w.NormalizedReference = r.NormalizedReference
JOIN dbo.Notes AS tn ON tn.Id = w.TargetNoteId
WHERE r.Targets > 1
GROUP BY r.NoteId, n.Title, r.NormalizedReference, w.TargetNoteId, tn.Title, tn.NoteType
ORDER BY r.NoteId, r.NormalizedReference, w.TargetNoteId;

-- The CRs journal: every note titled CRs, whether its paragraphs were read, and what each of its references became,
-- with each note it opens.
SELECT n.Id AS NoteId, n.Title, n.NoteType, n.ArchivedAtUtc,
    (SELECT COUNT(*) FROM dbo.NoteBlocks AS b WHERE b.NoteId = n.Id) AS Paragraphs,
    (SELECT COUNT(*) FROM #Texts AS t WHERE t.NoteId = n.Id AND t.NoteBlockId IS NOT NULL) AS ParagraphsRead,
    (SELECT COUNT(DISTINCT r.NormalizedReference) FROM #Resolved AS r WHERE r.NoteId = n.Id) AS [References],
    (SELECT COUNT(DISTINCT r.NormalizedReference) FROM #Resolved AS r WHERE r.NoteId = n.Id AND r.[Status] = N'Linked') AS Linked,
    (SELECT COUNT(DISTINCT r.NormalizedReference) FROM #Resolved AS r WHERE r.NoteId = n.Id AND r.Targets > 1) AS WithSeveralNotes,
    (SELECT COUNT(DISTINCT r.NormalizedReference) FROM #Resolved AS r WHERE r.NoteId = n.Id AND r.[Status] = N'WithoutTarget') AS WithoutTarget,
    (SELECT COUNT(DISTINCT r.NormalizedReference) FROM #Resolved AS r WHERE r.NoteId = n.Id AND r.[Status] = N'OwnNote') AS OwnNote
FROM dbo.Notes AS n
WHERE LTRIM(RTRIM(n.Title)) = N'CRs'
ORDER BY n.Id;

SELECT n.Id AS NoteId, r.NormalizedReference, MIN(r.ReferenceText) AS ReferenceText, r.[Status], w.TargetNoteId,
    tn.Title AS TargetTitle, tn.NoteType AS TargetType, COUNT(*) AS Paragraphs, SUM(r.Occurrences) AS Occurrences
FROM dbo.Notes AS n
JOIN #Resolved AS r ON r.NoteId = n.Id
LEFT JOIN #Wanted AS w ON w.NoteBlockId = r.NoteBlockId AND w.NormalizedReference = r.NormalizedReference
LEFT JOIN dbo.Notes AS tn ON tn.Id = w.TargetNoteId
WHERE LTRIM(RTRIM(n.Title)) = N'CRs'
GROUP BY n.Id, r.NormalizedReference, r.[Status], w.TargetNoteId, tn.Title, tn.NoteType
ORDER BY n.Id, r.NormalizedReference, w.TargetNoteId;

DROP TABLE #WordRanges, #Texts, #Found, #Titles, #Mentions, #Candidates, #Wanted, #Resolved, #TargetsMoved, #TargetsRemoved,
    #ReferencesRemoved, #ReferencesUpdated, #ReferencesInserted, #TargetsInserted;
GO
