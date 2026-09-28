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
| 002_CreateNoteReferences | Tabela `NoteReferences` a modelului anterior (nota sursă, nota destinație, numărul); înlocuită de 004 |
| 003_InsertNoteReferences | Datele tabelei modelului anterior, din textul existent; înlocuit de 004 |
| 004_ReplaceNoteReferences | Legăturile vechi din text redevin numere; tabela `NoteReferences` pe paragraf, cu cheile și indexurile ei; reindexarea tuturor paragrafelor și listele referințelor fără destinație și ambigue |
| 005_CreateNoteReferenceTargets | Tabela `NoteReferenceTargets` (notele fiecărei referințe, una sau mai multe); nota fiecărui rând din 004 mutată în ea și coloana `NoteReferences.TargetNoteId` eliminată; reindexarea tuturor paragrafelor și listele referințelor fără notă și cu mai multe note; nu se mai rulează după 008 |
| 006_CreateWorkReferences | Catalogul `WorkReferences`: fiecare referință, tipul și numărul, o singură dată, cu ID propriu |
| 007_InsertWorkReferences | Datele catalogului: fiecare tip și număr stocat în `NoteReferences` |
| 008_UpdateNoteReferencesWorkReferenceId | Coloana `NoteReferences.WorkReferenceId`, completată după tip și număr, obligatorie, cu cheia externă și indexul |

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

Textul notei există numai în `NoteBlocks` (nicio copie în `Notes`). Limite: maximum 5000 de paragrafe și 1 000 000 de caractere pe notă (`NoteRules`). O referință internă (un CR sau un bug scris în text, de exemplu `CR 30080` sau `bug_1234`) nu schimbă textul: legătura ei este un rând în `NoteReferences`, cu notele ei în `NoteReferenceTargets`, iar editorul o desenează peste text (`NoteReferenceRules`, docs/decisions/ADR-003-internal-references.md).

### NoteReferences
| Coloană | Rol |
| --- | --- |
| `Id` | identitate |
| `NoteBlockId` | paragraful care scrie referința (cascadă la ștergerea lui) |
| `ReferenceType` | `CR` / `BUG` (`CK_NoteReferences_ReferenceType`) |
| `ReferenceNumber` | numărul, bigint, fără zerourile de la început |
| `ReferenceText` | textul primei apariții în paragraf, ca scris (`CR_30080`) |
| `NormalizedReference` | `CR:30080` / `BUG:1234`, după care se compară referințele |
| `CreatedAtUtc` | data la care paragraful a primit referința |
| `WorkReferenceId` | referința din catalogul `WorkReferences` (fără cascadă) |

Câte un rând pentru fiecare paragraf și referință cu cel puțin o notă (`UX_NoteReferences_NoteBlockId_NormalizedReference`), oricâte apariții ar avea referința în paragraf. Destinațiile sunt toate notele contextului, vizibile proprietarului paragrafului și nearhivate, cu aceeași referință în titlu, în afară de nota paragrafului; fără nicio altă notă, nu există rând. Rândurile se scriu la salvarea notei, în tranzacția paragrafelor, și se recalculează la schimbarea titlului și la crearea unei note; ștergerea unei note își scoate rândurile. `IX_NoteReferences_NormalizedReference` servește căutarea paragrafelor care scriu o referință.

### NoteReferenceTargets
| Coloană | Rol |
| --- | --- |
| `NoteReferenceId` | referința (cascadă la ștergerea ei) |
| `TargetNoteId` | nota destinație (fără cascadă: aplicația șterge rândurile care o indică înainte să o șteargă) |
| `CreatedAtUtc` | data la care referința a primit nota |

Câte un rând pentru fiecare referință și notă (cheia primară `NoteReferenceId` + `TargetNoteId`). `IX_NoteReferenceTargets_TargetNoteId` servește ștergerea destinației și lista viitoare „Referințe către această notă”.

### WorkReferences
| Coloană | Rol |
| --- | --- |
| `Id` | identitate: ID-ul referinței |
| `ReferenceType` | `CR` / `BUG` |
| `ReferenceNumber` | numărul, bigint, fără zerourile de la început |
| `NormalizedReference` | `CR:30080` / `BUG:1234` |
| `CreatedAtUtc` | data la care referința a intrat în catalog |

Fiecare referință stocată o singură dată: cheia unică este tipul și numărul (`UX_WorkReferences_ReferenceType_ReferenceNumber`), iar forma normalizată este unică și ea. Catalogul este comun tuturor contextelor; o referință intră în el prima dată când un paragraf o stochează și rămâne, cu același ID, și când niciun paragraf nu o mai scrie.

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
| `WorkReferences` | Planul inițial: catalog pe context (`ContextId`, `Code` text, unic pe `ContextId + ReferenceType + Code`). Realizat în PR #4 comun tuturor contextelor, unic pe tip + număr (tabela de mai sus); rămân planificate `Title` și `ExternalUrl` opționale și auditul. URL-urile nu se deduc din cod. |
| `NoteWorkReferences` | O notă (de obicei un articol) ↔ una sau mai multe referințe |
| `NoteBlockWorkReferences` | Un paragraf ↔ referințele relevante pentru el |
| `NoteLinks` | Legături externe: `NoteId`, `NoteBlockId` opțional, `Url`, `Label` |
| `NotePlatforms` | Nota ↔ platformele la care se referă (când există modulul de platforme) |
| Ulterior | `NoteClients`, `NoteProjects`, `NoteBranches`, `NoteEvents`, `NoteReleases`, `NotePublishes`, când există modulele respective |

Relațiile se fac prin chei externe explicite, nu printr-o tabelă generică `EntityType + EntityId`. Toate asocierile trebuie să respecte contextul notei. Poziția unei referințe în text (pentru evidențiere în CodeMirror) este o problemă separată: ancorele trebuie actualizate la editare, numărul rândului nu ajunge. Referințele interne dintre note au rezolvat-o păstrând referința în text (vezi `NoteBlocks` și `NoteReferences`); referințele CR/bug pot folosi aceeași abordare.
