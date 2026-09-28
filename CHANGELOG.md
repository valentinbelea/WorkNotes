# Changelog

Modificările WorkNotes, grupate pe versiuni. Versiunile corespund versiunilor bazei de date (`dbo.DatabaseVersion`) și folderelor `Scripts/version_0.0x` ([docs/VERSIONING.md](docs/VERSIONING.md)). Datele sunt datele integrării în `main`; publicările nu sunt documentate, deci nu apar aici.

Categorii: **Added** (funcționalități noi), **Changed** (comportament modificat), **Fixed** (corecții), **Database** (scripturi SQL și schemă), **Documentation**.

## [Nelansat]

### Documentation

- Structura nouă de documentație: `CLAUDE.md` (cu importuri permanente), `AGENTS.md` și `README.md` mutate din `Solution/` în rădăcina repository-ului și consolidate, `CHANGELOG.md`, `docs/` (context, cerințe, model de domeniu, arhitectură, bază de date, standarde de cod, UI/UX, localizare, securitate, testare, Git, versionare, publicare, stare curentă, roadmap), ADR-urile din `docs/decisions/`, `Scripts/README.md` și câte un `CLAUDE.md` în `WorkNotes.Web`, `WorkNotes.Business` și `WorkNotes.DataAccess`.
- Conținutul din `Solution/docs/design-system.md` și `Solution/docs/planning/` a fost integrat în noile documente; corespondența este în [docs/CURRENT-STATUS.md](docs/CURRENT-STATUS.md#informații-mutate-sau-consolidate).
- Reguli noi, cerute explicit: feature branch din `main` pentru fiecare sarcină, fără dezvoltare directă pe `main`; scripturile SQL și migrările nu se aplică fără cerere explicită; scripturile livrate nu se modifică retroactiv.

PR #4 (branch `main_task_02`) este deschis și nu este inclus în `main`. Conține: ordonarea fără blocare, deschiderea notelor fără reîncărcarea tablei, Escape în Firefox și referințele interne între note, al căror prim model (legătura în text, sugestiile din editor, comanda `create-note-references`) a fost înlocuit pe 2026-09-28, la cererea utilizatorului, de modelul următor:

- **Added** — referințele interne CR/bug: `CR 30080`, `CR-30080`, `CR_30080`, `CR30080`, `bug_1234` și celelalte forme, în orice combinație de litere mari și mici, deschid singura notă a contextului care are aceeași referință în titlu (`bug1234` → „Rezolvare Bug-1234”); linkul este toată expresia, așa cum e scrisă; click sau Ctrl+Enter deschide nota într-un tab nou sau îi selectează tabul existent, readuce editorul minimizat și păstrează modificările nesalvate ale celorlalte taburi; fără JavaScript, linkuri HTML. `INoteReferenceService` / `NoteReferenceService` și `INoteReferenceRepository` ([ADR-003](docs/decisions/ADR-003-internal-references.md)).
- **Changed** — legăturile se recalculează la salvarea paragrafelor, la schimbarea titlului unei note și la crearea sau ștergerea unei note; textul paragrafelor nu se mai modifică; previzualizarea cardurilor este text simplu.
- **Removed** — sugestiile de referință din editor, handlerele `ReferenceSuggestions` și `ReferenceTargets`, comanda `create-note-references` și cheile .resx ale lor (169 de chei rămase).
- **Database** — `version_0.02/004_ReplaceNoteReferences.sql`: transformă legăturile vechi `[[note:{id}|{număr}]]` din text înapoi în număr, înlocuiește `dbo.NoteReferences` (paragraf → notă destinație, cu tipul, numărul, textul și forma normalizată `CR:30080`; cascadă cu paragraful; indexuri pe paragraf + referință, destinație și forma normalizată), reindexează toate paragrafele și listează referințele fără destinație, pe cele ambigue și jurnalul „CRs”; `002_CreateNoteReferences.sql` și `003_InsertNoteReferences.sql` rămân nemodificate.

Apoi, tot pe 2026-09-28, la cererea utilizatorului (`CR 27881` din jurnalul „CRs” nu era link, pentru că mai multe note aveau CR-ul în titlu):

- **Changed** — o referință deschide toate notele contextului care o au în titlu, vizibile proprietarului paragrafului, în afară de nota paragrafului: una, două sau mai multe, în ordinea ID-urilor; până acum, o referință cu mai multe note rămânea text. Click sau Ctrl+Enter deschide toate notele, fiecare în tabul ei (nou sau deja deschis), și o arată pe prima; tooltipul are câte un rând pentru fiecare notă; fără JavaScript, referința este link către prima notă, iar celelalte au câte un link numerotat (2, 3…). Ștergerea unei note nu mai recalculează referințele: ea scoate rândurile care o deschid, iar o referință rămasă fără note dispare ([ADR-003](docs/decisions/ADR-003-internal-references.md), deciziile 59–64).
- **Database** — `version_0.02/005_CreateNoteReferenceTargets.sql`: creează `dbo.NoteReferenceTargets` (`NoteReferenceId` → `NoteReferences`, cascadă; `TargetNoteId` → `Notes`, fără cascadă; `CreatedAtUtc`; cheia primară pe cele două ID-uri, indexul pe `TargetNoteId`), mută în ea nota fiecărui rând din `004` și elimină `NoteReferences.TargetNoteId`, cu cheia și indexul ei; reindexează toate paragrafele cu regula nouă și listează referințele fără notă, pe cele cu mai multe note și jurnalul „CRs”. `004` nu se mai rulează după `005`. Entitatea nouă `NoteReferenceTarget` și comanda de scaffolding cu `--table dbo.NoteReferenceTargets`.

Apoi, tot pe 2026-09-28, la cererea utilizatorului (fiecare referință, tipul și numărul, o singură dată, cu ID propriu, care să însoțească textele referințelor):

- **Added** — catalogul referințelor, `dbo.WorkReferences`: fiecare CR sau bug stocat o singură dată, după tip și număr (cheia unică), cu ID-ul lui, comun tuturor contextelor; fiecare rând din `NoteReferences` are ID-ul referinței lângă textul ei (`WorkReferenceId`). Aplicația adaugă o referință în catalog prima dată când un paragraf o stochează; rândurile rămân și când niciun paragraf nu o mai scrie. Afișarea și comportamentul referințelor nu se schimbă ([ADR-003](docs/decisions/ADR-003-internal-references.md), deciziile 65–69).
- **Database** — `version_0.02/006_CreateWorkReferences.sql` creează `dbo.WorkReferences` (`Id`, `ReferenceType`, `ReferenceNumber`, `NormalizedReference`, `CreatedAtUtc`; indexurile unice pe tip + număr și pe forma normalizată); `007_InsertWorkReferences.sql` adaugă în el, o singură dată, fiecare tip și număr din `dbo.NoteReferences`; `008_UpdateNoteReferencesWorkReferenceId.sql` adaugă `NoteReferences.WorkReferenceId`, o completează după tip și număr, o face obligatorie și adaugă cheia externă (fără cascadă) și indexul. `005` nu se mai rulează după `008`; `005`–`008` se aplică împreună, cu aplicația oprită, înaintea codului. Entitatea nouă `WorkReference` și comanda de scaffolding cu `--table dbo.WorkReferences`.

## [0.02] — integrată în `main` pe 2026-09-25 (PR #3)

### Added

- Ordonarea post-it-urilor prin drag-and-drop: proprietarul prinde un card de bandă și îl lasă peste altă notă a lui din aceeași lună; cele două își schimbă locurile imediat, schimbul se salvează (`POST /?handler=SwapNotes`), iar lista urmează ordinea returnată de server. Drop-ul în altă lună este ignorat; la eșec luna revine la ordinea anterioară și apare un mesaj localizat.
- Evidențierea discretă a cardului preluat, a celulelor eligibile și a țintei.
- Taburile editorului deschise în aceeași pagină primesc noile versiuni ale notelor mutate și salvează în continuare.

### Changed

- În fiecare lună, notele se ordonează după `Order` crescător, apoi după ultima modificare, creare și `Id`, cele mai recente primele; o notă nouă apare prima în luna curentă.
- Footerul afișează `v.0.02`.

### Fixed

- Nu sunt documentate corecții separate.

### Database

- `version_0.02/000_UpdateDatabaseVersion.sql`: înregistrează `v.0.02` ca rând nou, numai dacă lipsește; `v.0.01` rămâne.
- `version_0.02/001_AddNoteOrder.sql`: coloana `Notes.[Order]`, inițializată fără să schimbe aranjarea existentă, și indexul `IX_Notes_ContextId_Order`.

### Documentation

- README, ghidul de design și documentele de planificare actualizate pentru ordonarea prin drag-and-drop și versiunea 0.02.

## [0.01] — integrată în `main` pe 2026-09-22 (PR #1) și 2026-09-25 (PR #2)

### Added

- Soluția pe straturi (.NET 10, Razor Pages, EF Core Database First, SQL Server), cu versiunea aplicației citită din `DatabaseVersion` și afișată în footer.
- Conturi cu ASP.NET Core Identity: înregistrare, autentificare cu „Ține-mă minte”, datele contului, schimbarea parolei, deconectare prin POST; politica de parolă și blocarea după 5 încercări eșuate.
- Localizarea în română, engleză și poloneză (`WorkNotes.Resources`), selectorul de limbă și verificarea `tools/Test-Resources.ps1`.
- Designul „Hârtie & salvie”: tokenuri, componente, meniul sertar, subtitlul secțiunii în header, panourile pe hârtie salvie cu bandă adezivă.
- Contexte: listare, adăugare, editare și ștergere în overlay pe aceeași pagină; creatorul devine `Owner`; lista arată numai contextele în care utilizatorul este membru; numai proprietarul editează, șterge, adaugă membri după e-mail și îi elimină.
- Dashboard: o tablă pentru fiecare context, aleasă din lista de contexte; notele grupate pe luni.
- Note pe tablă: „Notă nouă” direct pe tablă, cu switch jurnal/articol și titlu opțional; mai multe jurnale pe zi; post-it-uri galbene (jurnal) și salvie (articol) cu înclinări deterministe; previzualizarea primelor paragrafe; redenumirea titlului pe loc; ștergerea cu confirmare; datele creării și ultimei modificări în headerul cardului.
- Editorul CodeMirror 6 peste tablă: paragrafe cu identitate și audit propriu, bara de informații, căutare și înlocuire, undo/redo, Ctrl+S, avertizare la părăsirea paginii, detectarea salvărilor concurente, acțiunile notei în footer, minimizarea în stânga-jos și mai multe note în taburi independente.
- Mesajele de salvare (succes, avertisment, eroare), fixe sus pe centru, cu buton de închidere, și în dialoguri.
- Sigla WN în fereastra editorului și ca favicon (SVG și `favicon.ico`).

### Changed

- Ordinea tablei: inițial, în fiecare lună, întâi jurnalele, apoi articolele; apoi gruparea și ordinea după ultima modificare (`ISNULL(modificare, creare)`).
- Panourile de conținut: de la verdele de selecție #CCE6DF la salvie #DDEADB.
- Sigla: inițial și în headerul site-ului; apoi numai în fereastra editorului, cu favicon-ul păstrat.
- Cardurile: tipul fără etichetă vizibilă (culoarea îl arată), datele pe un singur rând, banda verde pe hârtia salvie.
- Headerul editorului simplificat (sigla, taburile, Minimizează, Închide), cu acțiunile notei mutate în footer.

### Fixed

- Nu sunt documentate corecții separate.

### Database

- `version_0.01/000_CreateDatabaseVersion.sql` și `001_InsertDatabaseVersion.sql`: tabela `DatabaseVersion` și versiunea `v.0.01`.
- `002_AddIdentityUsers.sql`: tabelele Identity (`Users`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`), fără istoric EF.
- `004_CreateWorkContexts.sql`, `005_CreateContextMembers.sql`: contextele și membrii lor.
- `006_CreateNotes.sql`, `007_AllowSeveralJournalsPerDay.sql`, `008_CreateNoteBlocks.sql`: notele, eliminarea indexului „un jurnal pe zi” și paragrafele cu audit.

### Documentation

- `Solution/README.md`, `Solution/AGENTS.md`, ghidul de design `Solution/docs/design-system.md` și documentele de planificare `Solution/docs/planning/` (viziune, model de date, arhitectură, funcționalități, decizii, backlog).
