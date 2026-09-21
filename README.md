# WorkNotes

Aplicație ASP.NET Core Razor Pages pe .NET 10, cu Entity Framework Core 10.0.12 și SQL Server, Database First.

Schema bazei de date este administrată prin scripturi SQL. Modelul EF și DbContext sunt generate din schema existentă prin reverse engineering (scaffolding). Nu se folosesc migrări EF.

## Arhitectură

- **WorkNotes.Web**: prezentare Razor Pages, configurare și compunerea dependency injection în Program.cs. Pagina principală primește IApplicationVersionService și afișează rezultatul; nu accesează DbContext și nu selectează versiunea curentă.
- **WorkNotes.Business**: interfețe de serviciu și acces la date, plus regula de selectare a versiunii numerice maxime. Nu depinde de Web, DataAccess sau EF Core.
- **WorkNotes.DataAccess**: context și entități generate din SQL Server, repository concret și extensia de înregistrare DI. Implementează contractul definit în Business.
- **WorkNotes.Business.Tests**: teste independente de SQL Server pentru versiuni, rezultat gol, anulare și erori.

Dependențe: Web → Business; Web → DataAccess numai la compunerea DI; DataAccess → Business; Business.Tests → Business. Serviciul, repository-ul și DbContext folosesc lifetime scoped. Toate operațiile de citire propagă CancellationToken.

Flux: `IndexModel → IApplicationVersionService → ApplicationVersionService → IApplicationVersionRepository → ApplicationVersionRepository → WorkNotesDbContext`.

Regulile obligatorii pentru modificări viitoare sunt în [AGENTS.md](AGENTS.md).

## Structură

```text
E:\GitRepository\Vali\WorkNotes\
├── WorkNotes\                         # Folderul soluției și rădăcina Git
│   ├── WorkNotes.sln
│   ├── AGENTS.md
│   ├── dotnet-tools.json              # dotnet-ef 10.0.12, instrument local
│   ├── README.md
│   ├── .gitignore
│   ├── WorkNotes.Business\
│   │   ├── WorkNotes.Business.csproj
│   │   ├── Abstractions\
│   │   │   ├── IApplicationVersionService.cs
│   │   │   └── IApplicationVersionRepository.cs
│   │   └── Services\ApplicationVersionService.cs
│   ├── WorkNotes.DataAccess\
│   │   ├── WorkNotes.DataAccess.csproj
│   │   ├── DependencyInjection.cs
│   │   ├── Context\WorkNotesDbContext.cs
│   │   ├── Entities\DatabaseVersion.cs
│   │   └── Repositories\ApplicationVersionRepository.cs
│   ├── WorkNotes.Business.Tests\
│   │   ├── WorkNotes.Business.Tests.csproj
│   │   └── ApplicationVersionServiceTests.cs
│   └── WorkNotes.Web\
│       ├── WorkNotes.Web.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Pages\
│       │   ├── Index.cshtml
│       │   ├── Index.cshtml.cs
│       │   ├── _ViewImports.cshtml
│       │   ├── _ViewStart.cshtml
│       │   └── Shared\_Layout.cshtml
│       ├── Properties\launchSettings.json
│       └── wwwroot\css\site.css
└── Scripts\                           # Alături de folderul soluției
    └── version_0.01\
        ├── 000_CreateDatabaseVersion.sql
        ├── 001_InsertDatabaseVersion.sql
        └── 002_RemoveEFMigrationsHistory.sql
```

Folderul `Scripts` este în afara repository-ului Git actual, conform amplasării solicitate lângă folderul soluției. Un commit din folderul soluției nu îl va include automat.

## Conexiunea locală

În `WorkNotes.Web/appsettings.json`, cheia `ConnectionStrings:WorkNotes`:

```text
Server=localhost\MSSQLSERVER02;Database=WorkNotes.db;Integrated Security=True;Encrypt=True;TrustServerCertificate=True
```

Conexiunea utilizează identitatea Windows a procesului. Aceasta trebuie să aibă drept de citire pentru aplicație și drepturi de modificare a schemei când se execută scripturile SQL. `TrustServerCertificate=True` este configurat pentru instanța locală.

## Restaurare și compilare

Din PowerShell:

```powershell
Set-Location 'E:\GitRepository\Vali\WorkNotes\WorkNotes'
dotnet tool restore
dotnet restore WorkNotes.sln
dotnet build WorkNotes.sln --no-restore
dotnet test WorkNotes.sln --no-build --no-restore
```

## Pregătirea bazei de date prin SQL

Baza `WorkNotes.db` trebuie să existe pe instanța SQL Server. Execută scripturile din `..\Scripts\version_0.01` în ordinea numelor, în SQL Server Management Studio, conectat prin Windows Authentication la `localhost\MSSQLSERVER02`:

1. `000_CreateDatabaseVersion.sql`: creează `dbo.DatabaseVersion` numai dacă lipsește, cu `Version nvarchar(50) NOT NULL` și cheia primară `PK_DatabaseVersion`.
2. `001_InsertDatabaseVersion.sql`: inserează `v.0.01` numai dacă lipsește.
3. `002_RemoveEFMigrationsHistory.sql`: elimină istoricul EF rămas de la abordarea anterioară; dacă tabela nu există, nu face nimic. Acesta este un script de tranziție, nu este necesar la fiecare versiune viitoare.

Alternativ, dacă `sqlcmd` este instalat:

```powershell
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\000_CreateDatabaseVersion.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\001_InsertDatabaseVersion.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\002_RemoveEFMigrationsHistory.sql'
```

Scriptul de creare păstrează tabela și datele existente. Modificările ulterioare ale structurii se fac prin scripturi ALTER dedicate. La inserare, tranzacția, blocarea verificării și cheia primară previn duplicatele, inclusiv la executări concurente.

## Actualizarea modelului EF — Database First

După modificarea schemei prin SQL, regenerează clasele din baza de date:

```powershell
dotnet tool restore
dotnet ef dbcontext scaffold 'Name=ConnectionStrings:WorkNotes' Microsoft.EntityFrameworkCore.SqlServer --project WorkNotes.DataAccess --startup-project WorkNotes.Web --context WorkNotesDbContext --context-dir Context --output-dir Entities --namespace WorkNotes.DataAccess.Entities --context-namespace WorkNotes.DataAccess.Context --table dbo.DatabaseVersion --no-onconfiguring --force
dotnet build WorkNotes.sln
dotnet test WorkNotes.sln --no-build --no-restore
```

Comanda folosește Web pentru pornire și citirea configurației, dar generează fișierele numai în DataAccess. `--no-onconfiguring` păstrează conexiunea în configurație, fără să o scrie în clasele generate. Program.cs transmite connection string-ul extensiei AddDataAccess; această extensie înregistrează DbContext și providerul SQL Server.

`--force` suprascrie fișierele generate `WorkNotes.DataAccess/Context/WorkNotesDbContext.cs` și `WorkNotes.DataAccess/Entities/DatabaseVersion.cs`. Extensiile scrise manual se pun în fișiere partial separate. Pentru tabele viitoare, extinde lista de opțiuni `--table` astfel încât regenerarea contextului să includă toate entitățile necesare.

Pachetul EF Core Design este referit cu PrivateAssets=all în DataAccess și în Web (startup project), exclusiv pentru tooling. Providerul SQL Server este referit direct de DataAccess. Business nu are pachete EF. Instrumentul local dotnet-ef este păstrat pentru scaffolding. Nu există fișiere de migrare, snapshot sau tabelă `__EFMigrationsHistory`. Aplicația nu creează sau modifică schema și nu inserează versiuni la pornire.

## Rularea aplicației

```powershell
dotnet run --project WorkNotes.Web --launch-profile http
```

Deschide http://localhost:5018. Pagina are header, body și footer; versiunea din baza de date este afișată în dreapta footerului, la baza paginii.

Tabela goală afișează „Versiune neconfigurată”. Schema trebuie aplicată înainte de rulare; erorile de conexiune nu sunt tratate ca tabelă goală.

Întrucât tabela existentă nu conține o dată de instalare, „versiunea curentă” înseamnă cea mai mare versiune numerică înregistrată, cu formatul `v.major.minor[.build[.revision]]`. Textul original se afișează nemodificat: `v.0.01`. Compararea este numerică, astfel că `v.0.10` urmează după `v.0.9`. Etichetele care nu se pot interpreta numeric sunt ordonate la finalul priorității, determinist, după text.

Documentație: [EF Core SQL Server](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/), [comenzile EF Core, inclusiv dbcontext scaffold](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).
