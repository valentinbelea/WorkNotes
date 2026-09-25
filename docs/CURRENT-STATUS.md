# Starea curentă

**Data ultimei actualizări:** 2026-09-25 · **Versiunea:** 0.02 (`v.0.02`) · **Baza analizei:** `main` la commit-ul `504c01e` (merge-ul PR #3)

Statusul detaliat al fiecărei cerințe este în [REQUIREMENTS.md](REQUIREMENTS.md); planul, în [ROADMAP.md](ROADMAP.md).

## Implementat

- **Conturi** (ASP.NET Core Identity, Database First): înregistrare, autentificare, datele contului, schimbarea parolei, deconectare; politica de parolă și blocarea după 5 încercări.
- **Localizare** ro/en/pl pentru toate textele (168 de chei, aceleași în cele patru fișiere .resx).
- **Contexte și membri**: listare, adăugare, editare, ștergere în overlay; proprietarul gestionează membrii după e-mail.
- **Tabla**: o tablă pentru fiecare context, notele grupate pe luni după ultima modificare; post-it-uri pentru jurnale și articole, create, redenumite și șterse direct pe tablă; ordonarea prin drag-and-drop în aceeași lună (version_0.02).
- **Editorul** CodeMirror 6: paragrafe cu identitate și audit propriu, căutare, undo/redo, Ctrl+S, detectarea salvărilor concurente, taburi, minimizare.
- **Mesaje de salvare**, sigla WN, designul „Hârtie & salvie”, funcționarea de bază fără JavaScript, versiunea în footer.

## În dezvoltare

- **PR #4** — branch `main_task_02`, deschis pe 2026-09-25, neintegrat în `main`, două commit-uri:
  - „Board: the next drag is no longer refused while a swap is being saved” — schimburile se aplică la `dragend` și se salvează pe rând;
  - „Editor and board: internal references between notes” — referințe interne între note, tabela `NoteReferences` (`Scripts/version_0.02/002_CreateNoteReferences.sql`), deciziile 32–41 și reguli noi în `AGENTS.md`.
- **Branch-ul de documentare** `claude/worknotes-markdown-docs-6xyqw4` — această structură de documentație; nu modifică codul, schema sau funcționalitățile.

## Probleme cunoscute

Limitări documentate în version_0.01–0.02:

- [!] Pe `main`, după primul schimb prin drag-and-drop, un al doilea drag început cât timp prima salvare este în curs este anulat fără niciun semn vizibil (`dragstart` refuzat cât timp `saving` este activ); corecția este în PR #4.
- La reîncărcarea paginii editorului se redeschide doar tabul activ (adresa `/?note={id}`), nu toate taburile.
- Mutarea unui paragraf prin tăiere și lipire creează un paragraf nou (ID nou).
- Pe telefon, bara de taburi arată aproximativ un tab și jumătate; restul se derulează.
- Cu fonturi foarte late (de exemplu pe unele sisteme Linux), data modificării de pe card trece pe al doilea rând.
- Redenumirea pe loc nu mută cardul imediat; ordinea se actualizează la următoarea încărcare a tablei.
- După un schimb pe tablă, un editor deschis pe aceeași notă în alt tab sau în altă fereastră primește conflict la următoarea salvare (fără să suprascrie ceva); editorul din aceeași pagină primește noua versiune.
- Ordonarea se face numai cu mouse-ul (drag-and-drop HTML5); nu există alternativă de la tastatură, iar pe ecranele tactile depinde de suportul browserului.
- Două note cu aceeași valoare `Order` (posibil numai prin inserări directe în SQL) nu își schimbă locurile prin drag-and-drop; tabla le ordonează după date.

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

- [AGENTS.md](../AGENTS.md): regula drag-and-drop cu salvări pe rând, regulile referințelor interne și regula Git (armonizată cu contradicția 2);
- [DATABASE.md](DATABASE.md) și [Scripts/README.md](../Scripts/README.md): tabela `NoteReferences`, scriptul `version_0.02/002_CreateNoteReferences.sql` (structura, ordinea, comanda `sqlcmd`), `--table dbo.NoteReferences` și `NoteReference.cs` în comanda de scaffolding;
- [decisions/README.md](decisions/README.md): deciziile 32–41 și, eventual, un ADR pentru formatul referințelor;
- [UI-UX.md](UI-UX.md), [REQUIREMENTS.md](REQUIREMENTS.md), [DOMAIN-MODEL.md](DOMAIN-MODEL.md), [SECURITY.md](SECURITY.md): referințele interne și noul comportament drag-and-drop, cu statusurile trecute din [~] în [x];
- [ARCHITECTURE.md](ARCHITECTURE.md): `note-references.js`, `WorkNotes.Web/Notes/NoteReferences.cs`, handlerele `ReferenceSuggestions` și `ReferenceTargets`;
- [ROADMAP.md](ROADMAP.md) și această pagină: lista „Referințe către această notă” și cele cinci limitări noi din backlog-ul PR-ului;
- [CHANGELOG.md](../CHANGELOG.md), [LOCALIZATION.md](LOCALIZATION.md) (numărul de chei) și [TESTING.md](TESTING.md) (`NoteReferenceRulesTests` și noile totaluri).

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

## Următorii pași

1. Review-ul branch-ului de documentare și, la cerere explicită, pull request-ul lui.
2. Stabilirea ordinii de integrare între acest branch și PR #4 și portarea modificărilor de documentație din PR #4 ([lista de mai sus](#impactul-asupra-pr-4)).
3. Clarificarea punctelor marcate „TODO: Necesită clarificare”, în primul rând: calea locală de referință, convenția branch-urilor și regula Git din PR #4, mediile Test/Production, backup-ul și rollback-ul, contextele fără membri, pagina de bun venit.
4. Pașii următori ai produsului, în ordinea din [ROADMAP.md](ROADMAP.md#etapele-următoare), numai la cerere explicită.
