# Scripturi SQL

Scripturile SQL versionate care creează și modifică schema bazei `WorkNotes.db` (Database First). Regulile obligatorii sunt în [AGENTS.md](../AGENTS.md#scripturi-sql-și-versiuni), schema în [docs/DATABASE.md](../docs/DATABASE.md), versiunile în [docs/VERSIONING.md](../docs/VERSIONING.md).

> Scripturile se aplică numai la cererea explicită a utilizatorului, pe baza indicată pentru sarcină. Agenții nu execută scripturi și nu modifică baze de date din proprie inițiativă.

## Structura

```text
Scripts/
├── version_0.01/
│   ├── 000_CreateDatabaseVersion.sql
│   ├── 001_InsertDatabaseVersion.sql
│   ├── 002_AddIdentityUsers.sql
│   ├── 004_CreateWorkContexts.sql
│   ├── 005_CreateContextMembers.sql
│   ├── 006_CreateNotes.sql
│   ├── 007_AllowSeveralJournalsPerDay.sql
│   └── 008_CreateNoteBlocks.sql
├── version_0.02/
│   ├── 000_UpdateDatabaseVersion.sql
│   ├── 001_AddNoteOrder.sql
│   ├── 002_CreateNoteReferences.sql      # PR #4; înlocuit de 004
│   ├── 003_InsertNoteReferences.sql      # PR #4; înlocuit de 004
│   ├── 004_ReplaceNoteReferences.sql     # PR #4; completat de 005
│   ├── 005_CreateNoteReferenceTargets.sql # PR #4
│   ├── 006_CreateWorkReferences.sql      # PR #4
│   ├── 007_InsertWorkReferences.sql      # PR #4
│   ├── 008_UpdateNoteReferencesWorkReferenceId.sql # PR #4
│   ├── 009_CreateReferenceTypes.sql      # PR #4
│   ├── 010_InsertReferenceTypes.sql      # PR #4
│   ├── 011_UpdateReferenceTypeKeys.sql   # PR #4
│   └── 012_RefreshNoteReferences.sql     # PR #4
└── version_0.03/                         # versiunea curentă
    ├── 000_UpdateDatabaseVersion.sql
    ├── 001_CreateGitConnections.sql
    └── 002_CreateGitRepositories.sql
```

## Convenții de denumire

- `NNN_Descriere.sql`: prefix de trei cifre, unic în folder, urmat de o descriere PascalCase în engleză care începe cu acțiunea (`Create`, `Insert`, `Update`, `Add`, `Allow`). Numerotarea începe de la `000` în fiecare folder de versiune.
- Primul script al unei versiuni înregistrează versiunea în `DatabaseVersion` (vezi [docs/VERSIONING.md](../docs/VERSIONING.md#folderele-și-ordinea-scripturilor)).
- Numărul `003` lipsește din `version_0.01`. Istoricul Git arată că README-ul vechi menționa `003_RemoveIdentityMigrationsHistory.sql` și `temp_002_RemoveEFMigrationsHistory.sql` (scripturi de tranziție pentru eliminarea istoricului migrărilor EF din abordarea anterioară), care nu au fost niciodată adăugate în repository și au fost scoase din documentație pe 2026-09-23. TODO: Necesită clarificare — dacă au fost aplicate pe vreo bază și dacă numărul 003 rămâne liber.
- Comentariile din scripturi sunt în engleză și explică scopul și idempotența; fișierele sunt text ASCII cu terminații LF.

## Ordinea de aplicare

Baza `WorkNotes.db` trebuie să existe. Se aplică întâi `version_0.01`, apoi `version_0.02`, apoi `version_0.03`, în ordinea numelor:

| # | Script | Efect |
| --- | --- | --- |
| 1 | `version_0.01/000_CreateDatabaseVersion.sql` | Creează `dbo.DatabaseVersion` (`Version nvarchar(50)`, `PK_DatabaseVersion`), dacă lipsește |
| 2 | `version_0.01/001_InsertDatabaseVersion.sql` | Inserează `v.0.01`, dacă lipsește |
| 3 | `version_0.01/002_AddIdentityUsers.sql` | Creează tabelele și indexurile Identity lipsă (`Users`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`), fără istoric EF; conturile și hashurile existente rămân |
| 4 | `version_0.01/004_CreateWorkContexts.sql` | Creează `dbo.WorkContexts` și indexul unic pe `Name`, dacă lipsesc |
| 5 | `version_0.01/005_CreateContextMembers.sql` | Creează `dbo.ContextMembers`, cheile externe către `WorkContexts` și `Users` și indexul pe `UserId`, dacă lipsesc |
| 6 | `version_0.01/006_CreateNotes.sql` | Creează `dbo.Notes`, constrângerile și indexurile, dacă lipsesc |
| 7 | `version_0.01/007_AllowSeveralJournalsPerDay.sql` | Elimină indexul unic `UX_Notes_DailyJournal`: sunt permise mai multe jurnale pe zi |
| 8 | `version_0.01/008_CreateNoteBlocks.sql` | Creează `dbo.NoteBlocks` (paragrafele notelor, cu audit) și indexul pe `NoteId`, `Position`, dacă lipsesc |
| 9 | `version_0.02/000_UpdateDatabaseVersion.sql` | Inserează `v.0.02`, dacă lipsește; `v.0.01` rămâne, iar footerul afișează `v.0.02` |
| 10 | `version_0.02/001_AddNoteOrder.sql` | Adaugă `dbo.Notes.[Order]`, numerotează notele existente în ordinea lor de pe tablă și creează `IX_Notes_ContextId_Order`, dacă lipsesc |
| 11 | `version_0.02/002_CreateNoteReferences.sql` | PR #4, modelul vechi: creează `dbo.NoteReferences` cu `SourceNoteId`, `TargetNoteId`, `DisplayText`, dacă lipsește |
| 12 | `version_0.02/003_InsertNoteReferences.sql` | PR #4, modelul vechi, script de date: inserează în tabela veche rândurile pentru numerele din paragrafe aflate în titlul altor note |
| 13 | `version_0.02/004_ReplaceNoteReferences.sql` | PR #4: transformă legăturile vechi `[[note:{id}\|{număr}]]` din text înapoi în numărul lor, înlocuiește tabela veche cu `dbo.NoteReferences` (paragraf → notă destinație, cu tipul, numărul, textul și forma normalizată a referinței CR/bug), apoi citește toate paragrafele și titlurile și face rândurile; afișează referințele fără destinație, pe cele ambigue și jurnalul „CRs” |
| 14 | `version_0.02/005_CreateNoteReferenceTargets.sql` | PR #4: creează `dbo.NoteReferenceTargets` (referința → fiecare notă pe care o deschide, cheia primară `NoteReferenceId` + `TargetNoteId`), mută în ea nota fiecărui rând din `004` și elimină `NoteReferences.TargetNoteId`, cu cheia și indexul ei; apoi reindexează toate paragrafele: o referință deschide toate notele contextului care o au în titlu, în afară de nota paragrafului; afișează referințele fără notă, pe cele cu mai multe note (cu fiecare notă) și jurnalul „CRs” |
| 15 | `version_0.02/006_CreateWorkReferences.sql` | PR #4: creează `dbo.WorkReferences`, catalogul referințelor (`Id`; tipul și numărul, cheia unică `UX_WorkReferences_ReferenceType_ReferenceNumber`; forma normalizată, unică), dacă lipsește |
| 16 | `version_0.02/007_InsertWorkReferences.sql` | PR #4, script de date: adaugă în catalog, o singură dată, fiecare tip și număr din `dbo.NoteReferences` care lipsește; afișează referințele adăugate |
| 17 | `version_0.02/008_UpdateNoteReferencesWorkReferenceId.sql` | PR #4: adaugă `dbo.NoteReferences.WorkReferenceId`, adaugă întâi în catalog referințele stocate care lipsesc, completează cheia pentru fiecare rând după tip și număr, apoi o face obligatorie, cu cheia externă `FK_NoteReferences_WorkReferences_WorkReferenceId` și indexul `IX_NoteReferences_WorkReferenceId` |
| 18 | `version_0.02/009_CreateReferenceTypes.sql` | PR #4: creează `dbo.ReferenceTypes`, tabela de configurare a tipurilor de referință (`Code`: 1–10 litere ASCII mari, cheia primară; `IsActive`; `CreatedAtUtc`), dacă lipsește |
| 19 | `version_0.02/010_InsertReferenceTypes.sql` | PR #4, script de date: adaugă tipurile `CR` și `BUG`, active, și orice alt tip deja stocat în `NoteReferences` sau `WorkReferences`, dacă lipsesc; un tip existent rămâne cum este; afișează tipurile |
| 20 | `version_0.02/011_UpdateReferenceTypeKeys.sql` | PR #4: adaugă cheile externe `FK_NoteReferences_ReferenceTypes_ReferenceType` și `FK_WorkReferences_ReferenceTypes_ReferenceType` (fără cascadă), apoi elimină `CK_NoteReferences_ReferenceType` și `CK_WorkReferences_ReferenceType` |
| 21 | `version_0.02/012_RefreshNoteReferences.sql` | PR #4, script de date, se poate rula oricând: citește din nou toate paragrafele și titlurile cu tipurile active, aduce la zi `NoteReferences`, `NoteReferenceTargets` și `WorkReferences` (șterge rândurile pe care regula nu le mai dă, inclusiv cele ale tipurilor dezactivate, și le adaugă pe cele lipsă) și afișează tipurile, rezumatul, referințele fără notă, cele cu mai multe note și jurnalul „CRs” |
| 22 | `version_0.03/000_UpdateDatabaseVersion.sql` | Inserează `v.0.03`, dacă lipsește; `v.0.01` și `v.0.02` rămân, iar footerul afișează `v.0.03` |
| 23 | `version_0.03/001_CreateGitConnections.sql` | Creează `dbo.GitConnections` (conturile GitHub conectate, cu tokenurile criptate de aplicație; cheia `UserId` + `Provider`, cascadă cu `Users`), dacă lipsește; afișează coloanele |
| 24 | `version_0.03/002_CreateGitRepositories.sql` | Creează `dbo.GitRepositories` (repository-urile importate de fiecare utilizator, cascadă cu `Users`) și indexul unic `UX_GitRepositories_UserId_Provider_ExternalId`, dacă lipsesc; afișează coloanele |

Comentariul din antetul `006_CreateNotes.sql` („A Journal is daily: one per owner, context and date”) descrie regula inițială, înlocuită de `007_AllowSeveralJournalsPerDay.sql`; scriptul livrat nu se modifică.

## Comenzi

În SQL Server Management Studio, conectat prin Windows Authentication la instanță, executați scripturile în ordinea de mai sus. Alternativ, cu `sqlcmd`, din folderul `Solution`:

```powershell
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\000_CreateDatabaseVersion.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\001_InsertDatabaseVersion.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\002_AddIdentityUsers.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\004_CreateWorkContexts.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\005_CreateContextMembers.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\006_CreateNotes.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\007_AllowSeveralJournalsPerDay.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\008_CreateNoteBlocks.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.02\000_UpdateDatabaseVersion.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.02\001_AddNoteOrder.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.02\002_CreateNoteReferences.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.02\003_InsertNoteReferences.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.02\004_ReplaceNoteReferences.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.02\005_CreateNoteReferenceTargets.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.02\006_CreateWorkReferences.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.02\007_InsertWorkReferences.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.02\008_UpdateNoteReferencesWorkReferenceId.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.02\009_CreateReferenceTypes.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.02\010_InsertReferenceTypes.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.02\011_UpdateReferenceTypeKeys.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.02\012_RefreshNoteReferences.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.03\000_UpdateDatabaseVersion.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.03\001_CreateGitConnections.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.03\002_CreateGitRepositories.sql'
```

Pe o bază la care `002` și `003` au fost deja aplicate se rulează numai `004`–`011`; pe una la care și `004` a fost aplicat, `005`–`011`; pe una la care s-a ajuns până la `008`, `009`–`011`. `012` se rulează apoi oricând este nevoie. Scripturile `005`–`008` se aplică împreună, cu aplicația oprită, înaintea versiunii de cod care le folosește: codul PR #4 scrie `WorkReferenceId` la fiecare referință stocată, iar codul anterior nu îl scrie, deci după `008` nu mai poate salva o referință nouă (și nici nu citește catalogul). `005` afișează rezultatele sub formă de liste (rezumatul, referințele fără notă, cele cu mai multe note, cu fiecare notă, și jurnalul „CRs”), ca `004`. `004` afișează rezultatele sub formă de liste (în SSMS, în fila Results; cu `sqlcmd`, în consolă): rezumatul, referințele fără destinație, cele ambigue (cu notele care le au în titlu), jurnalul „CRs” și paragrafele ale căror legături vechi au devenit text. Cu `DECLARE @Save bit = 0;` în loc de `1`, `004`, `005`, `007`, `008` și `012` nu salvează nimic, nici tabelele sau coloana: listele arată ce ar face. `007` și `008` încep prin a verifica existența `dbo.WorkReferences` și se opresc cu un mesaj dacă `006` nu a fost aplicat; `010`–`012` verifică la fel scripturile de care depind.

Scripturile `009`–`011` se aplică înaintea versiunii de cod care citește `dbo.ReferenceTypes`; codul care le precede (cel cu catalogul, după `008`) funcționează și cu ele, pentru că scrie numai `CR` și `BUG`. `012` nu este necesar la prima aplicare (textele au fost citite de `005` cu `CR` și `BUG`, aceleași tipuri); se rulează după fiecare schimbare a tipurilor (un tip adăugat, activat sau dezactivat), astfel încât textele deja salvate să o urmeze. Un tip se adaugă cu `INSERT INTO dbo.ReferenceTypes (Code, IsActive) VALUES (N'TASK', 1);`, se dezactivează cu `UPDATE dbo.ReferenceTypes SET IsActive = 0 WHERE Code = N'TASK';` și nu se șterge cât timp sunt stocate referințe cu el; aplicația vede schimbarea în cel mult 5 minute.

`-E` folosește Windows Authentication, `-C` acceptă certificatul serverului (ca `TrustServerCertificate=True` din configurația locală), iar `-b` oprește execuția la prima eroare. Identitatea care rulează scripturile are nevoie de drepturi de modificare a schemei.

## Tabela DatabaseVersion

`dbo.DatabaseVersion` păstrează câte un rând pentru fiecare versiune aplicată (`v.0.01`, `v.0.02`, `v.0.03`); aplicația afișează versiunea numerică maximă. Semnificația completă: [docs/VERSIONING.md](../docs/VERSIONING.md#tabela-databaseversion).

## Idempotență

Toate scripturile pot fi executate repetat fără să schimbe rezultatul și păstrează tabelele și datele existente, cu excepția celor înlocuite:

- tabelele se creează numai dacă lipsesc: `IF OBJECT_ID(N'[dbo].[Tabelă]', N'U') IS NULL`;
- indexurile se creează sau se elimină numai după verificarea `sys.indexes` (`IF NOT EXISTS` / `IF EXISTS`);
- coloanele noi se adaugă numai dacă lipsesc: `IF COL_LENGTH(N'dbo.Notes', N'Order') IS NULL`;
- versiunile se inserează numai dacă lipsesc, într-o tranzacție, cu verificarea `WITH (UPDLOCK, HOLDLOCK)`, astfel încât execuțiile concurente nu produc duplicate;
- scripturile folosesc `SET XACT_ABORT ON` și, cu excepția `000_CreateDatabaseVersion.sql`, o tranzacție `BEGIN TRANSACTION … COMMIT TRANSACTION`;
- instrucțiunile care trebuie compilate după apariția unei coloane sau indexurile filtrate sunt rulate prin `EXEC(N'…')`;
- fiecare script începe cu `USE [WorkNotes.db];`: numele bazei este fix (punctul deschis despre celelalte medii este în [docs/DATABASE.md](../docs/DATABASE.md#sql-server-și-baza));
- PR #4: `002_CreateNoteReferences.sql` și `003_InsertNoteReferences.sql` au fost înlocuite de `004_ReplaceNoteReferences.sql` la cererea explicită a utilizatorului (tabela `NoteReferences` ștearsă și recreată cu altă structură). Ele rămân nemodificate, pentru că au fost aplicate, dar după `004` nu se mai rulează: `002` s-ar opri la indexul vechi (coloanele lui nu mai există), fără schimbări, iar `003` citește coloane care nu mai există. După `005`, nici `004` nu se mai rulează: coloana `NoteReferences.TargetNoteId`, pe care o folosește, nu mai există, iar o nouă rulare se oprește cu o eroare, fără nicio schimbare (tranzacția este anulată). `005` începe prin a verifica structura din `004` și se oprește cu un mesaj dacă `004` nu a fost aplicat. `005` se putea rula din nou până la `008`: după `008`, rândurile pe care le inserează nu au `WorkReferenceId`, deci o nouă rulare se oprește cu o eroare, fără nicio schimbare. `006`, `007` și `008` se pot rula oricând din nou: adaugă numai ce lipsește; la fel `009`, `010` și `011`. `012` ține locul lui `005` pentru rulările de după `008`: se poate rula oricând din nou și schimbă numai ce nu mai corespunde textelor, titlurilor și tipurilor active.

Schimbările de structură folosesc scripturi `ALTER` dedicate; nu se șterg tabele sau date pentru a rezolva o incompatibilitate fără solicitare explicită.

## Scripturile livrate nu se modifică

Un script integrat în `main` nu se mai modifică: orice corecție se face printr-un script nou, cu numărul următor, în folderul versiunii curente ([docs/VERSIONING.md](../docs/VERSIONING.md#scripturile-livrate)). Un script încă neintegrat poate fi corectat, dar dacă a fost deja aplicat pe o bază locală, modificarea nu se reaplică automat, pentru că verificările de idempotență sar peste obiectele existente.

## Adăugarea unui script

1. Folderul versiunii curente (în prezent `version_0.03`), cu numărul următor liber.
2. Antet în engleză: scopul și faptul că scriptul este idempotent; `USE [WorkNotes.db];`, `SET XACT_ABORT ON`, tranzacție acolo unde atomicitatea contează.
3. Verificări de existență pentru fiecare obiect creat, modificat sau eliminat.
4. Actualizarea acestui fișier (structura, ordinea, comenzile), a [docs/DATABASE.md](../docs/DATABASE.md) și a secțiunii Database din [CHANGELOG.md](../CHANGELOG.md); pentru tabele noi, extinderea comenzii de scaffolding și regenerarea modelului EF.
5. Aplicarea pe o bază numai la cerere explicită.

## Verificarea după aplicare

```sql
-- Versiunile înregistrate: v.0.01, v.0.02 și v.0.03
SELECT [Version] FROM [dbo].[DatabaseVersion] ORDER BY [Version];

-- Tabelele aplicației
SELECT [name] FROM sys.tables ORDER BY [name];

-- Coloana adăugată în version_0.02 (rezultat nenul)
SELECT COL_LENGTH(N'dbo.Notes', N'Order') AS [OrderColumnLength];

-- Indexurile notelor: IX_Notes_ContextId_CreatedAtUtc și IX_Notes_ContextId_Order; UX_Notes_DailyJournal nu mai există
SELECT [name] FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'dbo.Notes') AND [name] IS NOT NULL;

-- PR #4, după 008: coloanele NoteReferences (Id, NoteBlockId, ReferenceType, ReferenceNumber, ReferenceText,
-- NormalizedReference, CreatedAtUtc, WorkReferenceId), fără SourceNoteId, DisplayText și TargetNoteId
SELECT [name], [is_nullable] FROM sys.columns WHERE [object_id] = OBJECT_ID(N'dbo.NoteReferences') ORDER BY [column_id];
-- PK_NoteReferences, UX_NoteReferences_NoteBlockId_NormalizedReference, IX_NoteReferences_NormalizedReference,
-- IX_NoteReferences_WorkReferenceId
SELECT [name] FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'dbo.NoteReferences') AND [name] IS NOT NULL;
-- PK_WorkReferences, UX_WorkReferences_ReferenceType_ReferenceNumber, UX_WorkReferences_NormalizedReference
SELECT [name] FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'dbo.WorkReferences') AND [name] IS NOT NULL;
-- PK_NoteReferenceTargets, IX_NoteReferenceTargets_TargetNoteId
SELECT [name] FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'dbo.NoteReferenceTargets') AND [name] IS NOT NULL;
-- Fiecare referință are cel puțin o notă (rezultat 0)
SELECT COUNT(*) AS [ReferencesWithoutNotes] FROM [dbo].[NoteReferences] AS r
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[NoteReferenceTargets] AS t WHERE t.[NoteReferenceId] = r.[Id]);
-- PR #4, după 008: fiecare rând are referința lui din catalog, cu același tip și număr (rezultat 0)
SELECT COUNT(*) AS [RowsNotMatchingTheCatalog] FROM [dbo].[NoteReferences] AS r
JOIN [dbo].[WorkReferences] AS w ON w.[Id] = r.[WorkReferenceId]
WHERE w.[ReferenceType] <> r.[ReferenceType] OR w.[ReferenceNumber] <> r.[ReferenceNumber];
-- Catalogul: fiecare tip și număr o singură dată
SELECT [Id], [NormalizedReference] FROM [dbo].[WorkReferences] ORDER BY [ReferenceType], [ReferenceNumber];
-- PR #4, după 011: tipurile de referință (CR și BUG, active, după 010)
SELECT [Code], [IsActive], [CreatedAtUtc] FROM [dbo].[ReferenceTypes] ORDER BY [Code];
-- Cheile externe ale tipului; CK_NoteReferences_ReferenceType și CK_WorkReferences_ReferenceType nu mai există
SELECT [name] FROM sys.foreign_keys WHERE [referenced_object_id] = OBJECT_ID(N'dbo.ReferenceTypes');
SELECT [name] FROM sys.check_constraints WHERE [name] IN (N'CK_NoteReferences_ReferenceType', N'CK_WorkReferences_ReferenceType');
-- Versiunea 0.03: coloanele GitConnections și cheia ei externă către Users
SELECT [name], [is_nullable] FROM sys.columns WHERE [object_id] = OBJECT_ID(N'dbo.GitConnections') ORDER BY [column_id];
SELECT [name] FROM sys.foreign_keys WHERE [parent_object_id] = OBJECT_ID(N'dbo.GitConnections');
-- Versiunea 0.03: GitRepositories, PK_GitRepositories și UX_GitRepositories_UserId_Provider_ExternalId
SELECT [name] FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'dbo.GitRepositories') AND [name] IS NOT NULL;
-- Nicio legătură veche rămasă în text
SELECT COUNT(*) AS [OldLinks] FROM [dbo].[NoteBlocks] WHERE CHARINDEX(N'[[note:', [Content]) > 0;
```

Apoi porniți aplicația și verificați că footerul afișează `v.0.03` și că tabla se încarcă fără erori.
