# Baza de date

Regulile obligatorii (Database First, fără migrări, scripturi versionate, aplicare numai la cerere explicită) sunt în [AGENTS.md](../AGENTS.md#ef-core-și-schema-sql-database-first). Scripturile și ordinea lor sunt în [Scripts/README.md](../Scripts/README.md), versiunile în [VERSIONING.md](VERSIONING.md), entitățile de domeniu în [DOMAIN-MODEL.md](DOMAIN-MODEL.md).

## SQL Server și baza

- Motor: SQL Server. Configurația locală de dezvoltare: instanța `localhost\MSSQLSERVER02`, Windows Authentication (vezi [README.md](../README.md#configurarea-sql-server)).
- Numele bazei este `WorkNotes.db`: apare în connection string-ul local și în `USE [WorkNotes.db];` de la începutul fiecărui script. TODO: Necesită clarificare — numele bazelor din mediile Test și Production și adaptarea instrucțiunii `USE` pentru ele.
- Toate tabelele sunt în schema `dbo`.

## Strategia EF Core

- Database First: schema este scrisă în scripturi SQL; clasele EF sunt generate din baza existentă prin reverse engineering. Nu există migrări EF, snapshot-uri sau tabele de istoric, iar aplicația nu creează schema la pornire.
- `WorkNotesDbContext` și entitățile `ContextMember`, `DatabaseVersion`, `Note`, `NoteBlock`, `WorkContext` sunt generate prin scaffolding; PR #4 adaugă `NoteReference`, `NoteReferenceTarget`, `WorkReference` și `ReferenceType`, iar versiunea 0.03 `GitConnection`, `GitRepository`, `GitReference` și `NoteBlockGitReference`.
- `AccountsDbContext` (`IdentityUserContext<ApplicationUser>`) și `ApplicationUser` sunt scrise manual și mapează `Users`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, păstrând integrarea standard Identity; mapările se actualizează manual după modificarea SQL și nu se regenerează prin scaffolding.
- Cheile externe către `Users` există numai în SQL: `Users` aparține `AccountsDbContext`, iar scaffolding-ul contextului principal le omite (mesaj informativ).

Regenerarea după modificarea schemei, din folderul `Solution`:

```powershell
dotnet tool restore
dotnet ef dbcontext scaffold 'Name=ConnectionStrings:WorkNotes' Microsoft.EntityFrameworkCore.SqlServer --project WorkNotes.DataAccess --startup-project WorkNotes.Web --context WorkNotesDbContext --context-dir Context --output-dir Entities --namespace WorkNotes.DataAccess.Entities --context-namespace WorkNotes.DataAccess.Context --table dbo.DatabaseVersion --table dbo.WorkContexts --table dbo.ContextMembers --table dbo.Notes --table dbo.NoteBlocks --table dbo.NoteReferences --table dbo.NoteReferenceTargets --table dbo.WorkReferences --table dbo.ReferenceTypes --table dbo.GitConnections --table dbo.GitRepositories --table dbo.GitReferences --table dbo.NoteBlockGitReferences --no-onconfiguring --force
dotnet build WorkNotes.sln
dotnet test WorkNotes.sln --no-build --no-restore
```

- Comanda folosește Web pentru pornire și citirea configurației, dar generează fișierele numai în DataAccess. `--no-onconfiguring` păstrează conexiunea în configurație, fără să o scrie în clasele generate; `Program.cs` transmite connection string-ul extensiei `AddDataAccess`, care înregistrează DbContext-urile și providerul SQL Server.
- `--force` suprascrie `Context/WorkNotesDbContext.cs` și `Entities/DatabaseVersion.cs`, `WorkContext.cs`, `ContextMember.cs`, `Note.cs`, `NoteBlock.cs` (și, cu PR #4, `NoteReference.cs`, `NoteReferenceTarget.cs`, `WorkReference.cs` și `ReferenceType.cs`) din DataAccess. `--table dbo.NoteReferences`, `--table dbo.NoteReferenceTargets`, `--table dbo.WorkReferences` și `--table dbo.ReferenceTypes` vin cu PR #4: pe o bază fără tabelele lor, scaffolding-ul doar avertizează că nu le găsește. `NoteReferenceTargets` are, pe lângă cele două chei, coloana `CreatedAtUtc`, deci scaffolding-ul o generează ca entitate (`NoteReferenceTarget`), nu ca legătură many-to-many fără clasă. Extensiile scrise manual se pun în fișiere partial separate (contextul are `OnModelCreatingPartial`). Pentru tabele noi extindeți lista `--table`, astfel încât regenerarea să includă toate entitățile necesare.
- Scaffolding-ul scrie fișierele cu CRLF (și BOM); repository-ul folosește LF.
- Scaffolding-ul are nevoie de o bază la care s-au aplicat toate scripturile; comanda nu modifică baza.
- Documentație externă: [EF Core SQL Server](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/), [comenzile EF Core, inclusiv dbcontext scaffold](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).

## Tabele

### DatabaseVersion

| Coloană | Tip | Note |
| --- | --- | --- |
| `Version` | nvarchar(50) NOT NULL | cheie primară `PK_DatabaseVersion`; eticheta versiunii, de exemplu `v.0.03` |

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
| `JournalDate` | date NULL | obligatorie pentru jurnal (`CK_Notes_JournalDate`); PR #4: la schimbarea tipului din editor, ziua locală a creării pentru un jurnal, NULL pentru un articol |
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

### NoteReferences (PR #4, neintegrat în `main`)

Referințele interne: câte un rând pentru fiecare paragraf și referință (CR, bug sau alt tip din `ReferenceTypes`) care deschide cel puțin o notă ([DOMAIN-MODEL.md](DOMAIN-MODEL.md#în-dezvoltare)). Tabela este cea creată de `version_0.02/004_ReplaceNoteReferences.sql`, care o înlocuiește pe cea din `002_CreateNoteReferences.sql`, fără coloana `TargetNoteId`, pe care `005_CreateNoteReferenceTargets.sql` o mută în `NoteReferenceTargets`.

| Coloană | Tip | Note |
| --- | --- | --- |
| `Id` | int IDENTITY | `PK_NoteReferences` |
| `NoteBlockId` | uniqueidentifier NOT NULL | paragraful; cheie externă către `NoteBlocks`, cascadă |
| `ReferenceType` | nvarchar(20) NOT NULL | tipul, cu majuscule (`CR`, `BUG`); cheie externă către `ReferenceTypes`, fără cascadă (`FK_NoteReferences_ReferenceTypes_ReferenceType`, din `011`; înainte, `CK_NoteReferences_ReferenceType` permitea numai `CR` și `BUG`) |
| `ReferenceNumber` | bigint NOT NULL | numărul, fără zerourile de la început (`CK_NoteReferences_ReferenceNumber`: 0 până la 18 cifre) |
| `ReferenceText` | nvarchar(100) NOT NULL | textul primei apariții în paragraf, ca scris (`CR_30080`); cel mult 78 de caractere (un tip de 10 litere, 50 de spații, 18 cifre) |
| `NormalizedReference` | nvarchar(30) NOT NULL | `CR:30080`, `BUG:1234`; `CK_NoteReferences_NormalizedReference` îl leagă de tip și număr |
| `CreatedAtUtc` | datetime2(0) NOT NULL | implicit `SYSUTCDATETIME()`; data la care paragraful a primit referința (prima ei notă) |
| `WorkReferenceId` | int NOT NULL | referința din catalog (`WorkReferences`), cu tipul și numărul rândului; cheie externă fără cascadă (adăugată de `008_UpdateNoteReferencesWorkReferenceId.sql`) |

Textul paragrafului nu conține linkul: editorul îl desenează din aceste rânduri. `002_CreateNoteReferences.sql` (tabela veche: `SourceNoteId`, `TargetNoteId`, `DisplayText`, cu legătura păstrată în text ca `[[note:{id}|{număr}]]`) și `003_InsertNoteReferences.sql` rămân așa cum au fost aplicate; `004` elimină tabela veche și transformă legăturile vechi din text înapoi în numărul pe care îl afișau ([Scripts/README.md](../Scripts/README.md)).

### NoteReferenceTargets (PR #4, neintegrat în `main`)

Notele pe care le deschide o referință: câte un rând pentru fiecare referință și notă, o legătură 1–M cu `NoteReferences`. O referință pe care mai multe note o au în titlu are un rând pentru fiecare dintre ele. Tabela este creată de `version_0.02/005_CreateNoteReferenceTargets.sql`.

| Coloană | Tip | Note |
| --- | --- | --- |
| `NoteReferenceId` | int NOT NULL | referința; cheie externă către `NoteReferences`, cascadă |
| `TargetNoteId` | int NOT NULL | nota pe care o deschide; cheie externă către `Notes`, fără cascadă |
| `CreatedAtUtc` | datetime2(0) NOT NULL | implicit `SYSUTCDATETIME()`; data la care referința a primit această notă |

Cheia primară `PK_NoteReferenceTargets` este `NoteReferenceId` + `TargetNoteId`: o notă apare o singură dată la o referință, iar cheia servește și citirea notelor unei referințe. `005` a mutat aici nota fiecărui rând din `004`, cu data lui.

### WorkReferences (PR #4, neintegrat în `main`)

Catalogul referințelor: fiecare CR, bug sau referință de alt tip o singură dată, după tip și număr, cu ID-ul lui, oricum ar fi scris în paragrafe (`CR_30080`, `cr-30080`). Fiecare rând din `NoteReferences` are ID-ul referinței lui (`WorkReferenceId`), lângă textul ei. Tabela este creată de `version_0.02/006_CreateWorkReferences.sql` și completată de `007_InsertWorkReferences.sql` cu referințele stocate până atunci; aplicația adaugă o referință prima dată când un paragraf o stochează.

| Coloană | Tip | Note |
| --- | --- | --- |
| `Id` | int IDENTITY | `PK_WorkReferences`; ID-ul referinței |
| `ReferenceType` | nvarchar(20) NOT NULL | tipul, cu majuscule; cheie externă către `ReferenceTypes`, fără cascadă (`FK_WorkReferences_ReferenceTypes_ReferenceType`, din `011`; înainte, `CK_WorkReferences_ReferenceType` permitea numai `CR` și `BUG`) |
| `ReferenceNumber` | bigint NOT NULL | numărul, fără zerourile de la început (`CK_WorkReferences_ReferenceNumber`: 0 până la 18 cifre) |
| `NormalizedReference` | nvarchar(30) NOT NULL | `CR:30080`, `BUG:1234`; `CK_WorkReferences_NormalizedReference` îl leagă de tip și număr |
| `CreatedAtUtc` | datetime2(0) NOT NULL | implicit `SYSUTCDATETIME()`; data la care referința a intrat în catalog |

Cheia unică este tipul și numărul (`UX_WorkReferences_ReferenceType_ReferenceNumber`); `UX_WorkReferences_NormalizedReference` este aceeași cheie în forma după care aplicația caută referințele. Catalogul este comun tuturor contextelor: un rând conține numai tipul și numărul. Un rând rămâne și când niciun paragraf nu mai scrie referința, deci o referință își păstrează ID-ul. Referințele scrise numai în titluri sau fără nicio notă destinație nu au rând în `NoteReferences` și nici în catalog.

### ReferenceTypes (PR #4, neintegrat în `main`)

Tipurile de referință: prefixele cu care se scrie o referință (`CR 30080`, `bug 1234`), o tabelă de configurare. Aplicația citește orice text cu tipurile active ([DOMAIN-MODEL.md](DOMAIN-MODEL.md#în-dezvoltare)). Tabela este creată de `version_0.02/009_CreateReferenceTypes.sql`, iar `010_InsertReferenceTypes.sql` adaugă `CR` și `BUG`, active.

| Coloană | Tip | Note |
| --- | --- | --- |
| `Code` | nvarchar(20) NOT NULL | `PK_ReferenceTypes`; tipul, așa cum îl stochează `NoteReferences` și `WorkReferences`: 1–10 litere ASCII mari (`CK_ReferenceTypes_Code`, `NoteReferenceRules.ValidType`); în text se citește cu orice combinație de litere mari și mici |
| `IsActive` | bit NOT NULL | 1: tipul se citește în texte; 0: nu se mai citește (fără valoare implicită: se scrie la inserare) |
| `CreatedAtUtc` | datetime2(0) NOT NULL | implicit `SYSUTCDATETIME()` |

Un tip se adaugă cu `INSERT INTO dbo.ReferenceTypes (Code, IsActive) VALUES (N'TASK', 1);` și se dezactivează cu `UPDATE dbo.ReferenceTypes SET IsActive = 0 WHERE Code = N'TASK';`. Un tip cu care sunt stocate referințe (în `NoteReferences` sau în catalog) nu se poate șterge: cheile externe îl păstrează, deci se dezactivează. Aplicația ține tipurile în memorie și le citește din nou după cel mult 5 minute (`ReferenceTypeCache.Duration`): o schimbare se vede în acest interval sau la repornire. Textele deja salvate urmează schimbarea la următoarea lor salvare (titlurile, la următoarea schimbare) sau când se rulează `012_RefreshNoteReferences.sql`.

### GitConnections (versiunea 0.03)

Conturile Git conectate prin OAuth: câte un rând pentru fiecare utilizator și furnizor (deocamdată numai GitHub). Tabela este creată de `version_0.03/001_CreateGitConnections.sql`; regulile sunt în [AGENTS.md](../AGENTS.md#integrarea-github) și [ADR-004](decisions/ADR-004-github-oauth.md).

| Coloană | Tip | Note |
| --- | --- | --- |
| `UserId` | nvarchar(128) NOT NULL | utilizatorul WorkNotes; cheie externă către `Users`, cascadă |
| `Provider` | nvarchar(20) NOT NULL | `GitHub` (`CK_GitConnections_Provider`, constantele `GitProviders`) |
| `AccountId` | nvarchar(50) NOT NULL | ID-ul stabil al contului la furnizor (pe GitHub, `id` numeric) |
| `AccountLogin` | nvarchar(100) NOT NULL | login-ul contului, actualizat la fiecare verificare |
| `ProtectedAccessToken` | nvarchar(max) NOT NULL | tokenul de acces, criptat cu Data Protection; niciodată în clar |
| `ProtectedRefreshToken` | nvarchar(max) NULL | refresh token-ul criptat; numai pentru tokenurile care expiră (GitHub App) |
| `AccessTokenExpiresAtUtc`, `RefreshTokenExpiresAtUtc` | datetime2(0) NULL | expirările; NULL pentru un token fără expirare (OAuth App) |
| `Scopes` | nvarchar(500) NULL | scopurile acordate, informativ (OAuth App) |
| `ConnectedAtUtc` | datetime2(0) NOT NULL | implicit `SYSUTCDATETIME()`; data conectării (o reconectare o schimbă) |
| `ValidatedAtUtc` | datetime2(0) NOT NULL | ultima verificare reușită a tokenului la furnizor |

Cheia primară `PK_GitConnections` este `UserId` + `Provider`. Emailul normalizat este identificatorul funcțional cerut de fluxul GitHub și este unic în `Users`; conexiunea păstrează cheia internă `UserId` ca FK, evitând duplicarea emailului și rămânând asociată aceleiași identități. Tokenurile se pot decripta numai cu cheile Data Protection ale aplicației: fără ele, rândul rămâne, iar utilizatorul trebuie să se conecteze din nou.

### GitRepositories (versiunea 0.03)

Repository-urile Git importate de fiecare utilizator, alese dintre cele pe care furnizorul (deocamdată GitHub) i le arată contului conectat. Tabela este creată de `version_0.03/002_CreateGitRepositories.sql`.

| Coloană | Tip | Note |
| --- | --- | --- |
| `Id` | int IDENTITY | `PK_GitRepositories` |
| `UserId` | nvarchar(128) NOT NULL | utilizatorul care l-a importat; cheie externă către `Users`, cascadă |
| `Provider` | nvarchar(20) NOT NULL | `GitHub` (`CK_GitRepositories_Provider`) |
| `ExternalId` | nvarchar(50) NOT NULL | ID-ul stabil al repository-ului la furnizor (pe GitHub, `id` numeric), care rămâne la redenumire și transfer |
| `FullName` | nvarchar(200) NOT NULL | `owner/name`, la ultima salvare |
| `Description` | nvarchar(400) NULL | descrierea de la furnizor, fără caractere de control, tăiată la coloană |
| `IsPrivate` | bit NOT NULL | privat sau public |
| `DefaultBranch` | nvarchar(255) NULL | branch-ul implicit |
| `HtmlUrl` | nvarchar(300) NOT NULL | adresa https a repository-ului |
| `ImportedAtUtc` | datetime2(0) NOT NULL | implicit `SYSUTCDATETIME()`; data importului |
| `RefreshedAtUtc` | datetime2(0) NOT NULL | ultima salvare a selecției, când descrierea a fost citită din nou |

`UX_GitRepositories_UserId_Provider_ExternalId` (unic) păstrează un rând pe utilizator, furnizor și repository și servește citirea repository-urilor unui utilizator. Un repository importat rămâne după deconectarea contului GitHub; nu are cheie externă către `GitConnections`.

### GitReferences (versiunea 0.03)

Catalogul referințelor Git: un branch al unui repository, o singură dată, oricâte paragrafe îl leagă. Tabela este creată de `version_0.03/003_CreateGitReferences.sql`.

| Coloană | Tip | Note |
| --- | --- | --- |
| `Id` | int IDENTITY | `PK_GitReferences` |
| `Provider` | nvarchar(20) NOT NULL | `GitHub` (`CK_GitReferences_Provider`) |
| `RepositoryExternalId` | nvarchar(50) NOT NULL | ID-ul stabil al repository-ului la furnizor |
| `RepositoryFullName` | nvarchar(200) NOT NULL | `owner/name` la ultima legare |
| `RepositoryUrl` | nvarchar(300) NOT NULL | adresa https a repository-ului, din care se face linkul branch-ului |
| `Kind` | nvarchar(20) NOT NULL | `Branch` (`CK_GitReferences_Kind`, constantele `GitReferenceKinds`); pregătit pentru commit-uri și pull request-uri |
| `Name` | nvarchar(255) NOT NULL | numele branch-ului, cu colația `Latin1_General_100_BIN2`: Git face diferența dintre majuscule și minuscule |
| `CreatedAtUtc` | datetime2(0) NOT NULL | implicit `SYSUTCDATETIME()` |

Cheia unică `UX_GitReferences_Provider_RepositoryExternalId_Kind_Name` păstrează un rând pe branch. Catalogul nu depinde de utilizator: un rând conține numai date publice ale repository-ului și numele branch-ului.

### NoteBlockGitReferences (versiunea 0.03)

Legăturile dintre un paragraf, referința pe care o scrie (`WorkReferences`, de exemplu `CR:30080`) și un branch din catalog.

| Coloană | Tip | Note |
| --- | --- | --- |
| `Id` | int IDENTITY | `PK_NoteBlockGitReferences` |
| `NoteBlockId` | uniqueidentifier NOT NULL | paragraful; cheie externă către `NoteBlocks`, cascadă |
| `GitReferenceId` | int NOT NULL | branch-ul; cheie externă către `GitReferences`, fără cascadă |
| `WorkReferenceId` | int NOT NULL | referința din catalog pe care o scrie paragraful; cheie externă către `WorkReferences`, fără cascadă |
| `CreatedAtUtc` | datetime2(0) NOT NULL | implicit `SYSUTCDATETIME()` |
| `CreatedByUserId` | nvarchar(128) NOT NULL | cine a făcut legătura; cheie externă către `Users` |

`UX_NoteBlockGitReferences_NoteBlockId_GitReferenceId_WorkReferenceId` (unic) face legarea idempotentă; `IX_NoteBlockGitReferences_GitReferenceId` și `IX_NoteBlockGitReferences_WorkReferenceId` servesc cheile externe. Legătura se citește numai cât timp paragraful scrie încă referința (filtru la citire, în `NoteGitReferenceRepository`); dacă textul o scrie din nou, legătura reapare. Un paragraf șters își pierde legăturile în cascadă; rândurile din `GitReferences` rămân.

## Relații

```text
Users 1──* ContextMembers *──1 WorkContexts 1──* Notes 1──* NoteBlocks
NoteBlocks 1──* NoteReferences 1──* NoteReferenceTargets *──1 Notes  (PR #4: paragraful → referința → fiecare notă destinație, TargetNoteId)
WorkReferences 1──* NoteReferences  (PR #4: referința din catalog, WorkReferenceId)
ReferenceTypes 1──* NoteReferences, WorkReferences  (PR #4: tipul, ReferenceType)
Users 1──* Notes       (OwnerUserId, CreatedByUserId, ModifiedByUserId)
Users 1──* NoteBlocks  (CreatedByUserId, ModifiedByUserId)
Users 1──* AspNetUserClaims / AspNetUserLogins / AspNetUserTokens
Users 1──* GitConnections  (0.03: un rând pe furnizor, UserId + Provider)
NoteBlocks 1──* NoteBlockGitReferences *──1 GitReferences  (0.03: paragraful → branch-ul legat)
WorkReferences 1──* NoteBlockGitReferences  (0.03: referința pe care o scrie paragraful)
Users 1──* GitRepositories (0.03: repository-urile importate, UserId + Provider + ExternalId unic)
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
| `NoteReferences` (PR #4) | `Id` | `UX_NoteReferences_NoteBlockId_NormalizedReference` (unic: un rând pe paragraf și referință; servește și căutarea după `NoteBlockId`), `IX_NoteReferences_NormalizedReference`, `IX_NoteReferences_WorkReferenceId` (paragrafele care stochează o referință) |
| `NoteReferenceTargets` (PR #4) | `NoteReferenceId`, `TargetNoteId` | `IX_NoteReferenceTargets_TargetNoteId` (rândurile care deschid o notă, pentru ștergerea ei) |
| `WorkReferences` (PR #4) | `Id` | `UX_WorkReferences_ReferenceType_ReferenceNumber` (unic: cheia referinței), `UX_WorkReferences_NormalizedReference` (unic: aceeași cheie, pentru căutare) |
| `ReferenceTypes` (PR #4) | `Code` | — |
| `GitConnections` (0.03) | `UserId`, `Provider` | — |
| `GitRepositories` (0.03) | `Id` | `UX_GitRepositories_UserId_Provider_ExternalId` (unic) |
| `GitReferences` (0.03) | `Id` | `UX_GitReferences_Provider_RepositoryExternalId_Kind_Name` (unic) |
| `NoteBlockGitReferences` (0.03) | `Id` | `UX_NoteBlockGitReferences_NoteBlockId_GitReferenceId_WorkReferenceId` (unic), `IX_NoteBlockGitReferences_GitReferenceId`, `IX_NoteBlockGitReferences_WorkReferenceId` |

Indexul unic `UX_Notes_DailyJournal` (un jurnal pe proprietar, context și zi), creat de `006_CreateNotes.sql`, a fost eliminat de `007_AllowSeveralJournalsPerDay.sql`: sunt permise mai multe jurnale pe zi.

## Convenții

- Tabelele au nume PascalCase la plural (`WorkContexts`, `ContextMembers`, `Notes`, `NoteBlocks`), cu excepția `DatabaseVersion`, a tabelei Identity redenumite `Users` și a tabelelor `AspNetUser*`.
- Coloanele sunt PascalCase; momentele sunt UTC, cu sufixul `AtUtc` și tipul `datetime2(0)` (precizie la secundă); utilizatorii sunt referiți prin `…UserId` nvarchar(128); textele de cod (`NoteType`, `Visibility`, `Role`) sunt nvarchar(20) cu constrângeri `CHECK` aliniate la constantele Business; PR #4: tipul unei referințe (`ReferenceType`) nu este o constantă, ci o cheie externă către tabela de configurare `ReferenceTypes`.
- Numele constrângerilor: `PK_{Tabelă}`, `FK_{Tabelă}_{TabelăReferită}_{Coloană}`, `UX_{Tabelă}_{Coloane}` (index unic), `IX_{Tabelă}_{Coloane}`, `CK_{Tabelă}_{Regulă}`, `DF_{Tabelă}_{Coloană}`.

## Coloane de audit

- `Notes` și `NoteBlocks`: `CreatedAtUtc`, `CreatedByUserId`, `ModifiedAtUtc`, `ModifiedByUserId`. `ContextMembers`: `AddedAtUtc`. `WorkContexts` nu are audit.
- Aplicația scrie momentele trunchiate la secundă (`NoteService`), ca să se citească la fel după reîncărcare.
- `Notes.ModifiedAtUtc` este NOT NULL și primește la inserare aceeași valoare ca `CreatedAtUtc`; `NoteRepository` raportează o notă cu `ModifiedAtUtc` egal cu crearea ca nemodificată (`ModifiedAtUtc = null` în `NoteSummary`). Tabla folosește ultima modificare, `ISNULL(ModifiedAtUtc, CreatedAtUtc)`.
- Mutarea unui paragraf (schimbarea `Position`) și schimbul ordinii pe tablă nu modifică auditul; numai schimbarea textului sau a titlului îl actualizează (cu PR #4, și a tipului notei).

## Reguli de ștergere

| Operație | Efect |
| --- | --- |
| Ștergerea unui context | Refuzată dacă are note: `FK_Notes_WorkContexts_ContextId` nu are cascadă, iar eroarea 547 devine `WorkContextDeleteStatus.InUse` („Contextul are note și nu poate fi șters.”). Fără note, membrii se șterg în cascadă. |
| Ștergerea unei note | Ștergere fizică (`ExecuteDeleteAsync`), numai de proprietar; paragrafele se șterg în cascadă. Nu există ștergere logică; `ArchivedAtUtc` nu are interfață. Cu PR #4, în aceeași tranzacție serializabilă: întâi se șterg referințele pentru care nota era singura notă (cu rândurile lor din `NoteReferenceTargets`, în cascadă), apoi rândurile `NoteReferenceTargets` care o deschid din celelalte referințe (`FK_NoteReferenceTargets_Notes_TargetNoteId` nu poate avea cascadă: ar fi a doua cale de cascadă din `Notes`), apoi nota, cu paragrafele, referințele lor și notele acestora în cascadă. Nu urmează nicio recalculare: o notă ștearsă doar scoate o destinație. |
| Ștergerea unui paragraf (PR #4) | Rândurile lui din `NoteReferences` și din `NoteReferenceTargets` se șterg în cascadă (`FK_NoteReferences_NoteBlocks_NoteBlockId`, `FK_NoteReferenceTargets_NoteReferences_NoteReferenceId`); notele destinație și referințele din `WorkReferences` rămân. |
| Ștergerea unei referințe din catalog (PR #4) | Nu există în aplicație: rândurile din `WorkReferences` rămân și după ce niciun paragraf nu le mai stochează; `FK_NoteReferences_WorkReferences_WorkReferenceId` nu are cascadă și refuză ștergerea unei referințe stocate. |
| Ștergerea unui tip de referință (PR #4) | Nu există în aplicație. În SQL, `FK_NoteReferences_ReferenceTypes_ReferenceType` și `FK_WorkReferences_ReferenceTypes_ReferenceType`, fără cascadă, refuză ștergerea unui tip cu care sunt stocate referințe (și rândurile catalogului nu se șterg), deci un tip folosit se dezactivează (`IsActive = 0`). Un tip încă nefolosit se poate șterge; până la următoarea citire a tipurilor (cel mult 5 minute), salvarea unei referințe de acel tip dă eroare. |
| Deconectarea GitHub (0.03) | Ștergere fizică a rândului din `GitConnections` (`ExecuteDeleteAsync`), numai a utilizatorului curent, apoi revocarea autorizării la GitHub. |
| Scoaterea unui repository importat (0.03) | Ștergere fizică a rândului din `GitRepositories` când utilizatorul îl debifează și salvează; repository-ul de pe GitHub nu este atins. Deconectarea GitHub nu îl șterge. |
| Scoaterea unei legături Git (0.03) | Ștergere fizică a rândului din `NoteBlockGitReferences` (numai proprietarul notei); branch-ul din `GitReferences` și din repository nu sunt atinse. Ștergerea unui paragraf o face în cascadă. |
| Eliminarea unui membru | Șterge numai apartenența cu rolul `Member`; proprietarul nu poate fi eliminat. |
| Ștergerea unui utilizator | Nu există în aplicație. În SQL, apartenențele și tabelele `AspNetUser*` se șterg în cascadă, iar conexiunile și repository-urile Git (`GitConnections`, `GitRepositories`, 0.03) la fel, dar cheile externe din `Notes` și `NoteBlocks` către `Users` nu au cascadă și blochează ștergerea unui utilizator care are note sau paragrafe. |

## Concurență

- `Notes.RowVersion` protejează documentul: versiunea citită circulă ca text Base64 (8 octeți) în `NoteSummary.Version` și `NoteDocument.Version`. La salvare, repository-ul setează versiunea așteptată ca valoare originală și actualizează mereu rândul notei, astfel încât o salvare dintr-un editor învechit primește `Conflict`, fără să suprascrie nimic, inclusiv la două salvări în aceeași secundă.
- Redenumirea și schimbul ordinii schimbă `RowVersion`; schimbul este condiționat de versiunile ambelor note, într-un singur `UPDATE`. Editorul din aceeași pagină primește noile versiuni; un editor deschis în altă fereastră primește conflict la următoarea salvare.
- `NoteBlocks.RowVersion` este configurat ca token de concurență; verificarea documentului se face prin versiunea notei.
- O notă nouă primește `MIN([Order]) - 1` din contextul ei, citit cu `UPDLOCK, HOLDLOCK` în aceeași tranzacție, deci notele create simultan primesc valori diferite.
- Indexul unic pe `WorkContexts.Name`, cheia primară din `ContextMembers` și indexul unic pe `NormalizedEmail` protejează salvările concurente; scripturile de versiune inserează cu `UPDLOCK, HOLDLOCK`.
- PR #4: tipurile de referință sunt citite din `ReferenceTypes` o dată pentru toată aplicația și din nou după cel mult 5 minute; o cerere folosește aceleași tipuri de la început la sfârșit. O schimbare a tipurilor nu atinge rândurile stocate: le schimbă următoarea salvare a fiecărui paragraf sau `012_RefreshNoteReferences.sql`.
- PR #4: o referință nouă intră în `WorkReferences` înainte de salvarea paragrafelor, printr-un `INSERT … WHERE NOT EXISTS` cu `UPDLOCK, HOLDLOCK`, astfel încât două salvări simultane o adaugă o singură dată; dacă salvarea eșuează apoi, rândul rămâne în catalog.
- PR #4: referințele unei note, cu notele lor și cu ID-urile din catalog, se scriu în aceeași tranzacție cu paragrafele ei, sub verificarea `RowVersion` a notei; o notă destinație ștearsă între timp face salvarea să răspundă `Conflict` (eroarea 547), fără să scrie nimic. Recalcularea după schimbarea unui titlu sau crearea unei note este o tranzacție separată, făcută după ce operația s-a salvat: scrie numai paragrafele al căror `NoteBlocks.RowVersion` este cel citit (un paragraf salvat între timp și-a primit referințele de la salvare) și reia citirea o dată dacă o destinație a fost ștearsă între timp. Două operații simultane pe aceeași referință, în același context, pot lăsa o legătură învechită până la următoarea salvare a paragrafului sau a titlului (sau până la o nouă rulare a `012_RefreshNoteReferences.sql`; înainte de `008`, a `005_CreateNoteReferenceTargets.sql`); la fel o cerere întreruptă între operație și recalculare.

- Versiunea 0.03: o conexiune Git se salvează (inserare sau înlocuire) după autorizare; două conectări simultane ale aceluiași utilizator se rezolvă prin cheia primară (a doua devine o actualizare). O verificare actualizează numai un rând existent: o deconectare făcută între timp nu este anulată. Selecția repository-urilor se înlocuiește într-un singur `SaveChangesAsync`; o salvare concurentă a aceluiași utilizator (indexul unic sau un rând șters între timp) este refuzată, fără schimbări (`Conflict`).

## Scripturile și actualizarea bazei

Schema se modifică numai prin scripturi noi, idempotente, în folderul versiunii curente, aplicate la cerere explicită, urmate de scaffolding și de actualizarea acestui document. Lista scripturilor, ordinea, comenzile și verificările după aplicare: [Scripts/README.md](../Scripts/README.md).

## În dezvoltare și planificat

- PR #4 (branch `main_task_02`, neintegrat în `main`) adaugă tabelele `NoteReferences` ([mai sus](#notereferences-pr-4-neintegrat-în-main)) și `NoteReferenceTargets` ([mai sus](#notereferencetargets-pr-4-neintegrat-în-main)) prin `Scripts/version_0.02/002_CreateNoteReferences.sql`, `003_InsertNoteReferences.sql` (modelul vechi, păstrate așa cum au fost aplicate), `004_ReplaceNoteReferences.sql` (referințele pe paragraf, cu o singură notă), `005_CreateNoteReferenceTargets.sql` (una sau mai multe note pe referință) `006_CreateWorkReferences.sql`–`008_UpdateNoteReferencesWorkReferenceId.sql` (catalogul referințelor și cheia lui în `NoteReferences`), `009_CreateReferenceTypes.sql`–`011_UpdateReferenceTypeKeys.sql` (tipurile configurabile și cheile lor externe) și `012_RefreshNoteReferences.sql` (reindexarea cu tipurile active, care se poate rula oricând din nou). Tabelele `WorkReferences` și `ReferenceTypes` sunt descrise [mai sus](#workreferences-pr-4-neintegrat-în-main). Marcajele „PR #4” din acest document dispar la integrarea PR-ului.
- Tabelele planificate din planul inițial (`NoteWorkReferences`, `NoteBlockWorkReferences`, `NoteLinks`, `NotePlatforms` și ulterior `NoteClients`, `NoteProjects`, `NoteBranches`, `NoteEvents`, `NoteReleases`, `NotePublishes`), precum și titlul și URL-ul extern planificate pentru `WorkReferences`, sunt descrise în [DOMAIN-MODEL.md](DOMAIN-MODEL.md#entități-planificate).
