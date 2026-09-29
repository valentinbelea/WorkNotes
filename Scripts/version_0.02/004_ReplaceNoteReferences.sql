USE [WorkNotes.db];
GO
-- Internal references, new model. A reference is a CR or a bug written in a paragraph with its number (CR 30080,
-- CR-30080, CR_30080, CR30080, bug 1234, Bug-1234...: NoteReferenceRules); it opens the note whose title names the same
-- type and number, when exactly one note of the board has it and that note is another one. dbo.NoteReferences stores
-- one row per paragraph and reference with such a note: the paragraph (NoteBlockId), the note it opens (TargetNoteId),
-- the type, the number, the text as it is first written in the paragraph and the normalized form (CR:30080).
-- The text of the paragraphs is never changed to hold a link: the editor draws the links from these rows.
--
-- The script replaces the table of 002_CreateNoteReferences.sql (one row per source note, target note and number, the
-- link being kept in the text as [[note:{id}|{number}]]); 002 and 003 stay as they were applied and are not run again
-- after this script. The texts are read first; then, in one transaction:
--   1. the links of the old form in the text become the number they showed again (the text reads the same, the
--      paragraph keeps its audit);
--   2. the old table is dropped and the new one created, with its keys and indexes;
--   3. every paragraph that can have references is read (those of notes that are not archived, whose owner still
--      belongs to the note's context), as well as every title, and the rows are made from them: missing rows are
--      added, rows no longer valid are removed, the others stay. A paragraph changed while the script runs keeps the
--      rows its save gave it.
-- Then the lists: a summary, the references without a target, the ambiguous ones (with the notes that have them in
-- their title), the CRs journal and the paragraphs whose old links became text.
-- Idempotent: run again, it changes only what no longer matches the text and the titles, and lists the state again.
-- With @Save = 0 nothing is saved, the table included: the lists show what the script would do.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Save bit = 1;

IF OBJECT_ID(N'tempdb..#WordRanges') IS NOT NULL DROP TABLE #WordRanges;
IF OBJECT_ID(N'tempdb..#Cleaned') IS NOT NULL DROP TABLE #Cleaned;
IF OBJECT_ID(N'tempdb..#Texts') IS NOT NULL DROP TABLE #Texts;
IF OBJECT_ID(N'tempdb..#Found') IS NOT NULL DROP TABLE #Found;
IF OBJECT_ID(N'tempdb..#Titles') IS NOT NULL DROP TABLE #Titles;
IF OBJECT_ID(N'tempdb..#Mentions') IS NOT NULL DROP TABLE #Mentions;
IF OBJECT_ID(N'tempdb..#Candidates') IS NOT NULL DROP TABLE #Candidates;
IF OBJECT_ID(N'tempdb..#Resolved') IS NOT NULL DROP TABLE #Resolved;
IF OBJECT_ID(N'tempdb..#Removed') IS NOT NULL DROP TABLE #Removed;
IF OBJECT_ID(N'tempdb..#Inserted') IS NOT NULL DROP TABLE #Inserted;
IF OBJECT_ID(N'tempdb..#Updated') IS NOT NULL DROP TABLE #Updated;

-- What the transaction did; table variables keep their rows when it is rolled back (@Save = 0).
DECLARE @Removed TABLE (NoteBlockId uniqueidentifier NOT NULL, TargetNoteId int NOT NULL, NormalizedReference nvarchar(30) NOT NULL);
DECLARE @Inserted TABLE (NoteBlockId uniqueidentifier NOT NULL, TargetNoteId int NOT NULL, NormalizedReference nvarchar(30) NOT NULL);
DECLARE @Updated TABLE (Id int NOT NULL);
DECLARE @TextsCleaned TABLE (NoteBlockId uniqueidentifier NOT NULL, LinksRemoved int NOT NULL);
DECLARE @Stored int;

-- 1. The characters that make a word (NoteReferenceRules: letters, digits and combining marks, \p{L} \p{N} \p{M}).
-- SQL Server has no Unicode categories: these ranges give the Latin letters, the digits and the combining marks, and
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

-- 2. The links of the old form, [[note:{id}|{number}]] (an id from 1 to 2147483647, a number of 1 to 18 digits), each
-- become the number they showed; anything else stays as it is. One link per paragraph and pass.
CREATE TABLE #Cleaned
(
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    RowVersion binary(8) NOT NULL,
    Content nvarchar(max) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Position bigint NOT NULL,
    LinksRemoved int NOT NULL
);
INSERT INTO #Cleaned (Id, RowVersion, Content, Position, LinksRemoved)
SELECT b.Id, CAST(b.RowVersion AS binary(8)), b.Content, CHARINDEX(N'[[note:', b.Content COLLATE Latin1_General_100_BIN2), 0
FROM dbo.NoteBlocks AS b
WHERE CHARINDEX(N'[[note:', b.Content COLLATE Latin1_General_100_BIN2) > 0;

WHILE EXISTS (SELECT 1 FROM #Cleaned WHERE Position > 0)
BEGIN
    UPDATE c
    SET Content = r.Content,
        Position = CHARINDEX(N'[[note:', r.Content, r.SearchFrom),
        LinksRemoved = c.LinksRemoved + m.Valid
    FROM #Cleaned AS c
    CROSS APPLY (SELECT c.Position + 7 AS IdStart) AS s
    CROSS APPLY (SELECT PATINDEX(N'%[^0-9]%', SUBSTRING(c.Content, s.IdStart, 11) + N'x') - 1 AS IdLength) AS i
    CROSS APPLY (SELECT s.IdStart + i.IdLength + 1 AS NumberStart) AS ns
    CROSS APPLY (SELECT PATINDEX(N'%[^0-9]%', SUBSTRING(c.Content, ns.NumberStart, 19) + N'x') - 1 AS NumberLength) AS n
    CROSS APPLY (SELECT CASE
            WHEN i.IdLength BETWEEN 1 AND 10 AND n.NumberLength BETWEEN 1 AND 18
                AND SUBSTRING(c.Content, s.IdStart + i.IdLength, 1) = N'|'
                AND SUBSTRING(c.Content, ns.NumberStart + n.NumberLength, 2) = N']]'
                AND CAST(CASE WHEN i.IdLength BETWEEN 1 AND 10 THEN SUBSTRING(c.Content, s.IdStart, i.IdLength) END AS bigint)
                    BETWEEN 1 AND 2147483647
            THEN 1 ELSE 0 END AS Valid) AS m
    -- A link that became its number is searched for again after that number; anything else one character further.
    CROSS APPLY (SELECT
        CASE WHEN m.Valid = 1
            THEN STUFF(c.Content, c.Position, 7 + i.IdLength + 1 + n.NumberLength + 2, SUBSTRING(c.Content, ns.NumberStart, n.NumberLength))
            ELSE c.Content END AS Content,
        c.Position + CASE WHEN m.Valid = 1 THEN n.NumberLength ELSE 1 END AS SearchFrom) AS r
    WHERE c.Position > 0;
END;

-- 3. The texts read: the paragraphs that can have references (with the old links already turned into text) and the
-- titles of the notes that are not archived, the notes a reference can open. The collation is the one of the search
-- below: case-insensitive, accent-, kana- and width-sensitive (CR and cr are the same word, a fullwidth CR is not).
CREATE TABLE #Texts
(
    TextId int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    NoteId int NOT NULL,
    NoteBlockId uniqueidentifier NULL,
    RowVersion binary(8) NULL,
    Content nvarchar(max) COLLATE Latin1_General_100_CI_AS_KS_WS NOT NULL
);
INSERT INTO #Texts (NoteId, NoteBlockId, RowVersion, Content)
SELECT b.NoteId, b.Id, CAST(b.RowVersion AS binary(8)),
    CASE WHEN c.RowVersion = CAST(b.RowVersion AS binary(8))
        THEN c.Content COLLATE Latin1_General_100_CI_AS_KS_WS
        ELSE b.Content COLLATE Latin1_General_100_CI_AS_KS_WS END
FROM dbo.NoteBlocks AS b
JOIN dbo.Notes AS n ON n.Id = b.NoteId
LEFT JOIN #Cleaned AS c ON c.Id = b.Id
WHERE n.ArchivedAtUtc IS NULL
    AND EXISTS (SELECT 1 FROM dbo.ContextMembers AS m WHERE m.ContextId = n.ContextId AND m.UserId = n.OwnerUserId);

INSERT INTO #Texts (NoteId, NoteBlockId, RowVersion, Content)
SELECT n.Id, NULL, NULL, n.Title COLLATE Latin1_General_100_CI_AS_KS_WS
FROM dbo.Notes AS n
WHERE n.ArchivedAtUtc IS NULL AND n.Title IS NOT NULL;

-- 4. The references in the texts. Every place where CR or BUG is written, in any case, is checked as the application
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

-- 5. The references each title names, and those of each paragraph: once per paragraph, with the text they are first
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

-- 6. For each reference of a paragraph, the notes of the board the paragraph's owner may see that have it in their
-- title: the owner's own notes and those shared with the context, the paragraph's own note included. Linked: exactly
-- one such note, another one. WithoutTarget: none. Ambiguous: several (no link is made). OwnNote: only the note itself.
SELECT m.NoteBlockId, m.NormalizedReference, c.Id AS CandidateNoteId
INTO #Candidates
FROM #Mentions AS m
JOIN dbo.Notes AS s ON s.Id = m.NoteId
JOIN #Titles AS tt ON tt.NormalizedReference = m.NormalizedReference
JOIN dbo.Notes AS c ON c.Id = tt.NoteId AND c.ContextId = s.ContextId AND (c.OwnerUserId = s.OwnerUserId OR c.Visibility = N'Context');

SELECT m.NoteBlockId, m.NoteId, m.RowVersion, m.NormalizedReference, m.ReferenceType, m.ReferenceNumber, m.ReferenceText,
    m.Occurrences, k.Candidates,
    CASE WHEN k.Candidates = 0 THEN N'WithoutTarget'
        WHEN k.Candidates > 1 THEN N'Ambiguous'
        WHEN k.CandidateNoteId = m.NoteId THEN N'OwnNote'
        ELSE N'Linked' END AS [Status],
    CASE WHEN k.Candidates = 1 AND k.CandidateNoteId <> m.NoteId THEN k.CandidateNoteId END AS TargetNoteId
INTO #Resolved
FROM #Mentions AS m
OUTER APPLY (SELECT COUNT(*) AS Candidates, MIN(c.CandidateNoteId) AS CandidateNoteId
    FROM #Candidates AS c
    WHERE c.NoteBlockId = m.NoteBlockId AND c.NormalizedReference = m.NormalizedReference) AS k;

CREATE TABLE #Removed (NoteBlockId uniqueidentifier NOT NULL, TargetNoteId int NOT NULL, NormalizedReference nvarchar(30) COLLATE DATABASE_DEFAULT NOT NULL);
CREATE TABLE #Inserted (NoteBlockId uniqueidentifier NOT NULL, TargetNoteId int NOT NULL, NormalizedReference nvarchar(30) COLLATE DATABASE_DEFAULT NOT NULL);
CREATE TABLE #Updated (Id int NOT NULL);

BEGIN TRANSACTION;

-- 7. The table. The old one (SourceNoteId, DisplayText) goes with its rows; the new rows are made from the text below.
IF COL_LENGTH(N'dbo.NoteReferences', N'SourceNoteId') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[NoteReferences];
END;

-- Deleting a paragraph deletes its rows (cascade); the note a row opens stays. A second cascade from Notes (through
-- TargetNoteId) is not allowed, so the application deletes the rows that open a note just before deleting the note.
IF OBJECT_ID(N'[dbo].[NoteReferences]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[NoteReferences]
    (
        [Id] int IDENTITY(1, 1) NOT NULL,
        [NoteBlockId] uniqueidentifier NOT NULL,
        [TargetNoteId] int NOT NULL,
        [ReferenceType] nvarchar(20) NOT NULL,
        [ReferenceNumber] bigint NOT NULL,
        -- As first written in the paragraph: at most 71 characters (BUG, 50 spaces, 18 digits).
        [ReferenceText] nvarchar(100) NOT NULL,
        [NormalizedReference] nvarchar(30) NOT NULL,
        [CreatedAtUtc] datetime2(0) NOT NULL CONSTRAINT [DF_NoteReferences_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_NoteReferences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NoteReferences_NoteBlocks_NoteBlockId] FOREIGN KEY ([NoteBlockId]) REFERENCES [dbo].[NoteBlocks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_NoteReferences_Notes_TargetNoteId] FOREIGN KEY ([TargetNoteId]) REFERENCES [dbo].[Notes] ([Id]),
        -- NoteReferenceTypes.
        CONSTRAINT [CK_NoteReferences_ReferenceType] CHECK ([ReferenceType] IN (N'CR', N'BUG')),
        CONSTRAINT [CK_NoteReferences_ReferenceNumber] CHECK ([ReferenceNumber] >= 0 AND [ReferenceNumber] < 1000000000000000000),
        CONSTRAINT [CK_NoteReferences_ReferenceText] CHECK (LEN([ReferenceText]) > 0),
        -- CR:30080, BUG:1234: the type and the number without leading zeros (NoteReferenceRules.Normalize).
        CONSTRAINT [CK_NoteReferences_NormalizedReference] CHECK ([NormalizedReference] = [ReferenceType] + N':' + CONVERT(nvarchar(20), [ReferenceNumber]))
    );
END;

-- EXEC compiles each statement once the new table is in place (the old one has other columns).
-- One row per paragraph and reference, however often the paragraph writes it; also the index of a paragraph's rows.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NoteReferences]') AND name = N'UX_NoteReferences_NoteBlockId_NormalizedReference')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_NoteReferences_NoteBlockId_NormalizedReference] ON [dbo].[NoteReferences] ([NoteBlockId], [NormalizedReference])');
END;

-- The rows that open a note: for deleting it and for a future list of the notes that refer to it.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NoteReferences]') AND name = N'IX_NoteReferences_TargetNoteId')
BEGIN
    EXEC(N'CREATE INDEX [IX_NoteReferences_TargetNoteId] ON [dbo].[NoteReferences] ([TargetNoteId])');
END;

-- The paragraphs that write one reference, whatever their note.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NoteReferences]') AND name = N'IX_NoteReferences_NormalizedReference')
BEGIN
    EXEC(N'CREATE INDEX [IX_NoteReferences_NormalizedReference] ON [dbo].[NoteReferences] ([NormalizedReference])');
END;

-- 8. The rows. Removed: those of paragraphs that can no longer have references (archived note, owner no longer in the
-- context), and those of the paragraphs read, unchanged since, that do not match a Linked reference. The text of a kept
-- row follows the paragraph; the missing rows are added. A paragraph changed since it was read, or added since, keeps
-- its rows.
EXEC(N'
DELETE r
OUTPUT deleted.NoteBlockId, deleted.TargetNoteId, deleted.NormalizedReference INTO #Removed (NoteBlockId, TargetNoteId, NormalizedReference)
FROM [dbo].[NoteReferences] AS r
JOIN [dbo].[NoteBlocks] AS b ON b.Id = r.NoteBlockId
JOIN [dbo].[Notes] AS n ON n.Id = b.NoteId
LEFT JOIN #Texts AS t ON t.NoteBlockId = r.NoteBlockId
WHERE (n.ArchivedAtUtc IS NOT NULL
        OR NOT EXISTS (SELECT 1 FROM [dbo].[ContextMembers] AS m WHERE m.ContextId = n.ContextId AND m.UserId = n.OwnerUserId)
        OR t.RowVersion = CAST(b.RowVersion AS binary(8)))
    AND NOT EXISTS (SELECT 1 FROM #Resolved AS x
        WHERE x.NoteBlockId = r.NoteBlockId AND x.TargetNoteId = r.TargetNoteId AND x.NormalizedReference = r.NormalizedReference);');

EXEC(N'
UPDATE r
SET ReferenceText = x.ReferenceText
OUTPUT inserted.Id INTO #Updated (Id)
FROM [dbo].[NoteReferences] AS r
JOIN #Resolved AS x ON x.NoteBlockId = r.NoteBlockId AND x.TargetNoteId = r.TargetNoteId AND x.NormalizedReference = r.NormalizedReference
JOIN [dbo].[NoteBlocks] AS b ON b.Id = r.NoteBlockId AND CAST(b.RowVersion AS binary(8)) = x.RowVersion
WHERE r.ReferenceText <> x.ReferenceText COLLATE Latin1_General_100_BIN2;');

EXEC(N'
INSERT INTO [dbo].[NoteReferences] (NoteBlockId, TargetNoteId, ReferenceType, ReferenceNumber, ReferenceText, NormalizedReference)
OUTPUT inserted.NoteBlockId, inserted.TargetNoteId, inserted.NormalizedReference INTO #Inserted (NoteBlockId, TargetNoteId, NormalizedReference)
SELECT x.NoteBlockId, x.TargetNoteId, x.ReferenceType, x.ReferenceNumber, x.ReferenceText, x.NormalizedReference
FROM #Resolved AS x
JOIN [dbo].[NoteBlocks] AS b ON b.Id = x.NoteBlockId AND CAST(b.RowVersion AS binary(8)) = x.RowVersion
WHERE x.TargetNoteId IS NOT NULL
    AND EXISTS (SELECT 1 FROM [dbo].[Notes] AS n WHERE n.Id = x.TargetNoteId)
    AND NOT EXISTS (SELECT 1 FROM [dbo].[NoteReferences] AS r WITH (UPDLOCK, HOLDLOCK)
        WHERE r.NoteBlockId = x.NoteBlockId AND r.NormalizedReference = x.NormalizedReference);');

EXEC sp_executesql N'SELECT @Stored = COUNT(*) FROM [dbo].[NoteReferences];', N'@Stored int OUTPUT', @Stored = @Stored OUTPUT;

-- 9. The old links in the text, last: the rows above were checked against the paragraphs as they were read.
UPDATE b
SET Content = c.Content
OUTPUT inserted.Id, c.LinksRemoved INTO @TextsCleaned (NoteBlockId, LinksRemoved)
FROM dbo.NoteBlocks AS b
JOIN #Cleaned AS c ON c.Id = b.Id
WHERE c.LinksRemoved > 0 AND CAST(b.RowVersion AS binary(8)) = c.RowVersion;

INSERT INTO @Removed (NoteBlockId, TargetNoteId, NormalizedReference) SELECT NoteBlockId, TargetNoteId, NormalizedReference FROM #Removed;
INSERT INTO @Inserted (NoteBlockId, TargetNoteId, NormalizedReference) SELECT NoteBlockId, TargetNoteId, NormalizedReference FROM #Inserted;
INSERT INTO @Updated (Id) SELECT Id FROM #Updated;

IF @Save = 1
    COMMIT TRANSACTION;
ELSE
    ROLLBACK TRANSACTION;

-- The summary. ParagraphsRead leaves out those that cannot have references; ParagraphReferences counts each reference
-- once per paragraph (ReferencesFound counts every place). With @Save = 0 the rows are those that would change.
SELECT
    (SELECT COUNT(*) FROM dbo.NoteBlocks) AS ParagraphsInDatabase,
    (SELECT COUNT(*) FROM #Texts WHERE NoteBlockId IS NOT NULL) AS ParagraphsRead,
    (SELECT COUNT(*) FROM #Texts WHERE NoteBlockId IS NULL) AS TitlesRead,
    (SELECT COUNT(*) FROM #Found AS f JOIN #Texts AS t ON t.TextId = f.TextId WHERE t.NoteBlockId IS NOT NULL) AS ReferencesFound,
    (SELECT COUNT(*) FROM #Resolved) AS ParagraphReferences,
    (SELECT COUNT(*) FROM #Resolved WHERE [Status] = N'Linked') AS Linked,
    (SELECT COUNT(*) FROM #Resolved WHERE [Status] = N'WithoutTarget') AS WithoutTarget,
    (SELECT COUNT(*) FROM #Resolved WHERE [Status] = N'Ambiguous') AS Ambiguous,
    (SELECT COUNT(*) FROM #Resolved WHERE [Status] = N'OwnNote') AS OwnNote,
    (SELECT COUNT(*) FROM @Inserted) AS RowsInserted,
    (SELECT COUNT(*) FROM @Updated) AS RowsUpdated,
    (SELECT COUNT(*) FROM @Removed) AS RowsRemoved,
    @Stored AS RowsStored,
    (SELECT COUNT(*) FROM @TextsCleaned) AS ParagraphsWithOldLinks,
    (SELECT ISNULL(SUM(LinksRemoved), 0) FROM @TextsCleaned) AS OldLinksTurnedIntoText,
    @Save AS Saved;

-- The references no note of the board has in its title (as the paragraph's owner sees the board): no link.
SELECT r.NoteId, n.Title AS NoteTitle, n.NoteType, r.NormalizedReference, MIN(r.ReferenceText) AS ReferenceText,
    COUNT(*) AS Paragraphs, SUM(r.Occurrences) AS Occurrences
FROM #Resolved AS r
JOIN dbo.Notes AS n ON n.Id = r.NoteId
WHERE r.[Status] = N'WithoutTarget'
GROUP BY r.NoteId, n.Title, n.NoteType, r.NormalizedReference
ORDER BY r.NoteId, r.NormalizedReference;

-- The references several notes of the board have in their title: no link; each of those notes is listed.
SELECT r.NoteId, n.Title AS NoteTitle, r.NormalizedReference, MIN(r.ReferenceText) AS ReferenceText,
    COUNT(DISTINCT r.NoteBlockId) AS Paragraphs, c.CandidateNoteId, cn.Title AS CandidateTitle, cn.NoteType AS CandidateType
FROM #Resolved AS r
JOIN dbo.Notes AS n ON n.Id = r.NoteId
JOIN #Candidates AS c ON c.NoteBlockId = r.NoteBlockId AND c.NormalizedReference = r.NormalizedReference
JOIN dbo.Notes AS cn ON cn.Id = c.CandidateNoteId
WHERE r.[Status] = N'Ambiguous'
GROUP BY r.NoteId, n.Title, r.NormalizedReference, c.CandidateNoteId, cn.Title, cn.NoteType
ORDER BY r.NoteId, r.NormalizedReference, c.CandidateNoteId;

-- The CRs journal: every note titled CRs, whether its paragraphs were read, and what each of its references became.
SELECT n.Id AS NoteId, n.Title, n.NoteType, n.ArchivedAtUtc,
    (SELECT COUNT(*) FROM dbo.NoteBlocks AS b WHERE b.NoteId = n.Id) AS Paragraphs,
    (SELECT COUNT(*) FROM #Texts AS t WHERE t.NoteId = n.Id AND t.NoteBlockId IS NOT NULL) AS ParagraphsRead,
    (SELECT COUNT(DISTINCT r.NormalizedReference) FROM #Resolved AS r WHERE r.NoteId = n.Id) AS [References],
    (SELECT COUNT(DISTINCT r.NormalizedReference) FROM #Resolved AS r WHERE r.NoteId = n.Id AND r.[Status] = N'Linked') AS Linked,
    (SELECT COUNT(DISTINCT r.NormalizedReference) FROM #Resolved AS r WHERE r.NoteId = n.Id AND r.[Status] = N'WithoutTarget') AS WithoutTarget,
    (SELECT COUNT(DISTINCT r.NormalizedReference) FROM #Resolved AS r WHERE r.NoteId = n.Id AND r.[Status] = N'Ambiguous') AS Ambiguous,
    (SELECT COUNT(DISTINCT r.NormalizedReference) FROM #Resolved AS r WHERE r.NoteId = n.Id AND r.[Status] = N'OwnNote') AS OwnNote
FROM dbo.Notes AS n
WHERE LTRIM(RTRIM(n.Title)) = N'CRs'
ORDER BY n.Id;

SELECT n.Id AS NoteId, r.NormalizedReference, MIN(r.ReferenceText) AS ReferenceText, r.[Status], r.TargetNoteId,
    tn.Title AS TargetTitle, COUNT(*) AS Paragraphs, SUM(r.Occurrences) AS Occurrences
FROM dbo.Notes AS n
JOIN #Resolved AS r ON r.NoteId = n.Id
LEFT JOIN dbo.Notes AS tn ON tn.Id = r.TargetNoteId
WHERE LTRIM(RTRIM(n.Title)) = N'CRs'
GROUP BY n.Id, r.NormalizedReference, r.[Status], r.TargetNoteId, tn.Title
ORDER BY n.Id, r.NormalizedReference;

-- The paragraphs whose old links became their numbers again.
SELECT b.NoteId, n.Title AS NoteTitle, x.NoteBlockId, x.LinksRemoved
FROM @TextsCleaned AS x
JOIN dbo.NoteBlocks AS b ON b.Id = x.NoteBlockId
JOIN dbo.Notes AS n ON n.Id = b.NoteId
ORDER BY b.NoteId, b.Position;

DROP TABLE #WordRanges, #Cleaned, #Texts, #Found, #Titles, #Mentions, #Candidates, #Resolved, #Removed, #Inserted, #Updated;
GO
