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
└── version_0.02/                         # versiunea curentă
    ├── 000_UpdateDatabaseVersion.sql
    └── 001_AddNoteOrder.sql
```

## Convenții de denumire

- `NNN_Descriere.sql`: prefix de trei cifre, unic în folder, urmat de o descriere PascalCase în engleză care începe cu acțiunea (`Create`, `Insert`, `Update`, `Add`, `Allow`). Numerotarea începe de la `000` în fiecare folder de versiune.
- Primul script al unei versiuni înregistrează versiunea în `DatabaseVersion` (vezi [docs/VERSIONING.md](../docs/VERSIONING.md#folderele-și-ordinea-scripturilor)).
- Numărul `003` lipsește din `version_0.01`. Istoricul Git arată că README-ul vechi menționa `003_RemoveIdentityMigrationsHistory.sql` și `temp_002_RemoveEFMigrationsHistory.sql` (scripturi de tranziție pentru eliminarea istoricului migrărilor EF din abordarea anterioară), care nu au fost niciodată adăugate în repository și au fost scoase din documentație pe 2026-09-23. TODO: Necesită clarificare — dacă au fost aplicate pe vreo bază și dacă numărul 003 rămâne liber.
- Comentariile din scripturi sunt în engleză și explică scopul și idempotența; fișierele sunt text ASCII cu terminații LF.

## Ordinea de aplicare

Baza `WorkNotes.db` trebuie să existe. Se aplică întâi `version_0.01`, apoi `version_0.02`, în ordinea numelor:

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
```

`-E` folosește Windows Authentication, `-C` acceptă certificatul serverului (ca `TrustServerCertificate=True` din configurația locală), iar `-b` oprește execuția la prima eroare. Identitatea care rulează scripturile are nevoie de drepturi de modificare a schemei.

## Tabela DatabaseVersion

`dbo.DatabaseVersion` păstrează câte un rând pentru fiecare versiune aplicată (`v.0.01`, `v.0.02`); aplicația afișează versiunea numerică maximă. Semnificația completă: [docs/VERSIONING.md](../docs/VERSIONING.md#tabela-databaseversion).

## Idempotență

Toate scripturile pot fi executate repetat fără să schimbe rezultatul și păstrează tabelele și datele existente:

- tabelele se creează numai dacă lipsesc: `IF OBJECT_ID(N'[dbo].[Tabelă]', N'U') IS NULL`;
- indexurile se creează sau se elimină numai după verificarea `sys.indexes` (`IF NOT EXISTS` / `IF EXISTS`);
- coloanele noi se adaugă numai dacă lipsesc: `IF COL_LENGTH(N'dbo.Notes', N'Order') IS NULL`;
- versiunile se inserează numai dacă lipsesc, într-o tranzacție, cu verificarea `WITH (UPDLOCK, HOLDLOCK)`, astfel încât execuțiile concurente nu produc duplicate;
- scripturile folosesc `SET XACT_ABORT ON` și, cu excepția `000_CreateDatabaseVersion.sql`, o tranzacție `BEGIN TRANSACTION … COMMIT TRANSACTION`;
- instrucțiunile care trebuie compilate după apariția unei coloane sau indexurile filtrate sunt rulate prin `EXEC(N'…')`;
- fiecare script începe cu `USE [WorkNotes.db];`: numele bazei este fix (punctul deschis despre celelalte medii este în [docs/DATABASE.md](../docs/DATABASE.md#sql-server-și-baza)).

Schimbările de structură folosesc scripturi `ALTER` dedicate; nu se șterg tabele sau date pentru a rezolva o incompatibilitate fără solicitare explicită.

## Scripturile livrate nu se modifică

Un script integrat în `main` nu se mai modifică: orice corecție se face printr-un script nou, cu numărul următor, în folderul versiunii curente ([docs/VERSIONING.md](../docs/VERSIONING.md#scripturile-livrate)). Un script încă neintegrat poate fi corectat, dar dacă a fost deja aplicat pe o bază locală, modificarea nu se reaplică automat, pentru că verificările de idempotență sar peste obiectele existente.

## Adăugarea unui script

1. Folderul versiunii curente (în prezent `version_0.02`), cu numărul următor liber.
2. Antet în engleză: scopul și faptul că scriptul este idempotent; `USE [WorkNotes.db];`, `SET XACT_ABORT ON`, tranzacție acolo unde atomicitatea contează.
3. Verificări de existență pentru fiecare obiect creat, modificat sau eliminat.
4. Actualizarea acestui fișier (structura, ordinea, comenzile), a [docs/DATABASE.md](../docs/DATABASE.md) și a secțiunii Database din [CHANGELOG.md](../CHANGELOG.md); pentru tabele noi, extinderea comenzii de scaffolding și regenerarea modelului EF.
5. Aplicarea pe o bază numai la cerere explicită.

## Verificarea după aplicare

```sql
-- Versiunile înregistrate: v.0.01 și v.0.02
SELECT [Version] FROM [dbo].[DatabaseVersion] ORDER BY [Version];

-- Tabelele aplicației
SELECT [name] FROM sys.tables ORDER BY [name];

-- Coloana adăugată în version_0.02 (rezultat nenul)
SELECT COL_LENGTH(N'dbo.Notes', N'Order') AS [OrderColumnLength];

-- Indexurile notelor: IX_Notes_ContextId_CreatedAtUtc și IX_Notes_ContextId_Order; UX_Notes_DailyJournal nu mai există
SELECT [name] FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'dbo.Notes') AND [name] IS NOT NULL;
```

Apoi porniți aplicația și verificați că footerul afișează `v.0.02` și că tabla se încarcă fără erori.
