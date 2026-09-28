# 02 — Model de date

Abordarea este **Database First**: schema se scrie în scripturi SQL explicite, idempotente, în folderul versiunii curente (`Scripts/version_0.01`, iar de la taskul 02 `Scripts/version_0.02`; un modul nou nu incrementează versiunea), apoi clasele EF se regenerează prin scaffolding (`--no-onconfiguring`). Nu există migrări EF. Detaliile de rulare sunt în [README](../../README.md).

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

## Scripturi (version_0.02)

| Script | Conținut |
| --- | --- |
| 000_UpdateDatabaseVersion | Versiunea `v.0.02`, rând nou lângă `v.0.01` |
| 001_AddNoteOrder | Coloana `[Order]` a notelor, numerotată în ordinea de atunci a tablei, și indexul `IX_Notes_ContextId_Order` |
| 002_CreateNoteReferences | Tabela `NoteReferences` (referințele interne dintre note), cheile externe și indexurile ei |

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
| `Order` | locul notei în luna ei pe tablă, crescător (cuvânt rezervat: `[Order]` în SQL); o notă nouă primește minimul contextului minus 1 |

Tabla grupează notele pe luni după `ISNULL(ModifiedAtUtc, CreatedAtUtc)`: pentru că `ModifiedAtUtc` nu este niciodată NULL, repository-ul raportează o notă cu `ModifiedAtUtc = CreatedAtUtc` ca nemodificată. În fiecare lună notele sunt ordonate după `Order` crescător, apoi după ultima modificare, creare și `Id`, descrescător. Scriptul 001 a numerotat notele existente pe context (jurnalele, apoi articolele, fiecare de la ultima modificare), așa că aranjarea de dinainte s-a păstrat. Schimbul prin drag-and-drop interschimbă valorile `Order` ale celor două note, fără să atingă auditul.

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

Textul notei există numai în `NoteBlocks` (nicio copie în `Notes`). Limite: maximum 5000 de paragrafe și 1 000 000 de caractere pe notă (`NoteRules`). O referință internă către altă notă este păstrată chiar în text, ca `[[note:{id}|{număr}]]` (`NoteReferenceRules`): se mută, se copiază și se șterge odată cu textul, iar ID-ul destinației nu depinde de titlul ei.

### NoteReferences
| Coloană | Rol |
| --- | --- |
| `Id` | identitate |
| `SourceNoteId` | nota care conține referința (cascadă la ștergerea ei) |
| `TargetNoteId` | nota destinație (fără cascadă: aplicația șterge rândurile care o indică înainte să o șteargă) |
| `DisplayText` | numărul afișat, nvarchar(20), numai cifre (`CK_NoteReferences_DisplayText`) |
| `CreatedAtUtc` | prima salvare a referinței |

Tabela este evidența referințelor din textul fiecărei note, de la ultima salvare a textului: câte un rând pentru fiecare sursă, destinație și număr (`UX_NoteReferences_SourceNoteId_TargetNoteId_DisplayText`), oricâte apariții ar avea în text. Conține numai referințele către alte note ale aceluiași context pe care proprietarul le poate vedea (`CK_NoteReferences_OtherNote` exclude nota însăși); celelalte rămân în text, marcate ca referințe care nu se mai pot deschide. `IX_NoteReferences_TargetNoteId` servește ștergerea destinației și lista viitoare „Referințe către această notă”.

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

Relațiile se fac prin chei externe explicite, nu printr-o tabelă generică `EntityType + EntityId`. Toate asocierile trebuie să respecte contextul notei. Poziția unei referințe în text (pentru evidențiere în CodeMirror) este o problemă separată: ancorele trebuie actualizate la editare, numărul rândului nu ajunge. Referințele interne dintre note au rezolvat-o păstrând referința în text (vezi `NoteBlocks` și `NoteReferences`); referințele CR/bug pot folosi aceeași abordare.
