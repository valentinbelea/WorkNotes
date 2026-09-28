# CLAUDE.md — instrucțiuni pentru Claude Code în WorkNotes

WorkNotes este o aplicație web de tip caiet de lucru: jurnalul zilnic și articole (fișe de lucru, de exemplu despre un CR sau un bug), organizate pe contexte (clienți sau firme) și afișate ca post-it-uri pe o tablă, cu un editor de text pe paragrafe. Detalii: [README.md](README.md) și [docs/PROJECT-CONTEXT.md](docs/PROJECT-CONTEXT.md).

Tehnologii: .NET 10 (`net10.0`), ASP.NET Core Razor Pages, Entity Framework Core 10.0.12 (Database First, fără migrări), ASP.NET Core Identity, SQL Server, resurse .resx (română implicită, engleză, poloneză), JavaScript fără framework și CodeMirror 6 inclus local, teste xUnit.

## Documente încărcate permanent

Aceste documente fac parte din contextul fiecărei sesiuni. Regulile obligatorii sunt în AGENTS.md; celelalte descriu proiectul.

@AGENTS.md
@README.md
@docs/ARCHITECTURE.md
@docs/DATABASE.md
@docs/CODING-STANDARDS.md
@docs/LOCALIZATION.md
@docs/TESTING.md
@docs/VERSIONING.md

## Structura soluției

- Rădăcina repository-ului: `AGENTS.md`, `CLAUDE.md`, `README.md`, `CHANGELOG.md`, `docs/` (inclusiv `docs/decisions/`), `Scripts/` (scripturi SQL versionate, `Scripts/version_0.0x`) și `Solution/`.
- `Solution/WorkNotes.sln` conține `WorkNotes.Web`, `WorkNotes.Business`, `WorkNotes.DataAccess`, `WorkNotes.Resources` și `WorkNotes.Business.Tests`; `Solution/tools/` conține `Test-Resources.ps1` și construirea bundle-ului CodeMirror.
- Proiectele Web, Business și DataAccess au propriul `CLAUDE.md`, cu regulile specifice, încărcat când lucrezi în folderul respectiv.

## Comenzi

Din folderul `Solution` (PowerShell pe Windows; aceleași comenzi `dotnet` funcționează și în sesiunile cloud Linux):

```powershell
dotnet tool restore
dotnet restore WorkNotes.sln
dotnet build WorkNotes.sln --no-restore
dotnet test WorkNotes.sln --no-build --no-restore
dotnet run --project WorkNotes.Web --launch-profile http   # http://localhost:5018, necesită SQL Server
.\tools\Test-Resources.ps1                                 # verificarea resurselor .resx (PowerShell)
```

## Reguli esențiale

Rezumat de orientare; formularea obligatorie și completă este în secțiunile indicate din [AGENTS.md](AGENTS.md).

- Separarea Web / Business / DataAccess, direcția dependențelor și interdicția EF/SQL în Web — [Arhitectură și dependențe](AGENTS.md#arhitectură-și-dependențe).
- SOLID, servicii consumate prin interfețe, injectare prin constructor, lifetime scoped — [SOLID, interfețe și dependency injection](AGENTS.md#solid-interfețe-și-dependency-injection).
- Nicio logică de business în PageModel/controllere, în view-uri Razor sau în JavaScript; Business întoarce coduri de stare, Web le traduce — [Arhitectură și dependențe](AGENTS.md#arhitectură-și-dependențe).
- Acces la date asincron, cu `CancellationToken` propagat până la EF — [Acces asincron și anulare](AGENTS.md#acces-asincron-și-anulare).
- Toate textele afișate vin din `.resx`, complete în română, engleză și poloneză, cu aceleași chei în cele patru fișiere — [Localizare obligatorie](AGENTS.md#localizare-obligatorie).
- Baza de date se modifică numai prin scripturi SQL versionate în folderul versiunii curente; fără migrări EF; modelul EF se regenerează prin scaffolding — [EF Core și schema SQL](AGENTS.md#ef-core-și-schema-sql-database-first), [Scripturi SQL și versiuni](AGENTS.md#scripturi-sql-și-versiuni).
- Nu aplica scripturi SQL, migrări sau alte actualizări pe nicio bază de date fără cererea explicită a utilizatorului; nu modifica scripturile deja livrate și nu schimba versiunea fără cerere explicită.
- După modificări rulează verificările din [Verificarea livrării](AGENTS.md#verificarea-livrării) și raportează exact ce nu s-a putut verifica.
- Lucrează într-un feature branch creat din `main`; fără commit, push, pull request sau merge nesolicitate; păstrează modificările utilizatorului — [Git și limitele sarcinii](AGENTS.md#git-și-limitele-sarcinii).
- Nu modifica funcționalități, cod sau documente fără legătură cu sarcina; actualizează documentația afectată — [Întreținerea documentației](AGENTS.md#întreținerea-documentației).

## Când citești celelalte documente

| Situație | Document |
| --- | --- |
| Lucrezi la o funcționalitate sau îi schimbi statusul | [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md), [docs/CURRENT-STATUS.md](docs/CURRENT-STATUS.md) |
| Schimbi reguli de business, entități sau terminologie | [docs/DOMAIN-MODEL.md](docs/DOMAIN-MODEL.md), [docs/PROJECT-CONTEXT.md](docs/PROJECT-CONTEXT.md) |
| Modifici pagini, CSS, JavaScript, editorul sau tabla | [docs/UI-UX.md](docs/UI-UX.md) |
| Atingi autentificarea, autorizarea, accesul la note, validarea sau conținutul editorului | [docs/SECURITY.md](docs/SECURITY.md) |
| Creezi sau revizuiești scripturi SQL | [Scripts/README.md](Scripts/README.md) |
| Creezi branch-uri, commit-uri sau pull request-uri | [docs/GIT-WORKFLOW.md](docs/GIT-WORKFLOW.md) |
| Pregătești o publicare sau lucrezi cu medii Test/Production | [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) |
| Iei o decizie de arhitectură sau de produs | [docs/decisions/README.md](docs/decisions/README.md) |
| Planifici pașii următori | [docs/ROADMAP.md](docs/ROADMAP.md) |
| Înregistrezi o schimbare livrată | [CHANGELOG.md](CHANGELOG.md) |

## Mediul de lucru

- Mediul local de dezvoltare este Windows, cu instanța SQL Server `localhost\MSSQLSERVER02` și PowerShell.
- În sesiunile cloud (Linux) nu există SQL Server, iar .NET SDK nu este preinstalat: în sesiunea din 2026-09-25 SDK-ul 10.0.112 a fost instalat cu `apt-get install -y dotnet-sdk-10.0`, deoarece descărcarea de pe `builds.dotnet.microsoft.com` este blocată de politica de rețea. Acolo se pot verifica build-ul și testele Business, nu și aplicația, scaffolding-ul sau scripturile; spune explicit acest lucru în raport.
- `tools/Test-Resources.ps1` necesită PowerShell; dacă nu este disponibil, raportează verificarea ca neefectuată.
- Oprește procesele `WorkNotes.Web` pornite pentru verificare (vezi [Verificarea livrării](AGENTS.md#verificarea-livrării)).
