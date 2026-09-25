# 02 — Model de date

Abordarea este **Database First**: schema se scrie în scripturi SQL explicite, idempotente, în `Scripts/version_0.01` (fără incrementarea versiunii), apoi clasele EF se regenerează prin scaffolding (`--no-onconfiguring`). Nu există migrări EF. Detaliile de rulare sunt în [README](../../README.md).

## Scripturi (version_0.01)

| Script | Conținut |
| --- | --- |
| 000_CreateDatabaseVersion | Tabela `DatabaseVersion` |
| 001_InsertDatabaseVersion | Versiunea `v.0.01` |
| 002_AddIdentityUsers | Tabelele Identity (`Users` și cele asociate) |
| 004_CreateWorkContexts | Contextele |
| 005_CreateContextMembers | Membrii contextelor |
| 006_CreateNotes | Notele |
| 007_AllowSeveralJournalsPerDay | Elimină indexul unic „un jurnal pe zi” creat de 006 |
| 008_CreateNoteBlocks | Paragrafele notelor |

## Tabele realizate

### WorkContexts
`Id`, `Name` (nvarchar(100), unic global prin `UX_WorkContexts_Name`), `Description` (nvarchar(1000), opțional). Tabela nu are coloane de audit. Un context care are note nu poate fi șters (cheie externă fără cascadă din `Notes`).

### ContextMembers
`ContextId` + `UserId` (cheie primară), `Role` (`Owner` / `Member`, constrângerea `CK_ContextMembers_Role`), `AddedAtUtc`. Creatorul este adăugat ca `Owner` în aceeași tranzacție cu contextul. Ștergerea contextului sau a utilizatorului elimină membrii în cascadă.

### Notes
| Coloană | Rol |
| --- | --- |
| `Id` | identitate |
| `ContextId` | contextul (obligatoriu) |
| `OwnerUserId` | proprietarul |
| `NoteType` | `Journal` / `Article` |
| `Title` | opțional, maximum 200 de caractere, fără caractere de control |
| `JournalDate` | ziua locală a jurnalului (obligatorie pentru jurnal) |
| `Visibility` | `Private` (implicit) / `Context` |
| `CreatedAtUtc`, `CreatedByUserId` | auditul creării |
| `ModifiedAtUtc`, `ModifiedByUserId` | auditul ultimei modificări; la inserare este egal cu data creării |
| `ArchivedAtUtc` | arhivare (coloană pregătită, fără interfață încă) |
| `RowVersion` | concurență: o salvare dintr-un editor învechit este refuzată |

Ordinea pe tablă folosește `ISNULL(ModifiedAtUtc, CreatedAtUtc)`: pentru că `ModifiedAtUtc` nu este niciodată NULL, repository-ul raportează o notă cu `ModifiedAtUtc = CreatedAtUtc` ca nemodificată.

### NoteBlocks
| Coloană | Rol |
| --- | --- |
| `Id` | GUID stabil, generat de editor |
| `NoteId` | nota (cascadă la ștergerea notei) |
| `Position` | ordinea în document |
| `Content` | textul paragrafului, nvarchar(max) |
| `ActivityDate`, `IsImportant` | pregătite din plan, fără interfață încă |
| `CreatedAtUtc`, `CreatedByUserId`, `ModifiedAtUtc`, `ModifiedByUserId` | auditul paragrafului |
| `RowVersion` | pregătit pentru concurență |

Textul notei există numai în `NoteBlocks` (nicio copie în `Notes`). Limite: maximum 5000 de paragrafe și 1 000 000 de caractere pe notă (`NoteRules`).

## Regulile paragrafelor (confirmate și implementate în editor)

- un paragraf este textul dintre rânduri goale, nu rândul vizual;
- editarea păstrează identitatea și data creării; se actualizează doar auditul paragrafelor modificate;
- mutarea (schimbarea poziției) păstrează identitatea și nu contează ca modificare;
- împărțirea păstrează id-ul pe primul fragment; fragmentul desprins primește id nou;
- unirea păstrează id-ul primului paragraf;
- ștergerea urmată de undo readuce id-ul;
- textul copiat și lipit primește id nou; mutarea prin tăiere și lipire creează deocamdată tot un paragraf nou;
- `RowVersion` este mecanism de concurență, nu istoric al textului.

## Tabele planificate (din planul inițial)

| Tabelă | Rol |
| --- | --- |
| `WorkReferences` | Catalog de referințe: `Id`, `ContextId`, `ReferenceType` (`CR`, `Bug`), `Code` (text), `Title` și `ExternalUrl` opționale, audit. Unic pe `ContextId + ReferenceType + Code`. URL-urile nu se deduc din cod. |
| `NoteWorkReferences` | O notă (de obicei un articol) ↔ una sau mai multe referințe |
| `NoteBlockWorkReferences` | Un paragraf ↔ referințele relevante pentru el |
| `NoteLinks` | Legături externe: `NoteId`, `NoteBlockId` opțional, `Url`, `Label` |
| `NotePlatforms` | Nota ↔ platformele la care se referă (când există modulul de platforme) |
| Ulterior | `NoteClients`, `NoteProjects`, `NoteBranches`, `NoteEvents`, `NoteReleases`, `NotePublishes`, când există modulele respective |

Relațiile se fac prin chei externe explicite, nu printr-o tabelă generică `EntityType + EntityId`. Toate asocierile trebuie să respecte contextul notei. Poziția unei referințe în text (pentru evidențiere în CodeMirror) este o problemă separată: ancorele trebuie actualizate la editare, numărul rândului nu ajunge.
