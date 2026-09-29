USE [WorkNotes.db];
GO
-- Fills dbo.NoteReferences from the text of the existing notes; the text itself is not changed.
-- In every paragraph (NoteBlocks.Content) of a note, each number of 3-18 digits written as a whole word (no letter or
-- digit right before or after it) that appears whole in the title (Notes.Title) of other notes of the same context
-- gives a row: the note of the paragraph, the note the number names, the number. When several notes have the number in
-- their title, the only article among them is taken (a CR or a bug is documented in its article); with several
-- articles, or journals only, nothing is inserted and the number is listed at the end with its candidates.
-- As in the application: archived notes are left out, the owner of the source note is a member of its context, a
-- target is another note of that context the owner may see (their own, or shared with the context), and numbers inside
-- a stored reference ([[note:{id}|{number}]]) or right next to one do not count.
-- Only missing rows are inserted, so running the script again adds only what is new. With @Save = 0 nothing is saved
-- and the first list shows what would be inserted.
-- The application rebuilds the rows of a note from the references in its text whenever that text is saved: the rows
-- inserted here for a note are removed at its next save (from the editor or by create-note-references).
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Save bit = 1;
DECLARE @Inserted TABLE (SourceNoteId int NOT NULL, TargetNoteId int NOT NULL, DisplayText nvarchar(20) NOT NULL);

IF OBJECT_ID(N'tempdb..#TitleNumbers') IS NOT NULL DROP TABLE #TitleNumbers;
IF OBJECT_ID(N'tempdb..#Pairs') IS NOT NULL DROP TABLE #Pairs;
IF OBJECT_ID(N'tempdb..#Mentions') IS NOT NULL DROP TABLE #Mentions;
IF OBJECT_ID(N'tempdb..#Resolved') IS NOT NULL DROP TABLE #Resolved;

-- 1. The numbers in the titles: every run of 3-18 ASCII digits with no digit before or after it.
WITH Digits AS (SELECT d FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS v (d)),
Positions AS (SELECT a.d * 100 + b.d * 10 + c.d + 1 AS p FROM Digits AS a CROSS JOIN Digits AS b CROSS JOIN Digits AS c)
SELECT DISTINCT t.Id AS TargetNoteId, t.ContextId, t.OwnerUserId, t.Visibility, t.NoteType,
    CAST(SUBSTRING(t.Title, p.p, r.Length) AS nvarchar(20)) AS Number
INTO #TitleNumbers
FROM dbo.Notes AS t
JOIN Positions AS p ON p.p <= LEN(t.Title)
CROSS APPLY (SELECT PATINDEX(N'%[^0-9]%', SUBSTRING(t.Title, p.p, 19) COLLATE Latin1_General_100_BIN2 + N'x') - 1 AS Length) AS r
WHERE t.ArchivedAtUtc IS NULL
    AND t.Title IS NOT NULL
    AND SUBSTRING(t.Title, p.p, 1) COLLATE Latin1_General_100_BIN2 LIKE N'[0-9]'
    AND (p.p = 1 OR SUBSTRING(t.Title, p.p - 1, 1) COLLATE Latin1_General_100_BIN2 NOT LIKE N'[0-9]')
    AND r.Length BETWEEN 3 AND 18;

-- 2. The paragraphs of the notes that can hold references, each with the numbers of its context's titles it contains.
SELECT b.Id AS BlockId, s.Id AS SourceNoteId, n.Number
INTO #Pairs
FROM dbo.NoteBlocks AS b
JOIN dbo.Notes AS s ON s.Id = b.NoteId
JOIN (SELECT DISTINCT ContextId, Number FROM #TitleNumbers) AS n ON n.ContextId = s.ContextId
WHERE s.ArchivedAtUtc IS NULL
    AND EXISTS (SELECT 1 FROM dbo.ContextMembers AS m WHERE m.ContextId = s.ContextId AND m.UserId = s.OwnerUserId)
    AND CHARINDEX(n.Number COLLATE Latin1_General_100_BIN2, b.Content COLLATE Latin1_General_100_BIN2) > 0;

-- 3. The numbers each note mentions: an occurrence that is a whole word, outside the stored references.
WITH Occurrences AS
(
    SELECT c.BlockId, c.SourceNoteId, c.Number,
        CAST(CHARINDEX(c.Number COLLATE Latin1_General_100_BIN2, b.Content COLLATE Latin1_General_100_BIN2) AS bigint) AS Position
    FROM #Pairs AS c
    JOIN dbo.NoteBlocks AS b ON b.Id = c.BlockId
    UNION ALL
    SELECT o.BlockId, o.SourceNoteId, o.Number,
        CAST(CHARINDEX(o.Number COLLATE Latin1_General_100_BIN2, b.Content COLLATE Latin1_General_100_BIN2, o.Position + 1) AS bigint)
    FROM Occurrences AS o
    JOIN dbo.NoteBlocks AS b ON b.Id = o.BlockId
    WHERE o.Position > 0
)
SELECT DISTINCT o.SourceNoteId, o.Number
INTO #Mentions
FROM Occurrences AS o
JOIN dbo.NoteBlocks AS b ON b.Id = o.BlockId
CROSS APPLY (SELECT
    CASE WHEN o.Position > 1 THEN SUBSTRING(b.Content, o.Position - 1, 1) ELSE N'' END AS Before,
    SUBSTRING(b.Content, o.Position + LEN(o.Number), 1) AS After) AS e
WHERE o.Position > 0
    -- A letter, a digit or a combining mark right before or after makes the number part of a longer word.
    AND NOT (e.Before COLLATE Latin1_General_100_BIN2 LIKE N'[0-9]'
        OR UPPER(e.Before) COLLATE Latin1_General_100_BIN2 <> LOWER(e.Before) COLLATE Latin1_General_100_BIN2
        OR ISNULL(UNICODE(e.Before), 0) BETWEEN 768 AND 879)
    AND NOT (e.After COLLATE Latin1_General_100_BIN2 LIKE N'[0-9]'
        OR UPPER(e.After) COLLATE Latin1_General_100_BIN2 <> LOWER(e.After) COLLATE Latin1_General_100_BIN2
        OR ISNULL(UNICODE(e.After), 0) BETWEEN 768 AND 879)
    -- The id or the number of a stored reference, or a number right next to one.
    AND NOT (o.Position > 7 AND SUBSTRING(b.Content, o.Position - 7, 7) = N'[[note:')
    AND NOT (e.Before = N'|' AND SUBSTRING(b.Content, o.Position + LEN(o.Number), 2) = N']]')
    AND NOT (o.Position > 2 AND SUBSTRING(b.Content, o.Position - 2, 2) = N']]')
    AND NOT (SUBSTRING(b.Content, o.Position + LEN(o.Number), 7) = N'[[note:')
OPTION (MAXRECURSION 0);

-- 4. The notes each mention can refer to: other notes of the context, visible to the owner, with the number in their title.
SELECT m.SourceNoteId, m.Number, t.TargetNoteId, t.NoteType,
    COUNT(*) OVER (PARTITION BY m.SourceNoteId, m.Number) AS Candidates,
    SUM(CASE WHEN t.NoteType = N'Article' THEN 1 ELSE 0 END) OVER (PARTITION BY m.SourceNoteId, m.Number) AS Articles
INTO #Resolved
FROM #Mentions AS m
JOIN dbo.Notes AS s ON s.Id = m.SourceNoteId
JOIN #TitleNumbers AS t ON t.ContextId = s.ContextId AND t.Number = m.Number AND t.TargetNoteId <> m.SourceNoteId
    AND (t.OwnerUserId = s.OwnerUserId OR t.Visibility = N'Context');

-- 5. The rows: the only candidate, or the only article among several, when the row is not there yet.
BEGIN TRANSACTION;

INSERT INTO dbo.NoteReferences (SourceNoteId, TargetNoteId, DisplayText)
OUTPUT inserted.SourceNoteId, inserted.TargetNoteId, inserted.DisplayText INTO @Inserted
SELECT r.SourceNoteId, r.TargetNoteId, r.Number
FROM #Resolved AS r
WHERE (r.Candidates = 1 OR (r.Articles = 1 AND r.NoteType = N'Article'))
    AND NOT EXISTS (SELECT 1 FROM dbo.NoteReferences AS x WITH (UPDLOCK, HOLDLOCK)
        WHERE x.SourceNoteId = r.SourceNoteId AND x.TargetNoteId = r.TargetNoteId AND x.DisplayText = r.Number);

IF @Save = 1
    COMMIT TRANSACTION;
ELSE
    ROLLBACK TRANSACTION;

-- The rows inserted (with @Save = 0, the rows that would be).
SELECT i.SourceNoteId, sn.Title AS SourceTitle, i.DisplayText AS Number, i.TargetNoteId, tn.Title AS TargetTitle,
    tn.NoteType AS TargetType
FROM @Inserted AS i
JOIN dbo.Notes AS sn ON sn.Id = i.SourceNoteId
JOIN dbo.Notes AS tn ON tn.Id = i.TargetNoteId
ORDER BY i.SourceNoteId, i.DisplayText;

-- The numbers left out: several notes have them in their title, and not exactly one of them is an article.
SELECT r.SourceNoteId, sn.Title AS SourceTitle, r.Number, r.TargetNoteId AS CandidateNoteId, tn.Title AS CandidateTitle,
    r.NoteType AS CandidateType
FROM #Resolved AS r
JOIN dbo.Notes AS sn ON sn.Id = r.SourceNoteId
JOIN dbo.Notes AS tn ON tn.Id = r.TargetNoteId
WHERE r.Candidates > 1 AND r.Articles <> 1
ORDER BY r.SourceNoteId, r.Number, r.TargetNoteId;

DROP TABLE #TitleNumbers, #Pairs, #Mentions, #Resolved;
GO
