# Changelog

Modificările WorkNotes, grupate pe versiuni. Versiunile corespund versiunilor bazei de date (`dbo.DatabaseVersion`) și folderelor `Scripts/version_0.0x` ([docs/VERSIONING.md](docs/VERSIONING.md)). Datele sunt datele integrării în `main`; publicările nu sunt documentate, deci nu apar aici.

Categorii: **Added** (funcționalități noi), **Changed** (comportament modificat), **Fixed** (corecții), **Database** (scripturi SQL și schemă), **Documentation**.

- **Changed** — configurațiile OAuth GitHub sunt separate în `Development` și `Production`, selectate automat prin mediul ASP.NET Core; pagina admin editează independent cele două rânduri, iar lipsa configurației curente este raportată controlat. Scriptul defensiv `005_SplitGitHubConfigurationsByEnvironment.sql` migrează datele existente la `Production`.
- **Fixed** — testele configurației GitHub folosesc `CancellationToken.None`, compatibil cu versiunea xUnit a soluției, în locul API-ului inexistent `TestContext.Current`.

## [Nelansat]

### Versiunea 0.03 — integrarea cu Git (branch `main_task_03`)

Creată la cererea utilizatorului din 2026-09-29 ([docs/VERSIONING.md](docs/VERSIONING.md#versiunea-curentă), decizia 98), pentru integrarea cu GitHub.

- **Added** — popup-ul unei referințe `BUG`/`CR` inexistente oferă „Adaugă articol”; serverul caută din nou referința, reutilizează articolul apărut între timp sau creează un articol privat cu forma scrisă a referinței drept titlu, apoi îl deschide într-un tab al editorului fără a pierde modificările notei curente. Butonul este blocat în timpul cererii, iar cererile aceleiași referințe sunt reunite în fereastra editorului; erorile rămân în popup. 5 chei .resx noi, în ro/en/pl; fără schimbare de schemă.
- **Documentation** — autentificarea aplicației a fost reverificată pentru găzduire: cookie-ul Identity este valabil 14 zile, cu expirare glisantă (peste minimul cerut de 4 ore), iar `DataProtection:KeysPath` trebuie să indice un director persistent și comun instanțelor pentru ca restartul/deploy-ul să nu invalideze cookie-urile. „Ține-mă minte” continuă să controleze persistența cookie-ului în browser.

- **Changed** — integrarea OAuth GitHub citește acum Client ID, Client secret, scopes și callback URL din configurația globală protejată `GitHubConfigurations`, la fiecare operație; contractele de configurare, autorizare și verificare sunt asincrone, iar câmpurile OAuth au fost eliminate din configurația versionată. Nu există schimbare de schemă.

- **Fixed** — `/Repositories` tratează acum lipsa conexiunii ca setare personală a utilizatorului autentificat și oferă direct acțiunea „Conectează GitHub”; lipsa configurației OAuth este descrisă separat ca indisponibilitate tehnică a integrării. Emailul normalizat din identitatea autentificată este obligatoriu înainte de citirea, conectarea sau modificarea conexiunii GitHub; stocarea rămâne izolată prin cheia internă stabilă `UserId`. Mesajele pentru email lipsă și stările actualizate sunt localizate ro/en/pl.
- **Fixed** — acțiunea „Conectează GitHub” rămâne vizibilă în `/Repositories` și în pagina personală de conectare chiar când lipsesc setările OAuth; încercarea de conectare raportează separat indisponibilitatea tehnică.
- **Fixed** — butonul de conectare GitHub apare și pentru sesiunile autentificate create înainte ca emailul să fie inclus în claims: `AccountClaimsPrincipalFactory` adaugă acum claim-ul de email, iar paginile GitHub citesc profilul de cont drept fallback autoritar pentru cookie-urile existente.

Primul pas, tot pe 2026-09-29, la cererea utilizatorului (o secțiune de autentificare în Git; GitHub, OAuth, proiect nou):

- **Added** — secțiunea Conectare GitHub din Contul meu (`/Account/GitHub`, în meniu): Conectează GitHub trimite utilizatorul la GitHub prin OAuth (authorization code cu PKCE și `state`), iar la întoarcere (`/Account/GitHub/Callback`) contul este salvat și afișat, cu data conectării, a ultimei verificări și, pentru o GitHub App, expirarea accesului. Verifică conexiunea (reîmprospătează tokenul care expiră și actualizează login-ul), Conectează din nou și Deconectează (șterge conexiunea și revocă autorizarea la GitHub). Fără `GitHub:ClientId` / `GitHub:ClientSecret`, pagina separă indisponibilitatea tehnică OAuth de asocierea personală a contului. `IGitHubConnectionService` / `GitHubConnectionService`, `GitAuthorizationRules`, `IGitHubOAuthClient`, `IGitConnectionRepository` / `GitConnectionRepository`, `GitHubAuthorizationCookie`; 24 de chei .resx noi (202) ([ADR-004](docs/decisions/ADR-004-github-oauth.md), deciziile 99–110).
- **Added** — proiectul `WorkNotes.Integrations` (→ Business), cu `GitHubOAuthClient` (typed `HttpClient`), `GitHubOptions` și `AddIntegrations`, referit de Web numai în `Program.cs`.
- **Changed** — `Program.cs` configurează Data Protection (numele aplicației `WorkNotes`, folderul cheilor din `DataProtection:KeysPath`); prima pornire cere o nouă autentificare. Proiectul Web are `UserSecretsId`; `appsettings.json` are secțiunea `GitHub` fără secrete. `tools/Test-Resources.ps1` cunoaște prefixul `GitHub_` și citește și `WorkNotes.Integrations`.
- **Database** — `Scripts/version_0.03/000_UpdateDatabaseVersion.sql`: inserează `v.0.03` în `dbo.DatabaseVersion`, dacă lipsește; `v.0.01` și `v.0.02` rămân, iar footerul afișează `v.0.03` după aplicare. `001_CreateGitConnections.sql`: creează `dbo.GitConnections` (cheia `UserId` + `Provider`, cascadă cu `Users`, tokenurile criptate de aplicație). Entitatea `GitConnection` și comanda de scaffolding cu `--table dbo.GitConnections`.
- **Added** — pagina Repository-uri GitHub (`/Repositories`, în meniu, și „Alege repository-urile” pe pagina Conectare GitHub), la cererea utilizatorului din 2026-09-29: toate repository-urile GitHub ale contului conectat (proprii, de colaborator, ale organizațiilor; cel mult 1000, în ordinea numelui), cu vizibilitatea, branch-ul implicit și descrierea, câte o bifă pentru fiecare; „Salvează selecția” face din cele bifate repository-urile importate în WorkNotes, iar un repository debifat este scos. Un repository importat pe care GitHub nu-l mai arată este marcat și rămâne cât timp este bifat. `IGitRepositoryService` / `GitRepositoryService`, `GitRepositoryRules`, `IGitRepositoryRepository` / `GitRepositoryRepository`, `IGitHubOAuthClient.GetRepositoriesAsync`; 19 chei .resx noi (221) (deciziile 111–116).
- **Changed** — obținerea unui token GitHub valid, cu reîmprospătare, este serviciul comun `IGitHubTokenService` / `GitHubTokenService`, folosit și de verificarea conexiunii.
- **Database** — `Scripts/version_0.03/002_CreateGitRepositories.sql`: creează `dbo.GitRepositories` (repository-urile importate de fiecare utilizator, `ExternalId` = ID-ul GitHub, cascadă cu `Users`) și indexul unic pe utilizator, furnizor și repository. Entitatea `GitRepository` și comanda de scaffolding cu `--table dbo.GitRepositories`.
- **Added** — referința Git în popup-ul referinței abia scrise din editor, la cererea utilizatorului din 2026-09-29: popup-ul păstrează opțiunea existentă, numită acum „Referință aplicație” (nota care are referința în titlu), și adaugă „Referință Git”: se alege un repository dintre cele importate și se caută în el branch-urile al căror nume conține referința scrisă (`CR 30080`, `bug 1234`, cu aceeași regulă ca în texte: tipul din `ReferenceTypes`, separatorul, numărul, cuvânt întreg); alegerea unui branch îl leagă de paragraf (nota se salvează înainte). Legăturile apar în sertarul referințelor, sub „Referințe Git”, cu repository-ul și un link către branch, și se pot scoate. Serverul verifică proprietarul, că paragraful scrie referința, că repository-ul este printre cele importate de utilizator și că GitHub arată branch-ul. `INoteGitReferenceService` / `NoteGitReferenceService`, `GitReferenceRules`, `INoteGitReferenceRepository` / `NoteGitReferenceRepository`, `IGitHubOAuthClient.GetBranchesAsync` / `GetBranchAsync`, `IGitRepositoryService.GetImportedAsync`; handlerele `GitBranches`, `AddGitReference`, `RemoveGitReference`; 20 de chei .resx noi (241); 93 de teste noi (decizii 117–126).
- **Changed** — clicul pe o referință din text (și Ctrl+Enter), la cererea utilizatorului din 2026-09-29: dacă referința nu are branch-uri Git legate, deschide direct notele ei, ca înainte; dacă are și note, și branch-uri legate, apare un popup cu două opțiuni, „Deschide notele” (referința aplicație) și branch-ul/branch-urile ca linkuri către GitHub într-un tab nou (Tab / Shift+Tab / Esc). 4 chei .resx noi (245); linkurile trimise de server poartă și referința normalizată (`reference`), iar datele branch-urilor `normalized` (decizia 127).
- **Database** — `Scripts/version_0.03/003_CreateGitReferences.sql`: creează `dbo.GitReferences` (catalogul branch-urilor, cu numele în colație BIN2, unic pe furnizor, repository, tip și nume) și `dbo.NoteBlockGitReferences` (paragraf ↔ referință Git ↔ referință din catalog; cascadă cu paragraful). Entitățile `GitReference` și `NoteBlockGitReference` și comanda de scaffolding cu `--table dbo.GitReferences --table dbo.NoteBlockGitReferences`.
- **Documentation** — ADR-004; secțiunea „Integrarea GitHub” din `AGENTS.md`; `CLAUDE.md` pentru `WorkNotes.Integrations`; arhitectura, baza de date, securitatea (conectarea GitHub, secretele, Data Protection), publicarea (aplicația GitHub, configurarea), localizarea și testarea. Înainte: versiunea curentă 0.03 în `AGENTS.md`, `README.md`, `Scripts/README.md`, `docs/VERSIONING.md`, `docs/CURRENT-STATUS.md`, `docs/REQUIREMENTS.md`, `docs/ROADMAP.md`, `docs/DEPLOYMENT.md`, `docs/DATABASE.md` și în jurnalul deciziilor; PR #4 apare în `docs/VERSIONING.md` ca integrat în `main` (2026-09-29).

### Documentation

- Șablonul temporar solicitat pentru inserarea configurației GitHub este vizibil în `Solution/temp/InsertGitHubConfiguration.sql`; acceptă numai payload-uri Data Protection, se oprește înainte de modificări când acestea lipsesc și nu conține credențiale ori secretul OAuth expus.
- Analiza ecranului „Integrarea GitHub nu este disponibilă”: traseul exact al mesajului și al butonului, sursa configurației din `GitHubConfigurations`, cerințele Data Protection, callback-ul existent și pașii operaționali minimi pentru deblocarea OAuth, fără schimbarea fluxului aplicației sau a schemei; clarifică și că această configurație identifică aplicația, în timp ce fiecare utilizator WorkNotes își leagă separat contul GitHub, fără potrivirea adreselor de email, motivul pentru care implementarea curentă separă key ring-ul persistent de payload-urile SQL, de ce capturile GitHub nu pot produce un `INSERT` funcțional, diferența dintre Client secret și cheia privată/amprenta unei GitHub App și rotația obligatorie a unui secret expus.
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

Apoi, tot pe 2026-09-28, la cererea utilizatorului (prefixele CR și bug într-o tabelă de configurare, ținute în memorie sau pe sesiune):

- **Added** — tipurile de referință configurabile: prefixele recunoscute în toată aplicația sunt tipurile active din `dbo.ReferenceTypes` (implicit `CR` și `BUG`); un tip nou, de exemplu `TASK`, se adaugă fără cod nou, iar `task 12`, `TASK-12` sau `Task_12` devin referințe. `NoteReferenceParser`, `IReferenceTypeService` / `ReferenceTypeService`, `ReferenceTypeCache` și `IReferenceTypeRepository` ([ADR-003](docs/decisions/ADR-003-internal-references.md), deciziile 70–76).
- **Changed** — tipurile se țin în memorie pentru toată aplicația (nu pe sesiune) și se citesc din nou după cel mult 5 minute; o cerere folosește aceleași tipuri de la început la sfârșit. Toată citirea referințelor trece prin `NoteReferenceService`: linkurile din răspunsul salvării vin în `NoteReferenceResolution`. Constantele `NoteReferenceTypes` au fost eliminate.
- **Database** — `version_0.02/009_CreateReferenceTypes.sql` creează `dbo.ReferenceTypes` (`Code`, 1–10 litere ASCII mari, cheia primară; `IsActive`; `CreatedAtUtc`); `010_InsertReferenceTypes.sql` adaugă `CR` și `BUG`, active; `011_UpdateReferenceTypeKeys.sql` înlocuiește `CK_NoteReferences_ReferenceType` și `CK_WorkReferences_ReferenceType` cu chei externe către `ReferenceTypes` (fără cascadă). `012_RefreshNoteReferences.sql` citește din nou toate textele cu tipurile active și aduce la zi `NoteReferences`, `NoteReferenceTargets` și `WorkReferences`, cu listele lui `005`; se poate rula oricând și ține locul lui `005` după `008`. Entitatea nouă `ReferenceType` și comanda de scaffolding cu `--table dbo.ReferenceTypes`.

Apoi, pe 2026-09-29, la cererea utilizatorului (un sertar cu referințele notei în dreapta editorului; selecția textului din editor nu se vedea):

- **Added** — sertarul referințelor din editor, în dreapta textului: închis, o bandă îngustă cu numărul referințelor; deschis, fiecare referință a notei care este link, o singură dată, în ordinea din text (`CR 30080`), cu notele pe care le deschide. Click pe o referință le deschide pe toate în taburi, click pe o notă numai pe ea; sertarul este deschis sau închis în toate taburile, iar lista urmează fiecare salvare. Fără JavaScript, sertarul funcționează ca `details`, cu linkuri către `/?note={id}`; sub 700px trece sub text. Linkurile paragrafelor (`NoteReferenceLink`) poartă acum tipul și numărul referinței. Trei chei .resx noi (`Editor_References`, `Editor_ReferencesHelp`, `Editor_ReferencesEmpty`; 172 de chei).
- **Fixed** — selecția textului din editor: pe rândul activ nu se vedea (CodeMirror o desenează sub rânduri, iar fundalul rândului activ o acoperea), iar pe foaia salvie a articolelor abia se vedea. Cât timp există o selecție, rândul activ și paragraful descris își păstrează numai bara din stânga; selecția este verde pe foaia jurnalului (și în titlul lui, unde era galbenă pe galben) și galbenă pe foaia articolului; în forced-colors, un amestec deschis al culorii de sistem.

Apoi, tot pe 2026-09-29, la cererea utilizatorului (o listă mai simplă în sertar, antetul lui fără transparență; după `CR 30080` și un spațiu nu apărea niciun popup):

- **Added** — popup-ul referinței abia scrise: un spațiu, Tab, un rând nou sau un semn de punctuație (lista `wordEnds` din `note-references.js`) scris imediat după un număr întreabă serverul dacă textul rândului se termină cu o referință (`POST /?handler=ReferenceLookup`, `INoteService.LookUpReferenceAsync`, `INoteReferenceService.LookUpAsync`, `NoteReferenceRules.EndingReference`). Deasupra referinței, după designul primei sugestii: notele ei și butonul „Transformă în referință” (și Tab), care o face link imediat; sau „Referință inexistentă: nicio notă nu are … în titlu.”, închis singur după 4 secunde; iconul de căutare apare când răspunsul întârzie; un text care nu este referință nu arată nimic. Numai proprietarul notei întreabă (403 pentru cititori). Șase chei .resx noi (178 de chei). Bundle-ul CodeMirror exportă și `showTooltip` și `tooltips`.
- **Changed** — sertarul are un singur link pe referință, scris cum îl scrie textul prima dată (`CR_30080`), cu notele în tooltip, fără linkurile notelor cu tipul lor (`NoteReferenceLink` poartă textul linkului; `Editor_ReferencesHelp` schimbat). Un spațiu sau un semn de punctuație scris lângă un link îl păstrează; o literă sau o cifră lipită de el îl elimină până la salvare, ca până acum.
- **Fixed** — antetul sertarului era transparent (moștenea fundalul slotului elementului `details`), iar lista derulată trecea vizibil pe sub el; acum este opac, într-o nuanță mai închisă decât lista.

Apoi, tot pe 2026-09-29, la cererea utilizatorului (închiderea editorului întârzia uneori mult: reîncărca toată tabla; post-it-ul să se actualizeze la fiecare salvare, numai când se schimbă titlul sau începutul textului, plus data modificării):

- **Changed** — la fiecare salvare din editor, cardul notei de pe tabla din spate arată ce s-a salvat, fără ca tabla să fie citită din nou: titlul (câmpul de redenumire, titlul citit de cititoarele de ecran, etichetele Open și Delete) și previzualizarea numai când s-au schimbat, data ultimei modificări la fiecare salvare. O notă salvată pentru prima dată într-o lună nouă trece, cu cardul ei, în luna curentă, la locul dat de `Order` (numai atunci serverul citește tabla, pentru ordinea lunii). Răspunsul `SaveNote` are `card`, din `NoteSaveResult.Note` și `Month`, construite de `NoteService` din textul salvat (`NoteRules.PreviewOf`); salvarea citește nota prin `GetSummaryAsync`, fără toate paragrafele ei.
- **Changed** — închiderea editorului (Închide, Escape, click în afara ferestrei, × pe ultimul tab, Închide din forma minimizată) nu mai reîncarcă pagina cât timp niciun tab nu are modificări nesalvate: fereastra dispare, adresa devine a tablei (o intrare nouă în istoric; Back redeschide nota), iar focusul trece pe cardul notei active. Cu modificări nesalvate, închiderea încarcă tabla ca înainte, după avertizare; la fel după o salvare pe care tabla nu a putut-o arăta. Pe o tablă de 300 de note, închiderea durează circa 0,1 s, față de 0,6–0,8 s. `modal.js` trimite evenimentul `modal:close`, `note-editor.js` exportă `startEditor`, iar modulele comunică prin `note-editor:saved` și `note-editor:closed`.
- **Documentation** — regula de design din `AGENTS.md` (reordonările DOM ale tablei) cuprinde și mutarea cardului unei note salvate în luna curentă; deciziile 90–94.

Apoi, tot pe 2026-09-29, la cererea utilizatorului (în editor, schimbarea tipului unei note: din jurnal în articol sau invers):

- **Added** — proprietarul schimbă tipul notei din footerul ei din editor, cu același comutator ca pe cardul „Notă nouă” (iconurile sunt acum partialul comun `_NoteTypeIcon`, cu stilul lor în comutator): foaia, tabul și forma minimizată iau imediat tipul ales, nota are modificări nesalvate, iar Salvează sau Ctrl+S salvează tipul odată cu nota (`noteType` în corpul `SaveNote`); cardul de pe tablă își schimbă culoarea și numele tipului. O notă devenită jurnal primește ziua locală a creării ei, un articol nu are dată, iar schimbarea tipului actualizează ultima modificare. Un tip necunoscut primește 400 (`NoteSaveStatus.InvalidType`). Fără JavaScript și pentru cei care doar citesc, footerul arată în continuare numele tipului. Fără script SQL și fără chei .resx noi.

Apoi, pe 2026-09-30, la cererea utilizatorului (în editor, textul scris dispărea la Enter):

- **Fixed** — într-o notă ale cărei paragrafe erau stocate cu terminații `\r\n` (jurnalul `jurnal_2026` avea 497), editorul nu accepta nicio modificare: textul scris apărea, dar dispărea la Enter, iar consola arăta `RangeError: Position … is out of range for changeset`. CodeMirror numără o trecere la rând ca un caracter, iar pozițiile paragrafelor și ale linkurilor erau calculate pe textul cu `\r\n`, deci erau decalate (și linkurile apăreau mutate cu câte un caracter). `NoteService.GetDocumentAsync` citește acum paragrafele normalizate ca la salvare (`NoteRules.NormalizeBlockContent`), înainte ca linkurile să fie căutate în ele. Test nou: `AnOpenedNoteHasItsLineEndingsAsASaveStoresThem`.
- **Database** — `version_0.02/013_UpdateNoteBlockLineEndings.sql`, script de date: înlocuiește `\r\n` și `\r` cu `\n` în `dbo.NoteBlocks.Content`, fără să schimbe auditul paragrafelor; afișează notele atinse; cu `@Save = 0` nu salvează nimic; se poate rula din nou.

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
# Modificări nepublicate

### Added

- Zonă `/admin` cu autentificare și cookie separate, meniu propriu cu „Configurare” și administrarea configurației globale GitHub.
- Servicii stratificate pentru autentificarea administratorilor și configurarea GitHub; parola este hashuită, iar Client ID și Client secret sunt protejate reversibil cu Data Protection.

### Database

- `version_0.03/004_CreateAdministration.sql` creează idempotent `AdminUsers` și configurația globală `GitHubConfigurations`, fără seed sau secrete în SQL.

### Documentation

- Ghidul `docs/ADMIN_CONFIGURATION.md` documentează bootstrap-ul, cheile persistente și verificările de deploy.
