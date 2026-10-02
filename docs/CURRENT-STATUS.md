# Starea curentă

**Data ultimei actualizări:** 2026-09-29 · **Versiunea:** 0.03 (`v.0.03`, în dezvoltare; 0.02 integrată în `main`) · **Baza analizei:** `main` la commit-ul `504c01e` (merge-ul PR #3)

Statusul detaliat al fiecărei cerințe este în [REQUIREMENTS.md](REQUIREMENTS.md); planul, în [ROADMAP.md](ROADMAP.md).

## Implementat

- **Conturi** (ASP.NET Core Identity, Database First): înregistrare, autentificare, datele contului, schimbarea parolei, deconectare; politica de parolă și blocarea după 5 încercări.
- **Localizare** ro/en/pl pentru toate textele (168 de chei, aceleași în cele patru fișiere .resx; 178 cu PR #4).
- **Contexte și membri**: listare, adăugare, editare, ștergere în overlay; proprietarul gestionează membrii după e-mail.
- **Tabla**: o tablă pentru fiecare context, notele grupate pe luni după ultima modificare; post-it-uri pentru jurnale și articole, create, redenumite și șterse direct pe tablă; ordonarea prin drag-and-drop în aceeași lună (version_0.02).
- **Editorul** CodeMirror 6: paragrafe cu identitate și audit propriu, căutare, undo/redo, Ctrl+S, detectarea salvărilor concurente, taburi, minimizare.
- **Creare rapidă din referințe**: un `BUG`/`CR` fără destinație poate crea din popup un articol cu referința scrisă drept titlu și îl deschide într-un tab, păstrând nota curentă și evitând cererile duble din aceeași fereastră.
- **Persistența autentificării**: cookie-ul Identity are 14 zile și expirare glisantă; pe hosting, continuitatea după restart/deploy depinde de directorul persistent configurat prin `DataProtection:KeysPath`.
- **Mesaje de salvare**, sigla WN, designul „Hârtie & salvie”, funcționarea de bază fără JavaScript, versiunea în footer.

## În dezvoltare

- **Versiunea 0.03** — branch `main_task_03`, creată la cererea utilizatorului din 2026-09-29 pentru integrarea cu Git. Pagina `/Repositories` separă acum configurația tehnică OAuth de asocierea personală, cere emailul normalizat al identității autentificate și oferă conectare/reconectare pentru utilizator; izolarea în SQL rămâne pe cheia internă `UserId`. Emailul este inclus în noile cookie-uri de autentificare, iar pentru sesiunile existente pagina îl citește din profilul de cont, astfel încât butonul de conectare nu depinde de reautentificare. Primul pas al integrării: folderul `Scripts/version_0.03`, cu `000_UpdateDatabaseVersion.sql` (inserează `v.0.03`, neaplicat pe nicio bază), și documentele de versionare actualizate. Primul pas al integrării, conectarea contului GitHub prin OAuth ([ADR-004](decisions/ADR-004-github-oauth.md)): pagina `/Account/GitHub`, `GitHubConnectionService`, proiectul nou `WorkNotes.Integrations` (`GitHubOAuthClient`), `GitConnectionRepository` (tokenurile criptate cu Data Protection) și `001_CreateGitConnections.sql` (neaplicat pe nicio bază). Entitatea `GitConnection` și maparea ei au fost scrise manual, în forma generată de scaffolding, pentru că sesiunea nu avea SQL Server: scaffolding-ul trebuie rulat după aplicarea scriptului, ca verificare. Al doilea pas, importul repository-urilor: pagina `/Repositories` (toate repository-urile GitHub ale contului, cu câte o bifă), `GitRepositoryService`, `GitHubTokenService` (tokenul valid, comun), `GitRepositoryRepository` și `002_CreateGitRepositories.sql` (neaplicat; entitatea `GitRepository` scrisă tot manual, în forma scaffolding-ului). Al treilea pas, referința Git din editor: opțiunea „Referință Git” a popup-ului referinței abia scrise (repository importat, branch-uri care conțin referința), `NoteGitReferenceService`, `GitReferenceRules`, `NoteGitReferenceRepository`, `GitBranches` / `AddGitReference` / `RemoveGitReference` și `003_CreateGitReferences.sql` (aplicat numai pe o bază temporară de verificare, nu pe baza utilizatorului; entitățile `GitReference` și `NoteBlockGitReference` coincid cu ieșirea scaffolding-ului rulat pe acea bază, dar scaffolding-ul trebuie rulat și pe baza utilizatorului). Urmează citirea commit-urilor și pull request-urilor ([REQUIREMENTS.md](REQUIREMENTS.md#integrarea-cu-git-version_003)).
- **PR #4** — branch `main_task_02`, deschis pe 2026-09-25, neintegrat în `main` (care a fost adus în branch prin merge), cu commit-urile:
  - „Board: the next drag is no longer refused while a swap is being saved” — schimburile se aplică la `dragend` și se salvează pe rând;
  - „Editor and board: internal references between notes” — primul model al referințelor interne (legătura în text, `[[note:{id}|{număr}]]`), tabela `NoteReferences` (`Scripts/version_0.02/002_CreateNoteReferences.sql`), deciziile 32–41 și reguli noi în `AGENTS.md`; modelul este înlocuit (vezi mai jos);
  - „Notes: create-note-references command for the existing notes” — comanda de mentenanță `create-note-references` (primul model), deciziile 42–47; eliminată odată cu modelul;
  - „Data access: card previews number only the paragraphs of the notes read” și „Board: a note opens over the board without reloading the page” — previzualizarea cardurilor nu mai numerotează toate paragrafele din bază, iar o notă deschisă de pe tablă apare peste tabla din pagină (`?handler=NoteEditor`), fără reîncărcare; deciziile 48–49;
  - „Modal dialogs: Escape closes them in Firefox too” — `modal.js` tratează Escape la `keydown`, cu tasta prevenită, apoi navighează la adresa de închidere; decizia 50;
  - „Scripts: 003_InsertNoteReferences.sql fills NoteReferences from the existing text” — script SQL de date pentru tabela primului model; rămâne nemodificat;
  - referințele interne refăcute la cererea utilizatorului din 2026-09-28 ([ADR-003](decisions/ADR-003-internal-references.md), deciziile 51–58): referințele CR/bug scrise în paragrafe (`CR 30080`, `CR-30080`, `CR_30080`, `CR30080`, `bug_1234`…), destinația aflată din titluri (exact o notă a contextului vizibilă proprietarului paragrafului), relații pe paragraf în tabela `NoteReferences` recreată de `Scripts/version_0.02/004_ReplaceNoteReferences.sql` (care reindexează și conținutul existent și listează referințele fără destinație și pe cele ambigue), recalculare la salvare, la schimbarea titlului, la crearea și la ștergerea notelor, prin `INoteReferenceService`; editorul desenează linkurile din relații, fără să schimbe textul. Sugestiile din editor, comanda `create-note-references` și linkurile din previzualizarea cardurilor au fost eliminate.
  - referințele cu mai multe note, la cererea utilizatorului din 2026-09-28, după cazul `CR 27881` din jurnalul „CRs” (deciziile 59–64): o referință deschide toate notele contextului care au CR-ul sau bugul în titlu, în afară de nota paragrafului. Notele fiecărei referințe sunt în tabela nouă `NoteReferenceTargets` (legătură 1–M cu `NoteReferences`, care pierde coloana `TargetNoteId`), creată de `Scripts/version_0.02/005_CreateNoteReferenceTargets.sql`. Scriptul mută legăturile existente, reindexează conținutul și listează referințele fără notă, pe cele cu mai multe note și jurnalul „CRs”. Click sau Ctrl+Enter deschide toate notele, în taburi; fără JavaScript, fiecare notă are link. Ștergerea unei note nu mai recalculează referințele.
  - catalogul referințelor, la cererea utilizatorului din 2026-09-28 (deciziile 65–69): fiecare referință stocată, tipul și numărul, este o singură dată în tabela nouă `WorkReferences` (cheia unică tip + număr, ID propriu), creată de `Scripts/version_0.02/006_CreateWorkReferences.sql` și completată cu referințele existente de `007_InsertWorkReferences.sql`. `008_UpdateNoteReferencesWorkReferenceId.sql` adaugă în `NoteReferences` coloana obligatorie `WorkReferenceId`, cu ID-ul referinței lângă textul ei. Aplicația adaugă o referință în catalog prima dată când un paragraf o stochează; afișarea și comportamentul referințelor nu se schimbă.
  - tipurile de referință configurabile, la cererea utilizatorului din 2026-09-28 (deciziile 70–76): prefixele recunoscute în toată aplicația sunt tipurile active din tabela nouă `ReferenceTypes` (implicit `CR` și `BUG`, adăugate de `010_InsertReferenceTypes.sql`), ținute în memorie pentru toată aplicația și citite din nou după cel mult 5 minute. `009_CreateReferenceTypes.sql` creează tabela, `011_UpdateReferenceTypeKeys.sql` înlocuiește constrângerile `CHECK` ale tipului cu chei externe, iar `012_RefreshNoteReferences.sql` citește din nou toate textele cu tipurile active (după o schimbare a lor) și ține locul lui `005`.
  - sertarul referințelor și selecția din editor, la cererea utilizatorului din 2026-09-29 (deciziile 77–82): în dreapta textului, editorul are un sertar cu fiecare referință a notei care este link, o singură dată, cu notele pe care le deschide (click le deschide în taburi; lista urmează fiecare salvare; fără JavaScript, un `details` cu linkuri către pagina notelor). Selecția textului, care nu se vedea pe rândul activ și abia se vedea pe foaia salvie a articolelor, este acum vizibilă pe ambele foi. Schema și scripturile nu se schimbă.
  - popup-ul referinței abia scrise și un sertar mai simplu, la a doua cerere a utilizatorului din 2026-09-29 (deciziile 83–89): un spațiu, Tab, un rând nou sau un semn de punctuație scris imediat după un număr întreabă serverul dacă textul se termină cu o referință (`POST ?handler=ReferenceLookup`, `INoteReferenceService.LookUpAsync`). Deasupra referinței apare un popup: notele ei cu butonul „Transformă în referință” (și Tab), care o face link imediat, sau „Referință inexistentă”, închis singur după 4 secunde; pentru un text care nu este referință, nimic. Sertarul are acum un singur link pe referință, scris cum îl scrie textul prima dată, cu notele în tooltip, iar antetul lui este opac (era transparent: lista trecea vizibil pe sub el). Un spațiu sau un semn de punctuație scris lângă un link nu îl mai elimină. Bundle-ul CodeMirror exportă și `showTooltip` și `tooltips`. Schema și scripturile nu se schimbă.
  - tabla care urmează salvările din editor și închiderea editorului fără reîncărcare, la a treia cerere a utilizatorului din 2026-09-29 (deciziile 90–94): la fiecare salvare, cardul notei din spatele editorului arată titlul și previzualizarea când s-au schimbat și data ultimei modificări (răspunsul `SaveNote` are `card`, construit de `NoteService` din textul salvat, fără o citire nouă); o notă salvată pentru prima dată într-o lună nouă trece în luna curentă, în ordinea serverului; închiderea editorului fără modificări nesalvate nu mai reîncarcă pagina (circa 0,1 s în loc de 0,6–0,8 s pe o tablă de 300 de note). Regula de design din `AGENTS.md` a fost extinsă cu această mutare a cardului;
  - schimbarea tipului unei note din editor, la cererea utilizatorului din 2026-09-29 (deciziile 95–97): în footerul notei, proprietarul alege jurnal sau articol cu același comutator ca pe cardul „Notă nouă”; foaia, tabul și forma minimizată iau imediat tipul, iar salvarea îl stochează cu nota (o notă devenită jurnal primește ziua locală a creării, un articol nu are dată) și schimbă culoarea cardului de pe tablă; fără script SQL și fără chei .resx noi;
- **Branch-ul de documentare** `claude/worknotes-markdown-docs-6xyqw4` — această structură de documentație; nu modifică codul, schema sau funcționalitățile.

## Probleme cunoscute

Zona de administrare și configurația globală GitHub sunt implementate în cod și pregătite prin `version_0.03/004_CreateAdministration.sql`. Scriptul nu a fost aplicat de agent; administratorul inițial apare numai când operatorul furnizează secretul `AdminBootstrap:Password`. Detaliile sunt în [ADMIN_CONFIGURATION.md](ADMIN_CONFIGURATION.md).

Limitări documentate în version_0.01–0.02:

- [!] Pe `main`, în Firefox, Escape nu închide fereastra editorului: Firefox anulează navigarea pornită de `modal.js` din evenimentul `cancel` al tastei (verificat pe 2026-09-28 în Firefox 136); butonul Închide funcționează. Corecția este în PR #4.
- [!] Pe `main`, după primul schimb prin drag-and-drop, un al doilea drag început cât timp prima salvare este în curs este anulat fără niciun semn vizibil (`dragstart` refuzat cât timp `saving` este activ); corecția este în PR #4.
- [!] Pe `main`, în editor, selecția textului nu se vede pe rândul activ (fundalul lui acoperă stratul în care CodeMirror desenează selecția) și abia se vede pe foaia salvie a articolelor; în titlul unui jurnal este galbenă pe galben. Corecția este în PR #4.
- [!] Pe tablă, selecția textului de pe cardurile de articol (hârtia salvie #DDEADB) este verdele de selecție #CCE6DF, puțin vizibil (stilul calculat `::selection`, verificat pe 2026-09-29); PR #4 a corectat numai editorul.
- La reîncărcarea paginii editorului se redeschide doar tabul activ (adresa `/?note={id}`), nu toate taburile.
- Mutarea unui paragraf prin tăiere și lipire creează un paragraf nou (ID nou).
- Pe telefon, bara de taburi arată aproximativ un tab și jumătate; restul se derulează.
- Cu fonturi foarte late (de exemplu pe unele sisteme Linux), data modificării de pe card trece pe al doilea rând.
- Redenumirea pe loc nu mută cardul imediat; ordinea se actualizează la următoarea încărcare a tablei.
- PR #4: închiderea editorului nu mai reîncarcă tabla, deci modificările făcute între timp de alți membri sau în alte ferestre apar abia la următoarea încărcare a paginii (înainte, și la închiderea editorului); pe o pagină rămasă deschisă dintr-o lună anterioară, o salvare nu poate muta cardul în luna curentă, iar închiderea încarcă tabla.
- După un schimb pe tablă, un editor deschis pe aceeași notă în alt tab sau în altă fereastră primește conflict la următoarea salvare (fără să suprascrie ceva); editorul din aceeași pagină primește noua versiune.
- Ordonarea se face numai cu mouse-ul (drag-and-drop HTML5); nu există alternativă de la tastatură, iar pe ecranele tactile depinde de suportul browserului.
- Două note cu aceeași valoare `Order` (posibil numai prin inserări directe în SQL) nu își schimbă locurile prin drag-and-drop; tabla le ordonează după date.

Limitări ale referințelor interne din PR #4 ([ADR-003](decisions/ADR-003-internal-references.md#consecințe)):

- O referință nou scrisă devine link la salvare sau, mai devreme, din popup; textul scris într-un link, ca și o literă sau o cifră lipită de el, îl ascunde până la salvare. Sertarul arată o referință nouă abia după salvare, chiar făcută link din popup.
- Popup-ul întreabă serverul la fiecare cuvânt terminat după o cifră (câte o cerere de fiecare dată); un text care nu este referință nu arată nimic. Notele pe care le arată sunt cele din acel moment; salvarea le calculează din nou.
- Referințele fără nicio notă nu au semn în textul salvat (popup-ul spune că nu există numai când sunt scrise); `012_RefreshNoteReferences.sql`, rulat din nou (și cu `@Save = 0`), le listează (înainte de `008`, `005_CreateNoteReferenceTargets.sql`).
- O referință cu multe note deschide tot atâtea taburi; fără JavaScript, un link deschide o singură notă, iar celelalte au câte un link numerotat.
- Recalcularea după schimbarea unui titlu sau crearea unei note este o tranzacție separată de operație: două operații simultane pe aceeași referință sau o cerere întreruptă între ele pot lăsa o legătură învechită până la următoarea salvare ([DATABASE.md](DATABASE.md#concurență)).
- O schimbare în `ReferenceTypes` se vede în aplicație după cel mult 5 minute (sau la repornire); textele deja salvate o urmează la următoarea salvare a fiecărui paragraf sau după `012_RefreshNoteReferences.sql`. Până atunci, referințele stocate ale unui tip dezactivat nu mai sunt linkuri, dar rămân în tabele.
- Un tip de referință folosit nu se poate șterge (cheile externe îl păstrează), ci se dezactivează; un tip nefolosit, șters cât timp aplicația îl mai are în memorie, face ca salvarea unei referințe de acel tip să dea eroare până la următoarea citire a tipurilor.
- Catalogul `WorkReferences` nu scade: o referință rămâne în el, cu ID-ul ei, și când niciun paragraf nu o mai scrie (inclusiv după o salvare respinsă pentru care fusese adăugată).
- Schimbarea vizibilității, arhivarea și ieșirea unui membru din context nu recalculează legăturile (nu au interfață).
- Previzualizarea cardurilor afișează textul fără linkuri.
- Sertarul referințelor arată referințele textului salvat: o referință nou scrisă apare în el, ca și linkul din text, abia după salvare.
- După `004`, scripturile `002` și `003` nu se mai pot rula; după `005`, nici `004`; după `008`, nici `005`, al cărui loc îl ia `012`.
- Codul presupune că `005`–`011` au fost aplicate: fără `NoteReferenceTargets`, deschiderea și salvarea notelor dau eroare, fără `NoteReferences.WorkReferenceId`, salvarea lor, iar fără `ReferenceTypes`, orice citire a referințelor. Codul de dinaintea catalogului nu scrie `WorkReferenceId`, deci după `008` nu mai poate salva o referință nouă: scripturile `005`–`008` se aplică împreună, cu aplicația oprită, înaintea codului; `009`–`011` se aplică și ele înaintea codului, iar codul cu catalogul funcționează și cu ele.

Constatări din analiza din 2026-09-25 (codul nu a fost modificat):

- [!] Contextele create înainte de scriptul `005_CreateContextMembers.sql` nu au membri, deci nu sunt vizibile nimănui, iar numele lor rămân ocupate. TODO: Necesită clarificare.
- [!] Nu există pagini proprii pentru 404, 403 și erorile neașteptate; în afara Development erorile neașteptate primesc un răspuns ProblemDetails, nu o pagină HTML localizată.
- [!] Nu sunt configurate Content-Security-Policy, Data Protection pentru găzduire sau o listă `AllowedHosts` restrânsă ([SECURITY.md](SECURITY.md)).
- Nu există teste de integrare sau UI automate ([TESTING.md](TESTING.md)).
- Scripturile SQL conțin `USE [WorkNotes.db];`, deci presupun acest nume de bază în orice mediu.
- Numărul `003` lipsește din `Scripts/version_0.01` ([Scripts/README.md](../Scripts/README.md#convenții-de-denumire)).
- Textul paginii de bun venit („Aplicația este pregătită pentru dezvoltare.”) pare provizoriu.
- Comentarii învechite în fișiere existente: antetul `006_CreateNotes.sql` („A Journal is daily…”, înlocuit de 007; scriptul livrat nu se modifică) și primul comentariu din `notes-board.css` („Render items in creation-date order”, deși ordinea este `Order`, apoi ultima modificare).
- Nu există procedură de publicare, backup sau rollback și nici protecție pentru `main` ([DEPLOYMENT.md](DEPLOYMENT.md), [GIT-WORKFLOW.md](GIT-WORKFLOW.md#branch-uri-protejate)).

## Contradicții identificate

Contradicțiile nu au fost rezolvate prin alegerea arbitrară a unei variante; tratarea fiecăreia este descrisă mai jos.

| # | Contradicție | Surse | Tratare |
| --- | --- | --- | --- |
| 1 | Calea absolută a rădăcinii repository-ului: `E:\GitRepository\Vali\WorkNotes` (rădăcina Git, cu `Scripts` și `Solution`) față de `E:\GitRepository\Vali\WorkNotes\WorkNotes` | `Solution/AGENTS.md` și `Solution/README.md` vechi; cererea de documentare din 2026-09-25 | Documentația nouă folosește numai căi relative la rădăcina repository-ului, adevărate în ambele cazuri. TODO: Necesită clarificare — calea locală de referință. |
| 2 | Regula Git din PR #4 („commit-ul și push-ul se fac în branch-ul curent selectat… nu creați alt branch fără solicitare explicită”) față de fluxul nou „feature branch din `main` pentru fiecare sarcină” | `Solution/AGENTS.md` din PR #4; cererea de documentare | Pe `main` este în vigoare fluxul feature branch ([AGENTS.md](../AGENTS.md#git-și-limitele-sarcinii)); regula din PR #4 trebuie armonizată la integrarea lui. TODO: Necesită clarificare. |
| 3 | Convenția de denumire a branch-urilor: `main_task_00`, `main_task_01`, `main_task_002`, `main_task_02`, exemplul `feature/project-documentation`, branch-urile `claude/…` | istoricul Git; cererea de documentare | Descrise ca practică observată în [GIT-WORKFLOW.md](GIT-WORKFLOW.md#convenții-de-denumire). TODO: Necesită clarificare — convenția oficială. |
| 4 | Ghidul de design spunea că butonul „Notă nouă” este dezactivat până la implementarea modulului Notes și că notele „se vor afișa” pe tablă | `Solution/docs/design-system.md` (secțiunile Navigare și Verificare vizuală) față de cod | Codul este sursa pentru comportamentul implementat: butonul este dezactivat numai fără contexte, iar tabla este implementată ([UI-UX.md](UI-UX.md)). |
| 5 | README-ul vechi: structura soluției (lista doar fișierele versiunii inițiale), descrierea testelor (numai versiuni, rezultat gol, anulare, erori) și dreptul conexiunii („drept de citire pentru aplicație”, deși aplicația scrie conturi, contexte și note) | `Solution/README.md` față de cod | Actualizate după cod în [README.md](../README.md), [TESTING.md](TESTING.md) și [ARCHITECTURE.md](ARCHITECTURE.md). |
| 6 | Antetul `006_CreateNotes.sql` descrie un singur jurnal pe zi | scriptul 006 față de 007 și decizia #5 | Regula valabilă este cea din 007 (mai multe jurnale pe zi); scriptul livrat nu se modifică ([Scripts/README.md](../Scripts/README.md#ordinea-de-aplicare)). |
| 7 | Primul comentariu din `notes-board.css` vorbește de ordinea creării | CSS față de `NoteService` | Documentația descrie ordinea reală; comentariul se poate corecta într-o sarcină de cod. |
| 8 | Regula veche „aplicați scripturile asupra bazei autorizate pentru sarcină” față de cerința nouă „nu aplica scripturi sau migrări fără cerere explicită” | `Solution/AGENTS.md` față de cererea de documentare | Armonizate: scripturile se aplică numai la cerere explicită, pe baza indicată ([AGENTS.md](../AGENTS.md#ef-core-și-schema-sql-database-first)). |
| 9 | `Solution/AGENTS.md` declara că „se aplică întregului repository”, dar se afla în `Solution/`, deci nu acoperea `Scripts/` pentru agenții care citesc `AGENTS.md` pe foldere | amplasarea fișierului | Mutat în rădăcina repository-ului. |
| 10 | Structura cerută (`WorkNotes.Data/`, proiectele în rădăcină) față de structura reală (`Solution/WorkNotes.DataAccess/`, proiectele în `Solution/`) | cererea de documentare față de soluție | Adaptată la numele reale, cum cere documentarea: `CLAUDE.md` în `Solution/WorkNotes.Business`, `Solution/WorkNotes.DataAccess`, `Solution/WorkNotes.Web`. |

## Impactul asupra PR #4

PR #4 modifică `Solution/AGENTS.md`, `Solution/README.md`, `Solution/docs/design-system.md` și `Solution/docs/planning/02`–`06`, pe care branch-ul de documentare le-a mutat sau integrat. Oricare dintre cele două se integrează al doilea va avea conflicte (fișiere modificate într-o parte și eliminate în cealaltă). La rezolvare, modificările de documentație din PR #4 se portează în noua structură:

- [AGENTS.md](../AGENTS.md): regula drag-and-drop cu salvări pe rând, regulile referințelor interne (în `Solution/AGENTS.md`, după [ADR-003](decisions/ADR-003-internal-references.md)) și regula Git (armonizată cu contradicția 2);
- [DATABASE.md](DATABASE.md) și [Scripts/README.md](../Scripts/README.md): portate — tabelele `NoteReferences`, `NoteReferenceTargets`, `WorkReferences` și `ReferenceTypes` (modelul nou), scripturile `002`–`012`, `--table dbo.NoteReferences`, `--table dbo.NoteReferenceTargets`, `--table dbo.WorkReferences` și `--table dbo.ReferenceTypes` în comanda de scaffolding; marcajele „PR #4” se elimină la integrare;
- [decisions/README.md](decisions/README.md): deciziile 32–97 și [ADR-003](decisions/ADR-003-internal-references.md) sunt deja în jurnal; deciziile 33–38, 40–47 și 52 sunt marcate ca înlocuite;
- [ARCHITECTURE.md](ARCHITECTURE.md): `INoteReferenceService` / `NoteReferenceService`, `INoteReferenceRepository` (implementat de `NoteRepository`), fluxurile salvării și al recalculării, `note-references.js` (cu sertarul referințelor, `referenceDrawer`, și popup-ul referinței abia scrise, `referenceLookup`) și `WorkNotes.Web/Notes/NoteReferences.cs` (cu lista sertarului); deschiderea fără reîncărcare (handlerul `NoteEditor`, evenimentul `modal:open`, subinterogarea previzualizării limitată la paragrafele notelor citite);
- [SECURITY.md](SECURITY.md): legăturile respectă contextul și vizibilitatea proprietarului paragrafului, cititorul vede numai linkurile către notele pe care le poate vedea, iar recalcularea modifică legăturile paragrafelor altor membri ai contextului;
- [TESTING.md](TESTING.md): `NoteReferenceRulesTests`, `NoteReferenceServiceTests`, noile teste din `NoteServiceTests` și totalurile; verificările manuale ale linkurilor și ale scriptului `004`;
- fișierele `CLAUDE.md` din Web, Business și DataAccess: noile tipuri (`INoteReferenceService`, `INoteReferenceRepository`, `IReferenceTypeService`, `IReferenceTypeRepository`, `NoteReferenceParser`, `ReferenceTypeCache`, `NoteReference.cs`, `NoteReferenceTarget.cs`, `WorkReference.cs` și `ReferenceType.cs` generate prin scaffolding);
- [UI-UX.md](UI-UX.md), [REQUIREMENTS.md](REQUIREMENTS.md), [DOMAIN-MODEL.md](DOMAIN-MODEL.md): actualizate pentru modelul nou; statusurile trec din [~] în [x] la integrare;
- [ROADMAP.md](ROADMAP.md) și această pagină: lista „Referințe către această notă” și limitările de mai sus;
- [CHANGELOG.md](../CHANGELOG.md), [LOCALIZATION.md](LOCALIZATION.md) (178 de chei, cu `Notes_ReferenceTarget`, cheile sertarului `Editor_References*` și ale popup-ului `Editor_Reference*`) și [TESTING.md](TESTING.md) (noile totaluri).

## Informații mutate sau consolidate

| Document vechi | Locul nou |
| --- | --- |
| `Solution/AGENTS.md` | [AGENTS.md](../AGENTS.md) în rădăcină, cu toate regulile valabile păstrate și regulile noi cerute explicit (feature branch, scripturi aplicate numai la cerere, scripturi livrate nemodificabile, întreținerea documentației) |
| `Solution/README.md` — prezentare, tehnologii, structură, conexiune, build, pornire | [README.md](../README.md) |
| `Solution/README.md` — stadiu | [REQUIREMENTS.md](REQUIREMENTS.md) și această pagină |
| `Solution/README.md` — design, mesaje de salvare, editorul, cardurile | [UI-UX.md](UI-UX.md), [ADR-002](decisions/ADR-002-note-editor.md) |
| `Solution/README.md` — localizare | [LOCALIZATION.md](LOCALIZATION.md) |
| `Solution/README.md` — contexte, note, ordinea post-it-urilor, paragrafele | [DOMAIN-MODEL.md](DOMAIN-MODEL.md), [DATABASE.md](DATABASE.md), [UI-UX.md](UI-UX.md) |
| `Solution/README.md` — conturi și schema Identity | [SECURITY.md](SECURITY.md), [DATABASE.md](DATABASE.md) |
| `Solution/README.md` — arhitectură și fluxuri | [ARCHITECTURE.md](ARCHITECTURE.md) |
| `Solution/README.md` — pregătirea bazei, lista scripturilor, comenzile `sqlcmd` | [Scripts/README.md](../Scripts/README.md) |
| `Solution/README.md` — actualizarea modelului EF (scaffolding) | [DATABASE.md](DATABASE.md#strategia-ef-core) |
| `Solution/README.md` — regula versiunii curente | [VERSIONING.md](VERSIONING.md) |
| `Solution/README.md` — găzduire (HTTPS, Data Protection) | [DEPLOYMENT.md](DEPLOYMENT.md), [SECURITY.md](SECURITY.md) |
| `Solution/docs/design-system.md` | [UI-UX.md](UI-UX.md), integral; verificarea vizuală în [TESTING.md](TESTING.md#verificări-manuale-obligatorii) |
| `Solution/docs/planning/README.md` | indexul din [README.md](../README.md#documentație) și regula de întreținere din [AGENTS.md](../AGENTS.md#întreținerea-documentației) |
| `Solution/docs/planning/01-viziune-si-domeniu.md` | [PROJECT-CONTEXT.md](PROJECT-CONTEXT.md), [DOMAIN-MODEL.md](DOMAIN-MODEL.md) |
| `Solution/docs/planning/02-model-de-date.md` | [DATABASE.md](DATABASE.md), [DOMAIN-MODEL.md](DOMAIN-MODEL.md), [Scripts/README.md](../Scripts/README.md) |
| `Solution/docs/planning/03-arhitectura.md` | [ARCHITECTURE.md](ARCHITECTURE.md) |
| `Solution/docs/planning/04-functionalitati-realizate.md` | [CHANGELOG.md](../CHANGELOG.md), [REQUIREMENTS.md](REQUIREMENTS.md) |
| `Solution/docs/planning/05-decizii.md` | [decisions/README.md](decisions/README.md) (jurnalul complet, deciziile 1–31), [ADR-001](decisions/ADR-001-solution-architecture.md), [ADR-002](decisions/ADR-002-note-editor.md) |
| `Solution/docs/planning/06-backlog.md` | [ROADMAP.md](ROADMAP.md), limitările de pe această pagină, [REQUIREMENTS.md](REQUIREMENTS.md) |

Fișierele vechi au fost eliminate după integrare, ca să nu existe două surse pentru aceleași reguli; conținutul lor rămâne în istoricul Git.

## Ultimul build și ultimele teste

### 2026-09-29 — versiunea 0.03, referința Git din editor (branch `main_task_03`, sesiune cloud Linux)

| Verificare | Rezultat |
| --- | --- |
| `dotnet build WorkNotes.sln --no-restore` | reușit, fără avertismente |
| `dotnet test WorkNotes.sln --no-build --no-restore` | 450 de teste, toate trecute (93 noi: `GitReferenceRulesTests` 47, `NoteGitReferenceServiceTests` 46) |
| `tools/Test-Resources.ps1` (PowerShell 7 instalat ca instrument .NET) | `PASS: 241 keys` |
| SQL Server 2022 într-un container temporar, cu o bază creată pentru verificare | scripturile `version_0.01`–`003` aplicate (`003` de două ori); `sqlcmd` cere `-I` |
| Scaffolding pe acea bază | entitățile și contextul coincid cu cele din repository |
| Aplicația pornită pe acea bază, cu un GitHub simulat și Chromium | fluxul popup → branch → legătură → sertar → eliminare, refuzurile serverului, 320×640 |
| Verificările arhitecturale | niciun rezultat |
| Neefectuate | baza și contul GitHub reale ale utilizatorului (nici `003`, nici scaffolding-ul pe baza lui); GitHub real (numai un server simulat); Windows/forced-colors; Firefox/Safari |

### 2026-09-29 — versiunea 0.03, importul repository-urilor (branch `main_task_03`, sesiune cloud Linux)

| Verificare | Rezultat |
| --- | --- |
| `dotnet build WorkNotes.sln --no-restore` | reușit, fără avertismente |
| `dotnet test WorkNotes.sln --no-build --no-restore` | 357 de teste, toate trecute (37 noi: `GitRepositoryRulesTests`, `GitRepositoryServiceTests`) |
| `tools/Test-Resources.ps1` | `PASS: 221 keys` |
| Lista repository-urilor din clientul HTTP, cu un handler simulat | 3 pagini (242), limita de 1000 (trunchiată), 502 și 401 |
| Pornirea aplicației (Development) | DI validat; `/Repositories` redirecționează la autentificare |
| Verificările arhitecturale | niciun rezultat |
| Neefectuate | `002_CreateGitRepositories.sql` și scaffolding-ul (fără SQL Server); lista reală de la GitHub; pagina în browser |

### 2026-09-29 — versiunea 0.03, conectarea GitHub (branch `main_task_03`, sesiune cloud Linux)

| Verificare | Rezultat |
| --- | --- |
| `dotnet build WorkNotes.sln` (SDK 10.0.112) | reușit, fără avertismente |
| `dotnet test WorkNotes.sln --no-build` | 320 de teste, toate trecute (65 noi: `GitAuthorizationRulesTests`, `GitHubConnectionServiceTests`) |
| `tools/Test-Resources.ps1` (PowerShell 7.6, instalat ca dotnet tool) | `PASS: 202 keys` |
| Clientul HTTP GitHub, cu un handler simulat | cererile și răspunsurile verificate (vezi [TESTING.md](TESTING.md)) |
| Pornirea aplicației (Development) | DI validat; `/Account/GitHub` și callback-ul redirecționează la autentificare; paginile care citesc baza dau eroare, fără SQL Server |
| Verificările arhitecturale din [ARCHITECTURE.md](ARCHITECTURE.md#verificarea-regulilor-arhitecturale) | niciun rezultat (și cu `--untracked`, pentru fișierele noi) |
| Neefectuate | aplicarea `001_CreateGitConnections.sql` și scaffolding-ul (fără SQL Server); fluxul real cu GitHub (fără aplicație GitHub înregistrată și fără `ClientId` / `ClientSecret`); pagina în browser, în cele trei limbi și la 320px |

Rulate pe 2026-09-25, în sesiunea cloud de documentare (Linux, .NET SDK 10.0.112, runtime 10.0.12), din folderul `Solution`, înainte și după modificările de documentație:

| Verificare | Rezultat |
| --- | --- |
| `dotnet tool restore` | reușit (`dotnet-ef` 10.0.12) |
| `dotnet restore WorkNotes.sln` | reușit |
| `dotnet build WorkNotes.sln --no-restore` | reușit, 0 avertismente, 0 erori |
| `dotnet test WorkNotes.sln --no-build --no-restore` | 98 de teste trecute, 0 eșuate, 0 omise |
| `dotnet publish WorkNotes.Web -c Release` (control) | reușit; niciun fișier `.md` în output |
| Verificările arhitecturale din [ARCHITECTURE.md](ARCHITECTURE.md#verificarea-regulilor-arhitecturale) | niciun rezultat (regulile sunt respectate) |
| `tools/Test-Resources.ps1` | neefectuat: PowerShell nu este disponibil în sesiune; o verificare echivalentă ad-hoc (aceleași chei și parametri în cele patru fișiere, fallback românesc identic, fără chei lipsă sau neutilizate) a trecut pentru 168 de chei |
| Aplicația și scripturile SQL | neverificate: sesiunea nu are SQL Server; nu s-a aplicat niciun script |

Pe 2026-09-28, pentru referințele interne refăcute din PR #4 (sesiune cloud Linux, .NET SDK 10.0.112, fără SQL Server):

| Verificare | Rezultat |
| --- | --- |
| `dotnet tool restore`, `dotnet restore WorkNotes.sln` | reușite |
| `dotnet build WorkNotes.sln --no-restore` | reușit, 0 avertismente, 0 erori |
| `dotnet test WorkNotes.sln --no-build --no-restore` | 196 de teste trecute, 0 eșuate, 0 omise |
| `tools/Test-Resources.ps1` (PowerShell 7 ca instrument .NET global) | `PASS`: 169 de chei, aceleași în cele patru fișiere |
| Verificările arhitecturale din [ARCHITECTURE.md](ARCHITECTURE.md#verificarea-regulilor-arhitecturale) | niciun rezultat, inclusiv pentru fișierele noi |
| Paginile reale, pe o gazdă de test cu depozit în memorie în locul SQL Server (Chromium și Firefox 136, cu mouse și tastatură reale în Firefox) | linkurile desenate din relații, click și Ctrl+Enter în taburi (tab nou, tab existent, editor minimizat, modificări nesalvate păstrate), editarea și salvarea, redenumirea, crearea și ștergerea notelor, ștergerea unui paragraf, afișarea fără JavaScript și textul HTML afișat ca text; deschiderea de pe tablă, Escape și drag-and-drop funcționează ca înainte |
| `004_ReplaceNoteReferences.sql` | fără erori de sintaxă (parserul Microsoft ScriptDom, gramaticile SQL Server 2016 și 2022, inclusiv instrucțiunile din `EXEC`); logica de recunoaștere și de transformare a legăturilor vechi, reprodusă în C#, dă aceleași rezultate ca aplicația pe 400 000 de texte generate |
| Interogările EF noi | SQL-ul generat, verificat offline (`ToQueryString`) |
| Aplicația pe SQL Server, scaffolding-ul și aplicarea `004` | neverificate: sesiunea nu are SQL Server; scriptul nu a fost aplicat pe nicio bază |

Tot pe 2026-09-28, pentru referințele cu mai multe note (`NoteReferenceTargets`, `005_CreateNoteReferenceTargets.sql`), în aceeași sesiune:

| Verificare | Rezultat |
| --- | --- |
| `dotnet tool restore`, `dotnet restore WorkNotes.sln` | reușite |
| `dotnet build WorkNotes.sln --no-restore` | reușit, 0 avertismente, 0 erori |
| `dotnet test WorkNotes.sln --no-build --no-restore` | 197 de teste trecute, 0 eșuate, 0 omise |
| `tools/Test-Resources.ps1` | `PASS`: 169 de chei (nicio cheie nouă) |
| Verificările arhitecturale din [ARCHITECTURE.md](ARCHITECTURE.md#verificarea-regulilor-arhitecturale) | niciun rezultat |
| Paginile reale, pe gazda de test cu depozit în memorie (Chromium; Firefox 136 cu mouse și tastatură reale) | o referință cu două note are un link cu tooltip pe două rânduri; click și Ctrl+Enter deschid ambele note în taburi și o arată pe prima (și când sunt deja deschise, după închiderea uneia și din editorul minimizat), cu modificările nesalvate păstrate; salvarea, redenumirea, crearea și ștergerea notelor actualizează notele referinței; afișarea fără JavaScript are câte un link pentru fiecare notă |
| `005_CreateNoteReferenceTargets.sql` | fără erori de sintaxă (ScriptDom, gramaticile SQL Server 2016 și 2022, inclusiv cele 11 instrucțiuni din `EXEC`); recunoașterea este cea din `004`; regula notelor, reprodusă în C#, dă aceleași legături ca `NoteReferenceService` pe 3000 de table generate (1924 de referințe, 129 cu mai multe note) |
| Interogările EF noi și ștergerea unei note | SQL-ul generat, verificat offline (`ToQueryString`) |
| Aplicația pe SQL Server, scaffolding-ul și aplicarea `005` | neverificate: sesiunea nu are SQL Server; scriptul nu a fost aplicat pe nicio bază |

Tot pe 2026-09-28, pentru catalogul referințelor (`WorkReferences`, `006`–`008`), în aceeași sesiune:

| Verificare | Rezultat |
| --- | --- |
| `dotnet tool restore`, `dotnet restore WorkNotes.sln` | reușite |
| `dotnet build WorkNotes.sln --no-restore` | reușit, 0 avertismente, 0 erori |
| `dotnet test WorkNotes.sln --no-build --no-restore` | 197 de teste trecute, 0 eșuate, 0 omise (nicio regulă Business nouă: catalogul este în DataAccess și în scripturi) |
| `tools/Test-Resources.ps1` | `PASS`: 169 de chei (nicio cheie nouă) |
| Verificările arhitecturale din [ARCHITECTURE.md](ARCHITECTURE.md#verificarea-regulilor-arhitecturale) | niciun rezultat |
| `006_CreateWorkReferences.sql`, `007_InsertWorkReferences.sql`, `008_UpdateNoteReferencesWorkReferenceId.sql` | fără erori de sintaxă (ScriptDom, gramaticile SQL Server 2016 și 2022, inclusiv instrucțiunile din `EXEC` și `sp_executesql`) |
| Căutarea în catalog și adăugarea unei referințe | SQL-ul generat de EF, verificat offline (`ToQueryString`); instrucțiunea `INSERT … WHERE NOT EXISTS`, cu parametrii ei, fără erori de sintaxă |
| Aplicația pe SQL Server, scaffolding-ul și aplicarea `006`–`008` | neverificate: sesiunea nu are SQL Server; scripturile nu au fost aplicate pe nicio bază |

Tot pe 2026-09-28, pentru tipurile de referință configurabile (`ReferenceTypes`, `009`–`012`), în aceeași sesiune:

| Verificare | Rezultat |
| --- | --- |
| `dotnet tool restore`, `dotnet restore WorkNotes.sln` | reușite |
| `dotnet build WorkNotes.sln --no-restore` | reușit, 0 avertismente, 0 erori |
| `dotnet test WorkNotes.sln --no-build --no-restore` | 220 de teste trecute, 0 eșuate, 0 omise (noile `NoteReferenceParserTests` și `ReferenceTypeServiceTests`) |
| `tools/Test-Resources.ps1` | `PASS`: 169 de chei (nicio cheie nouă) |
| Verificările arhitecturale din [ARCHITECTURE.md](ARCHITECTURE.md#verificarea-regulilor-arhitecturale) | niciun rezultat |
| Paginile reale, pe gazda de test cu depozit în memorie (Chromium; Firefox 136 cu mouse și tastatură reale) | testele anterioare ale referințelor trec neschimbate cu `CR` și `BUG`; cu tipul `TASK` configurat, `task 12` și `TASK_12` sunt linkuri către „TASK-12 migrare” (și fără JavaScript), click deschide nota, un `Task-12` nou scris devine link la salvare; fără `TASK`, `task 12` rămâne text; tipurile au fost citite o singură dată pe toată durata testelor |
| `009_CreateReferenceTypes.sql`–`012_RefreshNoteReferences.sql` | fără erori de sintaxă (ScriptDom, gramaticile SQL Server 2016 și 2022, inclusiv instrucțiunea din `EXEC`); recunoașterea din `012`, reprodusă în C#, dă aceleași rezultate ca `NoteReferenceParser` pe 800 000 de texte, cu seturi de tipuri generate aleatoriu (63 146 de referințe); regula notelor, pe 3000 de table cu `CR`, `BUG` și `TASK`, aceleași legături ca `NoteReferenceService` |
| Modelul EF | construit fără avertismente; interogarea tipurilor active, verificată offline (`ToQueryString`) |
| Aplicația pe SQL Server, scaffolding-ul și aplicarea `009`–`012` | neverificate: sesiunea nu are SQL Server; scripturile nu au fost aplicate pe nicio bază |

Pe 2026-09-29, pentru sertarul referințelor și selecția din editor, în aceeași sesiune:

| Verificare | Rezultat |
| --- | --- |
| `dotnet tool restore`, `dotnet restore WorkNotes.sln` | reușite |
| `dotnet build WorkNotes.sln --no-restore` (și `--no-incremental`) | reușit, 0 avertismente, 0 erori |
| `dotnet test WorkNotes.sln --no-build --no-restore` | 220 de teste trecute, 0 eșuate, 0 omise (linkurile poartă acum tipul și numărul referinței) |
| `tools/Test-Resources.ps1` | `PASS`: 172 de chei (`Editor_References`, `Editor_ReferencesHelp`, `Editor_ReferencesEmpty`) |
| Verificările arhitecturale din [ARCHITECTURE.md](ARCHITECTURE.md#verificarea-regulilor-arhitecturale), `git diff --check` | niciun rezultat |
| Paginile reale, pe gazda de test cu depozit în memorie (Chromium) | sertarul închis este o bandă de 48px lângă text, cu numărul referințelor; deschis, o coloană lângă text; fiecare referință o singură dată, în ordinea din text, cu toate notele ei (o referință fără notă vizibilă lipsește); click pe o referință deschide toate notele ei în taburi, click pe o notă numai pe ea; starea deschis/închis este aceeași în toate taburile; după salvare lista urmează textul salvat; Enter pe antet îl deschide; fără JavaScript sertarul se deschide nativ, linkurile duc la `/?note={id}`, iar o notă fără linkuri arată mesajul gol; la 390px sertarul este sub text, fără derulare laterală; nicio eroare în pagină |
| Selecția (Chromium, stilurile calculate și pixelii capturilor) | pe rândul activ, cuvântul selectat este verde #CCE6DF pe foaia jurnalului și galben #FFF0B7 pe foaia articolului (înainte: #E7F1EC, culoarea rândului activ, adică invizibil); rândul activ nu mai are fundal cât timp există o selecție; titlul jurnalului are selecția verde |
| Firefox 136, cu mouse și tastatură reale și culorile citite de pe ecran | aceleași verificări pentru sertar (click, taburi, Enter și Space pe antet) și pentru selecție (dublu-click pe un cuvânt al rândului activ și pe titlu: verde pe jurnal, galben pe articol) |
| Forced-colors, emulat în Chromium | cuvântul selectat se vede: browserul desenează peste text selecția în culorile sistemului, iar stratul CodeMirror are amestecul deschis al culorii `Highlight`; sertarul și numărul lui au bordură. Modul real din Windows (contrast ridicat) nu a fost verificat |
| Regresie | testele anterioare ale referințelor (mai multe note, tipuri configurabile, cu și fără `TASK`) trec neschimbate |
| Aplicația pe SQL Server, browserul utilizatorului | neverificate: sesiunea nu are SQL Server; nicio modificare de schemă sau de script |

Tot pe 2026-09-29, pentru popup-ul referinței abia scrise și sertarul simplificat, în aceeași sesiune:

| Verificare | Rezultat |
| --- | --- |
| `dotnet tool restore`, `dotnet restore WorkNotes.sln` | reușite |
| `dotnet build WorkNotes.sln --no-restore --no-incremental` | reușit, 0 avertismente, 0 erori |
| `dotnet test WorkNotes.sln --no-build --no-restore` | 245 de teste trecute, 0 eșuate, 0 omise (25 noi: referința cu care se termină un text, căutarea ei și dreptul proprietarului) |
| `tools/Test-Resources.ps1` | `PASS`: 178 de chei (6 noi pentru popup, `Editor_ReferencesHelp` schimbat) |
| Verificările arhitecturale din [ARCHITECTURE.md](ARCHITECTURE.md#verificarea-regulilor-arhitecturale), `git diff --check` | niciun rezultat |
| Bundle-ul CodeMirror | reconstruit din `Solution/tools/codemirror` (`npm run build`, cu `node_modules` din `package-lock.json`): fără schimbări de intrare, identic octet cu octet cu cel versionat; apoi cu `showTooltip` și `tooltips` exportate; `THIRD-PARTY-NOTICES.txt` neschimbat |
| Paginile reale, pe gazda de test cu depozit în memorie (Chromium) | sertarul: un link pe referință, scris ca prima apariție (`CR-30081`, `bug_512`, `cr30082`), notele în tooltip, antetul opac, click deschide toate notele; popup-ul: după `CR 30080` și spațiu, nota ei, butonul o face link imediat, focusul rămâne în text; după `CR 30083` și virgulă, „Referință inexistentă”, închis după circa 4 secunde; o dată (`29.09.2026`) nu arată nimic; Tab caută, al doilea Tab face link, al treilea mută focusul; Escape închide numai popup-ul; două note, fiecare cu culoarea hârtiei ei; închiderea la alt rând și la click în text; Enter după număr; un răspuns lent arată căutarea după 250 ms; spațiul după un link îl păstrează și nu întreabă nimic, o literă îl elimină; salvarea păstrează linkurile; o notă a altcuiva nu are căutare (403), o notă nevăzută 404, o cerere fără token 400; la 390px popup-ul rămâne la 16px de margini; fără JavaScript sertarul are un link pe referință; nicio eroare în pagină |
| Firefox 136, cu mouse și tastatură reale | aceleași cazuri ale popup-ului (spațiu, virgulă, Tab, punct, Escape, Enter, o dată) și sertarul |
| Regresie | testele anterioare ale referințelor și ale selecției trec |
| Aplicația pe SQL Server, browserul utilizatorului | neverificate: sesiunea nu are SQL Server; nicio modificare de schemă sau de script |

Tot pe 2026-09-29, pentru tabla care urmează salvările din editor și închiderea editorului fără reîncărcare, în aceeași sesiune:

| Verificare | Rezultat |
| --- | --- |
| `dotnet tool restore`, `dotnet restore WorkNotes.sln` | reușite |
| `dotnet build WorkNotes.sln --no-restore` | reușit, 0 avertismente, 0 erori |
| `dotnet test WorkNotes.sln --no-build --no-restore` | 249 de teste trecute, 0 eșuate, 0 omise (4 noi: cardul notei salvate, data unei salvări fără schimbări, luna în care trece nota, previzualizarea din paragrafe întregi) |
| `tools/Test-Resources.ps1` | `PASS`: 178 de chei (nicio cheie nouă) |
| Verificările arhitecturale din [ARCHITECTURE.md](ARCHITECTURE.md#verificarea-regulilor-arhitecturale), `git diff --check` | niciun rezultat |
| Paginile reale, pe gazda de test cu depozit în memorie (Chromium) | după salvare, cardul din spate are titlul nou (câmpul, titlul citit, etichetele Open și Delete), previzualizarea primelor paragrafe și data; o modificare după primele trei paragrafe schimbă numai data (observat cu `MutationObserver`); o notă din luna trecută și singura notă de acum două luni trec în luna curentă, la locul dat de `Order`, iar luna rămasă goală se ascunde; Închide, Escape, × pe ultimul tab, Închide din forma minimizată și închiderea cu două taburi nu reîncarcă pagina, iar focusul trece pe cardul notei active; editorul se deschide din nou și salvează (și după o fereastră randată de pagină, `/?note={id}`); Back încarcă nota; redenumirea și schimbul prin drag-and-drop funcționează după mutare; cu modificări nesalvate, Închide întreabă (rămânerea păstrează editorul, plecarea încarcă tabla); o salvare al cărei card lipsește din pagină sau a cărei lună nu este pe pagină face închiderea să încarce tabla; fără JavaScript, Închide este un link către tablă, iar o notă fără text nu arată previzualizare |
| Firefox 136, cu mouse și tastatură reale | salvarea se vede pe cardul din spate, cardul unei note din luna trecută trece în luna curentă, Închide și Escape nu reîncarcă pagina, focusul trece pe card; cu modificări nesalvate, Închide încarcă tabla |
| Durata închiderii (Chromium, tabla de 300 de note, fără SQL Server) | 80–110 ms de la click până la al doilea cadru desenat, cu sau fără o salvare înainte; înainte, 0,6–0,8 s (reîncărcarea tablei) |
| Regresie | testele anterioare (referințele cu mai multe note, tipurile configurabile cu și fără `TASK`, sertarul, popup-ul, Escape în editor și pe celelalte ferestre) trec |
| Aplicația pe SQL Server, browserul utilizatorului | neverificate: sesiunea nu are SQL Server; nicio modificare de schemă sau de script |

Tot pe 2026-09-29, pentru schimbarea tipului unei note din editor, în aceeași sesiune:

| Verificare | Rezultat |
| --- | --- |
| `dotnet tool restore`, `dotnet restore WorkNotes.sln` | reușite |
| `dotnet build WorkNotes.sln --no-restore` | reușit, 0 avertismente, 0 erori |
| `dotnet test WorkNotes.sln --no-build --no-restore` | 256 de teste trecute, 0 eșuate, 0 omise (7 noi: articolul devenit jurnal al zilei creării, în fusul orar al aplicației; jurnalul devenit articol, fără dată; jurnalul care rămâne jurnal își păstrează data; tipurile necunoscute) |
| `tools/Test-Resources.ps1` | `PASS`: 178 de chei (nicio cheie nouă) |
| Verificările arhitecturale din [ARCHITECTURE.md](ARCHITECTURE.md#verificarea-regulilor-arhitecturale), `git diff --check` | niciun rezultat (și pentru partialul nou) |
| Paginile reale, pe gazda de test cu depozit în memorie (Chromium) | comutatorul apare în footer în locul numelui tipului, cu tipul notei ales; Articol face imediat foaia salvie, punctul tabului și forma minimizată ale unui articol și lasă nota nesalvată; salvarea trimite tipul, iar cardul de pe tablă devine articol (culoarea, numele citit); săgețile schimbă alegerea, iar revenirea la tipul salvat nu lasă nimic nesalvat; închiderea tabului și a ferestrei întreabă, ca pentru orice modificare nesalvată; două taburi își păstrează fiecare tipul; după reîncărcare, tipul salvat; un tip necunoscut primește 400, cu mesaj localizat; o notă doar pentru citire nu are comutator (iar salvarea ei de către un cititor primește 403); cardul „Notă nouă” are aceleași iconuri și își schimbă culoarea; fără JavaScript, numele tipului în locul comutatorului; la 320px, fără derulare laterală |
| Firefox 136, cu mouse și tastatură reale | click pe Articol, Ctrl+S, cardul devine articol; săgeata stânga, înapoi la jurnal, Ctrl+S; Escape închide fără reîncărcare |
| Forced-colors, emulat în Chromium | opțiunea aleasă are sublinierea în culoarea sistemului, iconurile au conturul textului, focusul se vede |
| Regresie | testele anterioare (tabla care urmează salvările, inclusiv cazul fără lună, referințele, sertarul, popup-ul, Escape) trec |
| Aplicația pe SQL Server, browserul utilizatorului | neverificate: sesiunea nu are SQL Server; nicio modificare de schemă sau de script |

## Următorii pași

1. Review-ul branch-ului de documentare și, la cerere explicită, pull request-ul lui.
2. Stabilirea ordinii de integrare între acest branch și PR #4 și portarea modificărilor de documentație din PR #4 ([lista de mai sus](#impactul-asupra-pr-4)).
3. Clarificarea punctelor marcate „TODO: Necesită clarificare”, în primul rând: calea locală de referință, convenția branch-urilor și regula Git din PR #4, mediile Test/Production, backup-ul și rollback-ul, contextele fără membri, pagina de bun venit.
4. Pașii următori ai produsului, în ordinea din [ROADMAP.md](ROADMAP.md#etapele-următoare), numai la cerere explicită.

### 2026-09-30 — configurația OAuth GitHub din baza de date

Fluxurile utilizatorilor consumă configurația globală administrată în `/admin/configuration`; lipsa ori imposibilitatea decriptării înseamnă „neconfigurat”, iar erorile SQL se propagă. Operațiile următoare unei salvări administrative citesc valorile noi fără restart.
