# WorkNotes

WorkNotes este un caiet de lucru web pentru munca de zi cu zi pe proiecte: jurnalul cronologic al zilei și articole — fișe de lucru despre subiecte precise, de exemplu un CR, un bug sau o livrare. Notele sunt organizate pe contexte (clienți sau firme), apar ca post-it-uri pe o tablă, grupate pe luni, și se editează într-un editor cu paragrafe care își păstrează identitatea și auditul. Scopul și terminologia sunt în [docs/PROJECT-CONTEXT.md](docs/PROJECT-CONTEXT.md).

Versiunea curentă: **0.02** (eticheta `v.0.02` din `dbo.DatabaseVersion`, afișată în footer). Stadiul real: [docs/CURRENT-STATUS.md](docs/CURRENT-STATUS.md); cerințele și statusul lor: [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md); istoricul: [CHANGELOG.md](CHANGELOG.md).

## Tehnologii

- .NET 10 (`net10.0`), ASP.NET Core Razor Pages, C# cu nullable reference types.
- Entity Framework Core 10.0.12 cu providerul SQL Server, abordare Database First: schema este definită de scripturi SQL versionate, fără migrări EF; instrumentul local `dotnet-ef` 10.0.12 (`Solution/dotnet-tools.json`) servește numai la scaffolding.
- ASP.NET Core Identity cu stocare EF și cookie de autentificare.
- SQL Server.
- Localizare prin resurse .resx: română (implicită și fallback), engleză, poloneză.
- Interfață: Razor, CSS cu tokenuri, JavaScript fără framework (module ES); editorul CodeMirror 6 (licență MIT), inclus local în `wwwroot/lib/codemirror`.
- Teste: xUnit.

## Cerințe de instalare

- .NET 10 SDK. Build-ul și testele au fost verificate și cu SDK 10.0.112 pe Linux.
- O instanță SQL Server. Configurația locală versionată folosește `localhost\MSSQLSERVER02` cu Windows Authentication; baza `WorkNotes.db` trebuie să existe înainte de aplicarea scripturilor.
- SQL Server Management Studio sau `sqlcmd`, pentru scripturile SQL.
- PowerShell, pentru `Solution/tools/Test-Resources.ps1`.
- Node.js și npm, numai pentru reconstruirea bundle-ului CodeMirror (`Solution/tools/codemirror`).

## Structura

```text
<rădăcina repository-ului>/
├── AGENTS.md                  # regulile obligatorii, pentru orice agent sau dezvoltator
├── CLAUDE.md                  # instrucțiunile Claude Code și importurile permanente
├── README.md
├── CHANGELOG.md
├── docs/                      # documentația detaliată; docs/decisions/ conține ADR-urile
├── Scripts/                   # scripturile SQL versionate (README.md, version_0.01/, version_0.02/)
└── Solution/                  # folderul soluției: comenzile dotnet se rulează de aici
    ├── WorkNotes.sln
    ├── dotnet-tools.json      # dotnet-ef 10.0.12, instrument local
    ├── WorkNotes.Web/         # Razor Pages, ViewModels, localizare, CSS/JS, compunerea DI (Program.cs)
    ├── WorkNotes.Business/    # interfețe (Abstractions), servicii (Services), modele și reguli (Models)
    ├── WorkNotes.DataAccess/  # DbContext-uri, entități, repository-uri, Identity, AddDataAccess
    ├── WorkNotes.Resources/   # SharedResources.resx (ro) + .ro/.en/.pl
    ├── WorkNotes.Business.Tests/  # teste xUnit, fără bază de date
    └── tools/
        ├── Test-Resources.ps1     # verificarea catalogului .resx
        └── codemirror/            # sursa și construirea bundle-ului CodeMirror
```

Descrierea proiectelor, a fluxurilor și a paginilor este în [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Configurarea SQL Server

- Connection string-ul este cheia `ConnectionStrings:WorkNotes` din `Solution/WorkNotes.Web/appsettings.json`; fără ea aplicația nu pornește.
- Configurația locală versionată: serverul `localhost\MSSQLSERVER02`, baza `WorkNotes.db`, `Integrated Security=True`, `Encrypt=True`, `TrustServerCertificate=True` — fără utilizator și fără parolă. Conexiunea folosește identitatea Windows a procesului, care are nevoie de drepturi de citire și scriere a datelor pentru aplicație și de drepturi de modificare a schemei atunci când se execută scripturile.
- Pentru alt server sau alt mediu suprascrieți cheia prin configurația mediului (de exemplu variabila de mediu `ConnectionStrings__WorkNotes`), nu prin fișiere versionate; credențialele nu se salvează în Git.
- Pregătirea bazei: creați baza `WorkNotes.db`, apoi aplicați, în ordine, scripturile din `Scripts/version_0.01` și `Scripts/version_0.02` — lista, efectul fiecărui script și comenzile `sqlcmd` sunt în [Scripts/README.md](Scripts/README.md). Schema este descrisă în [docs/DATABASE.md](docs/DATABASE.md).

## Pornire

```powershell
Set-Location '<rădăcina repository-ului>\Solution'
dotnet run --project WorkNotes.Web --launch-profile http
```

Deschideți http://localhost:5018 (profilul `https`: https://localhost:7190). Schema trebuie aplicată înainte de rulare; erorile de conexiune nu sunt tratate ca tabelă goală. Footerul afișează versiunea din baza de date sau „Versiune neconfigurată” când `dbo.DatabaseVersion` este goală.

## Build și teste

Din folderul `Solution`:

```powershell
dotnet tool restore
dotnet restore WorkNotes.sln
dotnet build WorkNotes.sln --no-restore
dotnet test WorkNotes.sln --no-build --no-restore
.\tools\Test-Resources.ps1
```

Ultimul rezultat și verificările manuale obligatorii: [docs/CURRENT-STATUS.md](docs/CURRENT-STATUS.md) și [docs/TESTING.md](docs/TESTING.md).

## Scripturi SQL

Scripturile sunt în `Scripts/version_0.0x`, câte un folder pentru fiecare versiune; din `Solution` se referă ca `..\Scripts\version_0.0x\...`. Scripturile noi se adaugă numai în folderul versiunii curente. Convențiile și ordinea de aplicare: [Scripts/README.md](Scripts/README.md); regulile de versionare: [docs/VERSIONING.md](docs/VERSIONING.md).

## Documentație

| Document | Conținut |
| --- | --- |
| [AGENTS.md](AGENTS.md) | Regulile obligatorii: arhitectură, SOLID, date, SQL, localizare, design, verificare, Git |
| [CLAUDE.md](CLAUDE.md) | Instrucțiunile pentru Claude Code |
| [CHANGELOG.md](CHANGELOG.md) | Modificările pe versiuni |
| [docs/PROJECT-CONTEXT.md](docs/PROJECT-CONTEXT.md) | Originea, scopul și terminologia |
| [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md) | Cerințele funcționale și statusul lor |
| [docs/DOMAIN-MODEL.md](docs/DOMAIN-MODEL.md) | Entitățile, relațiile și regulile de business |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Proiectele, dependențele, serviciile, fluxurile și paginile |
| [docs/DATABASE.md](docs/DATABASE.md) | Schema SQL Server, EF Core Database First, cheile, indecșii și concurența |
| [docs/CODING-STANDARDS.md](docs/CODING-STANDARDS.md) | Convențiile pentru C#, JavaScript, CSS și Razor |
| [docs/UI-UX.md](docs/UI-UX.md) | Designul „Hârtie & salvie” și comportamentul interfeței |
| [docs/LOCALIZATION.md](docs/LOCALIZATION.md) | Resursele .resx, limbile, cheile și formatarea |
| [docs/SECURITY.md](docs/SECURITY.md) | Autentificarea, autorizarea și protecțiile aplicației |
| [docs/TESTING.md](docs/TESTING.md) | Strategia de testare, comenzile și verificările manuale |
| [docs/GIT-WORKFLOW.md](docs/GIT-WORKFLOW.md) | Branch-uri, commit-uri, pull request-uri și merge |
| [docs/VERSIONING.md](docs/VERSIONING.md) | Versiunile, `DatabaseVersion` și folderele de scripturi |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | Mediile, configurarea, publicarea și rollback-ul |
| [docs/CURRENT-STATUS.md](docs/CURRENT-STATUS.md) | Situația curentă, problemele cunoscute și contradicțiile |
| [docs/ROADMAP.md](docs/ROADMAP.md) | Etapele următoare, îmbunătățirile și dependențele |
| [docs/decisions/README.md](docs/decisions/README.md) | ADR-urile și jurnalul deciziilor |
| [Scripts/README.md](Scripts/README.md) | Scripturile SQL: structură, denumire, ordine, verificare |
