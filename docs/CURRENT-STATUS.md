# Starea curentă

**Data ultimei actualizări:** 2026-09-28 · **Versiunea:** 0.02 (`v.0.02`) · **Baza analizei:** `main` la commit-ul `504c01e` (merge-ul PR #3)

Statusul detaliat al fiecărei cerințe este în [REQUIREMENTS.md](REQUIREMENTS.md); planul, în [ROADMAP.md](ROADMAP.md).

## Implementat

- **Conturi** (ASP.NET Core Identity, Database First): înregistrare, autentificare, datele contului, schimbarea parolei, deconectare; politica de parolă și blocarea după 5 încercări.
- **Localizare** ro/en/pl pentru toate textele (168 de chei, aceleași în cele patru fișiere .resx; 169 cu PR #4).
- **Contexte și membri**: listare, adăugare, editare, ștergere în overlay; proprietarul gestionează membrii după e-mail.
- **Tabla**: o tablă pentru fiecare context, notele grupate pe luni după ultima modificare; post-it-uri pentru jurnale și articole, create, redenumite și șterse direct pe tablă; ordonarea prin drag-and-drop în aceeași lună (version_0.02).
- **Editorul** CodeMirror 6: paragrafe cu identitate și audit propriu, căutare, undo/redo, Ctrl+S, detectarea salvărilor concurente, taburi, minimizare.
- **Mesaje de salvare**, sigla WN, designul „Hârtie & salvie”, funcționarea de bază fără JavaScript, versiunea în footer.

## În dezvoltare

- **PR #4** — branch `main_task_02`, deschis pe 2026-09-25, neintegrat în `main` (care a fost adus în branch prin merge), cu commit-urile:
  - „Board: the next drag is no longer refused while a swap is being saved” — schimburile se aplică la `dragend` și se salvează pe rând;
  - „Editor and board: internal references between notes” — primul model al referințelor interne (legătura în text, `[[note:{id}|{număr}]]`), tabela `NoteReferences` (`Scripts/version_0.02/002_CreateNoteReferences.sql`), deciziile 32–41 și reguli noi în `AGENTS.md`; modelul este înlocuit (vezi mai jos);
  - „Notes: create-note-references command for the existing notes” — comanda de mentenanță `create-note-references` (primul model), deciziile 42–47; eliminată odată cu modelul;
  - „Data access: card previews number only the paragraphs of the notes read” și „Board: a note opens over the board without reloading the page” — previzualizarea cardurilor nu mai numerotează toate paragrafele din bază, iar o notă deschisă de pe tablă apare peste tabla din pagină (`?handler=NoteEditor`), fără reîncărcare; deciziile 48–49;
  - „Modal dialogs: Escape closes them in Firefox too” — `modal.js` tratează Escape la `keydown`, cu tasta prevenită, apoi navighează la adresa de închidere; decizia 50;
  - „Scripts: 003_InsertNoteReferences.sql fills NoteReferences from the existing text” — script SQL de date pentru tabela primului model; rămâne nemodificat;
  - referințele interne refăcute la cererea utilizatorului din 2026-09-28 ([ADR-003](decisions/ADR-003-internal-references.md), deciziile 51–58): referințele CR/bug scrise în paragrafe (`CR 30080`, `CR-30080`, `CR_30080`, `CR30080`, `bug_1234`…), destinația aflată din titluri (exact o notă a contextului vizibilă proprietarului paragrafului), relații pe paragraf în tabela `NoteReferences` recreată de `Scripts/version_0.02/004_ReplaceNoteReferences.sql` (care reindexează și conținutul existent și listează referințele fără destinație și pe cele ambigue), recalculare la salvare, la schimbarea titlului, la crearea și la ștergerea notelor, prin `INoteReferenceService`; editorul desenează linkurile din relații, fără să schimbe textul. Sugestiile din editor, comanda `create-note-references` și linkurile din previzualizarea cardurilor au fost eliminate.
  - referințele cu mai multe note, la cererea utilizatorului din 2026-09-28, după cazul `CR 27881` din jurnalul „CRs” (deciziile 59–64): o referință deschide toate notele contextului care au CR-ul sau bugul în titlu, în afară de nota paragrafului. Notele fiecărei referințe sunt în tabela nouă `NoteReferenceTargets` (legătură 1–M cu `NoteReferences`, care pierde coloana `TargetNoteId`), creată de `Scripts/version_0.02/005_CreateNoteReferenceTargets.sql`. Scriptul mută legăturile existente, reindexează conținutul și listează referințele fără notă, pe cele cu mai multe note și jurnalul „CRs”. Click sau Ctrl+Enter deschide toate notele, în taburi; fără JavaScript, fiecare notă are link. Ștergerea unei note nu mai recalculează referințele.
- **Branch-ul de documentare** `claude/worknotes-markdown-docs-6xyqw4` — această structură de documentație; nu modifică codul, schema sau funcționalitățile.

## Probleme cunoscute

Limitări documentate în version_0.01–0.02:

- [!] Pe `main`, în Firefox, Escape nu închide fereastra editorului: Firefox anulează navigarea pornită de `modal.js` din evenimentul `cancel` al tastei (verificat pe 2026-09-28 în Firefox 136); butonul Închide funcționează. Corecția este în PR #4.
- [!] Pe `main`, după primul schimb prin drag-and-drop, un al doilea drag început cât timp prima salvare este în curs este anulat fără niciun semn vizibil (`dragstart` refuzat cât timp `saving` este activ); corecția este în PR #4.
- La reîncărcarea paginii editorului se redeschide doar tabul activ (adresa `/?note={id}`), nu toate taburile.
- Mutarea unui paragraf prin tăiere și lipire creează un paragraf nou (ID nou).
- Pe telefon, bara de taburi arată aproximativ un tab și jumătate; restul se derulează.
- Cu fonturi foarte late (de exemplu pe unele sisteme Linux), data modificării de pe card trece pe al doilea rând.
- Redenumirea pe loc nu mută cardul imediat; ordinea se actualizează la următoarea încărcare a tablei.
- După un schimb pe tablă, un editor deschis pe aceeași notă în alt tab sau în altă fereastră primește conflict la următoarea salvare (fără să suprascrie ceva); editorul din aceeași pagină primește noua versiune.
- Ordonarea se face numai cu mouse-ul (drag-and-drop HTML5); nu există alternativă de la tastatură, iar pe ecranele tactile depinde de suportul browserului.
- Două note cu aceeași valoare `Order` (posibil numai prin inserări directe în SQL) nu își schimbă locurile prin drag-and-drop; tabla le ordonează după date.

Limitări ale referințelor interne din PR #4 ([ADR-003](decisions/ADR-003-internal-references.md#consecințe)):

- O referință nou scrisă devine link abia după salvare; textul scris într-un link îl ascunde până la salvare.
- Referințele fără nicio notă nu au semn în editor; `005_CreateNoteReferenceTargets.sql`, rulat din nou (și cu `@Save = 0`), le listează.
- O referință cu multe note deschide tot atâtea taburi; fără JavaScript, un link deschide o singură notă, iar celelalte au câte un link numerotat.
- Recalcularea după schimbarea unui titlu sau crearea unei note este o tranzacție separată de operație: două operații simultane pe aceeași referință sau o cerere întreruptă între ele pot lăsa o legătură învechită până la următoarea salvare ([DATABASE.md](DATABASE.md#concurență)).
- Schimbarea vizibilității, arhivarea și ieșirea unui membru din context nu recalculează legăturile (nu au interfață).
- Previzualizarea cardurilor afișează textul fără linkuri.
- După `004`, scripturile `002` și `003` nu se mai pot rula; după `005`, nici `004`.
- Codul presupune că `005` a fost aplicat: fără `NoteReferenceTargets`, deschiderea și salvarea notelor dau eroare.

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
- [DATABASE.md](DATABASE.md) și [Scripts/README.md](../Scripts/README.md): portate — tabelele `NoteReferences` și `NoteReferenceTargets` (modelul nou), scripturile `002`–`005`, `--table dbo.NoteReferences` și `--table dbo.NoteReferenceTargets` în comanda de scaffolding; marcajele „PR #4” se elimină la integrare;
- [decisions/README.md](decisions/README.md): deciziile 32–64 și [ADR-003](decisions/ADR-003-internal-references.md) sunt deja în jurnal; deciziile 33–38, 40–47 și 52 sunt marcate ca înlocuite;
- [ARCHITECTURE.md](ARCHITECTURE.md): `INoteReferenceService` / `NoteReferenceService`, `INoteReferenceRepository` (implementat de `NoteRepository`), fluxurile salvării și al recalculării, `note-references.js` și `WorkNotes.Web/Notes/NoteReferences.cs`; deschiderea fără reîncărcare (handlerul `NoteEditor`, evenimentul `modal:open`, subinterogarea previzualizării limitată la paragrafele notelor citite);
- [SECURITY.md](SECURITY.md): legăturile respectă contextul și vizibilitatea proprietarului paragrafului, cititorul vede numai linkurile către notele pe care le poate vedea, iar recalcularea modifică legăturile paragrafelor altor membri ai contextului;
- [TESTING.md](TESTING.md): `NoteReferenceRulesTests`, `NoteReferenceServiceTests`, noile teste din `NoteServiceTests` și totalurile; verificările manuale ale linkurilor și ale scriptului `004`;
- fișierele `CLAUDE.md` din Web, Business și DataAccess: noile tipuri (`INoteReferenceService`, `INoteReferenceRepository`, `NoteReference.cs` și `NoteReferenceTarget.cs` generate prin scaffolding);
- [UI-UX.md](UI-UX.md), [REQUIREMENTS.md](REQUIREMENTS.md), [DOMAIN-MODEL.md](DOMAIN-MODEL.md): actualizate pentru modelul nou; statusurile trec din [~] în [x] la integrare;
- [ROADMAP.md](ROADMAP.md) și această pagină: lista „Referințe către această notă” și limitările de mai sus;
- [CHANGELOG.md](../CHANGELOG.md), [LOCALIZATION.md](LOCALIZATION.md) (169 de chei, cu `Notes_ReferenceTarget`) și [TESTING.md](TESTING.md) (noile totaluri).

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

## Următorii pași

1. Review-ul branch-ului de documentare și, la cerere explicită, pull request-ul lui.
2. Stabilirea ordinii de integrare între acest branch și PR #4 și portarea modificărilor de documentație din PR #4 ([lista de mai sus](#impactul-asupra-pr-4)).
3. Clarificarea punctelor marcate „TODO: Necesită clarificare”, în primul rând: calea locală de referință, convenția branch-urilor și regula Git din PR #4, mediile Test/Production, backup-ul și rollback-ul, contextele fără membri, pagina de bun venit.
4. Pașii următori ai produsului, în ordinea din [ROADMAP.md](ROADMAP.md#etapele-următoare), numai la cerere explicită.
