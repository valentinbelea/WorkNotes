# Baza de date

Regulile obligatorii (Database First, fără migrări, scripturi versionate, aplicare numai la cerere explicită) sunt în [AGENTS.md](../AGENTS.md#ef-core-și-schema-sql-database-first). Scripturile și ordinea lor sunt în [Scripts/README.md](../Scripts/README.md), versiunile în [VERSIONING.md](VERSIONING.md), entitățile de domeniu în [DOMAIN-MODEL.md](DOMAIN-MODEL.md).

## SQL Server și baza

- Motor: SQL Server. Configurația locală de dezvoltare: instanța `localhost\MSSQLSERVER02`, Windows Authentication (vezi [README.md](../README.md#configurarea-sql-server)).
- Numele bazei este `WorkNotes.db`: apare în connection string-ul local și în `USE [WorkNotes.db];` de la începutul fiecărui script. TODO: Necesită clarificare — numele bazelor din mediile Test și Production și adaptarea instrucțiunii `USE` pentru ele.
- Toate tabelele sunt în schema `dbo`.

## Strategia EF Core

- Database First: schema este scrisă în scripturi SQL; clasele EF sunt generate din baza existentă prin reverse engineering. Nu există migrări EF, snapshot-uri sau tabele de istoric, iar aplicația nu creează schema la pornire.
- `WorkNotesDbContext` și entitățile `ContextMember`, `DatabaseVersion`, `Note`, `NoteBlock`, `WorkContext` sunt generate prin scaffolding.
- `AccountsDbContext` (`IdentityUserContext<ApplicationUser>`) și `ApplicationUser` sunt scrise manual și mapează `Users`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, păstrând integrarea standard Identity; mapările se actualizează manual după modificarea SQL și nu se regenerează prin scaffolding.
- Cheile externe către `Users` există numai în SQL: `Users` aparține `AccountsDbContext`, iar scaffolding-ul contextului principal le omite (mesaj informativ).

Regenerarea după modificarea schemei, din folderul `Solution`:

```powershell
dotnet tool restore
dotnet ef dbcontext scaffold 'Name=ConnectionStrings:WorkNotes' Microsoft.EntityFrameworkCore.SqlServer --project WorkNotes.DataAccess --startup-project WorkNotes.Web --context WorkNotesDbContext --context-dir Context --output-dir Entities --namespace WorkNotes.DataAccess.Entities --context-namespace WorkNotes.DataAccess.Context --table dbo.DatabaseVersion --table dbo.WorkContexts --table dbo.ContextMembers --table dbo.Notes --table dbo.NoteBlocks --no-onconfiguring --force
dotnet build WorkNotes.sln
dotnet test WorkNotes.sln --no-build --no-restore
```

- Comanda folosește Web pentru pornire și citirea configurației, dar generează fișierele numai în DataAccess. `--no-onconfiguring` păstrează conexiunea în configurație, fără să o scrie în clasele generate; `Program.cs` transmite connection string-ul extensiei `AddDataAccess`, care înregistrează DbContext-urile și providerul SQL Server.
- `--force` suprascrie `Context/WorkNotesDbContext.cs` și `Entities/DatabaseVersion.cs`, `WorkContext.cs`, `ContextMember.cs`, `Note.cs`, `NoteBlock.cs` din DataAccess. Extensiile scrise manual se pun în fișiere partial separate (contextul are `OnModelCreatingPartial`). Pentru tabele noi extindeți lista `--table`, astfel încât regenerarea să includă toate entitățile necesare.
- Scaffolding-ul scrie fișierele cu CRLF (și BOM); repository-ul folosește LF.
- Scaffolding-ul are nevoie de o bază la care s-au aplicat toate scripturile; comanda nu modifică baza.
- Documentație externă: [EF Core SQL Server](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/), [comenzile EF Core, inclusiv dbcontext scaffold](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).

## Tabele

### DatabaseVersion

| Coloană | Tip | Note |
| --- | --- | --- |
| `Version` | nvarchar(50) NOT NULL | cheie primară `PK_DatabaseVersion`; eticheta versiunii, de exemplu `v.0.02` |

Un rând pentru fiecare versiune aplicată; nu există dată de instalare. Semnificația și regula versiunii curente: [VERSIONING.md](VERSIONING.md#tabela-databaseversion).

### Users și tabelele Identity

`Users` (cheie `PK_Users` pe `Id` nvarchar(128)) conține coloanele standard Identity — `UserName`, `NormalizedUserName`, `Email`, `NormalizedEmail` (nvarchar(256)), `EmailConfirmed`, `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `PhoneNumber`, `PhoneNumberConfirmed`, `TwoFactorEnabled`, `LockoutEnd` (datetimeoffset), `LockoutEnabled`, `AccessFailedCount` — plus `FirstName` și `LastName`, nvarchar(100) NOT NULL. Indexuri unice filtrate: `EmailIndex` pe `NormalizedEmail` și `UserNameIndex` pe `NormalizedUserName` (`WHERE … IS NOT NULL`).

`AspNetUserClaims` (`Id` identity, `UserId`, `ClaimType`, `ClaimValue`), `AspNetUserLogins` (cheie `LoginProvider` + `ProviderKey`) și `AspNetUserTokens` (cheie `UserId` + `LoginProvider` + `Name`) au chei externe către `Users` cu ștergere în cascadă și indexuri pe `UserId` (claims, logins). Nu există tabele de roluri.

### WorkContexts

| Coloană | Tip | Note |
| --- | --- | --- |
| `Id` | int IDENTITY | `PK_WorkContexts` |
| `Name` | nvarchar(100) NOT NULL | unic global prin `UX_WorkContexts_Name` |
| `Description` | nvarchar(1000) NULL | opțională |

Tabela nu are coloane de audit. Numele nu este `Contexts`: entitatea generată s-ar numi `Context`, în conflict cu namespace-ul `WorkNotes.DataAccess.Context`.

### ContextMembers

| Coloană | Tip | Note |
| --- | --- | --- |
| `ContextId` | int NOT NULL | cheie externă către `WorkContexts`, cascadă |
| `UserId` | nvarchar(128) NOT NULL | cheie externă către `Users`, cascadă |
| `Role` | nvarchar(20) NOT NULL | `Owner` / `Member` (`CK_ContextMembers_Role`, constantele `ContextRoles`) |
| `AddedAtUtc` | datetime2(0) NOT NULL | implicit `SYSUTCDATETIME()` |

Cheia primară este `ContextId` + `UserId`; indexul `IX_ContextMembers_UserId` servește filtrul de apartenență.

### Notes

| Coloană | Tip | Note |
| --- | --- | --- |
| `Id` | int IDENTITY | `PK_Notes` |
| `ContextId` | int NOT NULL | cheie externă către `WorkContexts`, fără cascadă |
| `OwnerUserId` | nvarchar(128) NOT NULL | proprietarul; cheie externă către `Users` |
| `NoteType` | nvarchar(20) NOT NULL | `Journal` / `Article` (`CK_Notes_NoteType`) |
| `Title` | nvarchar(200) NULL | opțional |
| `JournalDate` | date NULL | obligatorie pentru jurnal (`CK_Notes_JournalDate`) |
| `Visibility` | nvarchar(20) NOT NULL | `Private` (implicit) / `Context` (`CK_Notes_Visibility`) |
| `CreatedAtUtc`, `CreatedByUserId` | datetime2(0), nvarchar(128) | auditul creării |
| `ModifiedAtUtc`, `ModifiedByUserId` | datetime2(0), nvarchar(128) | auditul ultimei modificări; la inserare egal cu crearea |
| `ArchivedAtUtc` | datetime2(0) NULL | arhivare; coloană pregătită, fără interfață |
| `RowVersion` | rowversion | concurența documentului |
| `[Order]` | int NOT NULL, implicit 0 | locul notei în luna ei pe tablă (adăugată de `version_0.02/001_AddNoteOrder.sql`) |

`ORDER` este cuvânt rezervat: scripturile scriu mereu `[Order]`, iar EF Core delimitează singur numele în SQL-ul generat (`SET [n].[Order] = …`), deci proprietatea `Note.Order` nu are nevoie de configurare suplimentară. La adăugarea coloanei, notele existente au fost numerotate pe context în ordinea de atunci a tablei (jurnalele, apoi articolele, fiecare de la ultima modificare), așa că fiecare lună și-a păstrat aranjarea.

### NoteBlocks

| Coloană | Tip | Note |
| --- | --- | --- |
| `Id` | uniqueidentifier | `PK_NoteBlocks`; GUID stabil, generat de editor (fără valoare implicită) |
| `NoteId` | int NOT NULL | cheie externă către `Notes`, cascadă |
| `Position` | int NOT NULL | ordinea în document, `CK_NoteBlocks_Position` (`>= 0`) |
| `Content` | nvarchar(max) NOT NULL | textul paragrafului |
| `ActivityDate` | date NULL | pregătită, fără interfață |
| `IsImportant` | bit NOT NULL, implicit 0 | pregătită, fără interfață |
| `CreatedAtUtc`, `CreatedByUserId`, `ModifiedAtUtc`, `ModifiedByUserId` | datetime2(0), nvarchar(128) | auditul paragrafului; utilizatorii au chei externe către `Users` |
| `RowVersion` | rowversion | configurat în EF ca token de concurență |

Textul notei există numai în `NoteBlocks`; `Notes` nu păstrează o copie. Regulile paragrafelor sunt în [DOMAIN-MODEL.md](DOMAIN-MODEL.md#paragraf-noteblock).

## Relații

```text
Users 1──* ContextMembers *──1 WorkContexts 1──* Notes 1──* NoteBlocks
Users 1──* Notes       (OwnerUserId, CreatedByUserId, ModifiedByUserId)
Users 1──* NoteBlocks  (CreatedByUserId, ModifiedByUserId)
Users 1──* AspNetUserClaims / AspNetUserLogins / AspNetUserTokens
DatabaseVersion        (fără relații)
```

Relațiile se fac prin chei externe explicite; modelul planificat păstrează această regulă (nicio tabelă generică `EntityType + EntityId`).

## Chei, constrângeri și indecși

| Tabelă | Cheie primară | Indexuri |
| --- | --- | --- |
| `DatabaseVersion` | `Version` | — |
| `Users` | `Id` | `EmailIndex` (unic, filtrat), `UserNameIndex` (unic, filtrat) |
| `AspNetUserClaims` | `Id` | `IX_AspNetUserClaims_UserId` |
| `AspNetUserLogins` | `LoginProvider`, `ProviderKey` | `IX_AspNetUserLogins_UserId` |
| `AspNetUserTokens` | `UserId`, `LoginProvider`, `Name` | — |
| `WorkContexts` | `Id` | `UX_WorkContexts_Name` (unic) |
| `ContextMembers` | `ContextId`, `UserId` | `IX_ContextMembers_UserId` |
| `Notes` | `Id` | `IX_Notes_ContextId_CreatedAtUtc` (`ContextId`, `CreatedAtUtc DESC`), `IX_Notes_ContextId_Order` (`ContextId`, `[Order]`) |
| `NoteBlocks` | `Id` | `IX_NoteBlocks_NoteId_Position` |

Indexul unic `UX_Notes_DailyJournal` (un jurnal pe proprietar, context și zi), creat de `006_CreateNotes.sql`, a fost eliminat de `007_AllowSeveralJournalsPerDay.sql`: sunt permise mai multe jurnale pe zi.

## Convenții

- Tabelele au nume PascalCase la plural (`WorkContexts`, `ContextMembers`, `Notes`, `NoteBlocks`), cu excepția `DatabaseVersion`, a tabelei Identity redenumite `Users` și a tabelelor `AspNetUser*`.
- Coloanele sunt PascalCase; momentele sunt UTC, cu sufixul `AtUtc` și tipul `datetime2(0)` (precizie la secundă); utilizatorii sunt referiți prin `…UserId` nvarchar(128); textele de cod (`NoteType`, `Visibility`, `Role`) sunt nvarchar(20) cu constrângeri `CHECK` aliniate la constantele Business.
- Numele constrângerilor: `PK_{Tabelă}`, `FK_{Tabelă}_{TabelăReferită}_{Coloană}`, `UX_{Tabelă}_{Coloane}` (index unic), `IX_{Tabelă}_{Coloane}`, `CK_{Tabelă}_{Regulă}`, `DF_{Tabelă}_{Coloană}`.

## Coloane de audit

- `Notes` și `NoteBlocks`: `CreatedAtUtc`, `CreatedByUserId`, `ModifiedAtUtc`, `ModifiedByUserId`. `ContextMembers`: `AddedAtUtc`. `WorkContexts` nu are audit.
- Aplicația scrie momentele trunchiate la secundă (`NoteService`), ca să se citească la fel după reîncărcare.
- `Notes.ModifiedAtUtc` este NOT NULL și primește la inserare aceeași valoare ca `CreatedAtUtc`; `NoteRepository` raportează o notă cu `ModifiedAtUtc` egal cu crearea ca nemodificată (`ModifiedAtUtc = null` în `NoteSummary`). Tabla folosește ultima modificare, `ISNULL(ModifiedAtUtc, CreatedAtUtc)`.
- Mutarea unui paragraf (schimbarea `Position`) și schimbul ordinii pe tablă nu modifică auditul; numai schimbarea textului sau a titlului îl actualizează.

## Reguli de ștergere

| Operație | Efect |
| --- | --- |
| Ștergerea unui context | Refuzată dacă are note: `FK_Notes_WorkContexts_ContextId` nu are cascadă, iar eroarea 547 devine `WorkContextDeleteStatus.InUse` („Contextul are note și nu poate fi șters.”). Fără note, membrii se șterg în cascadă. |
| Ștergerea unei note | Ștergere fizică (`ExecuteDeleteAsync`), numai de proprietar; paragrafele se șterg în cascadă. Nu există ștergere logică; `ArchivedAtUtc` nu are interfață. |
| Eliminarea unui membru | Șterge numai apartenența cu rolul `Member`; proprietarul nu poate fi eliminat. |
| Ștergerea unui utilizator | Nu există în aplicație. În SQL, apartenențele și tabelele `AspNetUser*` se șterg în cascadă, dar cheile externe din `Notes` și `NoteBlocks` către `Users` nu au cascadă și blochează ștergerea unui utilizator care are note sau paragrafe. |

## Concurență

- `Notes.RowVersion` protejează documentul: versiunea citită circulă ca text Base64 (8 octeți) în `NoteSummary.Version` și `NoteDocument.Version`. La salvare, repository-ul setează versiunea așteptată ca valoare originală și actualizează mereu rândul notei, astfel încât o salvare dintr-un editor învechit primește `Conflict`, fără să suprascrie nimic, inclusiv la două salvări în aceeași secundă.
- Redenumirea și schimbul ordinii schimbă `RowVersion`; schimbul este condiționat de versiunile ambelor note, într-un singur `UPDATE`. Editorul din aceeași pagină primește noile versiuni; un editor deschis în altă fereastră primește conflict la următoarea salvare.
- `NoteBlocks.RowVersion` este configurat ca token de concurență; verificarea documentului se face prin versiunea notei.
- O notă nouă primește `MIN([Order]) - 1` din contextul ei, citit cu `UPDLOCK, HOLDLOCK` în aceeași tranzacție, deci notele create simultan primesc valori diferite.
- Indexul unic pe `WorkContexts.Name`, cheia primară din `ContextMembers` și indexul unic pe `NormalizedEmail` protejează salvările concurente; scripturile de versiune inserează cu `UPDLOCK, HOLDLOCK`.

## Scripturile și actualizarea bazei

Schema se modifică numai prin scripturi noi, idempotente, în folderul versiunii curente, aplicate la cerere explicită, urmate de scaffolding și de actualizarea acestui document. Lista scripturilor, ordinea, comenzile și verificările după aplicare: [Scripts/README.md](../Scripts/README.md).

## În dezvoltare și planificat

- PR #4 (branch `main_task_02`, neintegrat în `main`) adaugă `Scripts/version_0.02/002_CreateNoteReferences.sql` și tabela `NoteReferences` (`SourceNoteId` cu cascadă, `TargetNoteId` fără cascadă, `DisplayText` numai cifre, `CreatedAtUtc`, index unic pe sursă + destinație + număr, index pe destinație) pentru referințele interne dintre note. Documentul se actualizează la integrarea PR-ului.
- Tabelele planificate din planul inițial (`WorkReferences`, `NoteWorkReferences`, `NoteBlockWorkReferences`, `NoteLinks`, `NotePlatforms` și ulterior `NoteClients`, `NoteProjects`, `NoteBranches`, `NoteEvents`, `NoteReleases`, `NotePublishes`) sunt descrise în [DOMAIN-MODEL.md](DOMAIN-MODEL.md#entități-planificate).
