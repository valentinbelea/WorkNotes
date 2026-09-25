# WorkNotes

## Design — Hârtie & salvie

Tema aprobată folosește verdele #176C65, fundal crem și post-it-uri pastelate. Stilurile sunt separate în tokens.css (valori comune), site.css (componente), notes-board.css (tablă) și postit.css (hârtia). Decalajele cardurilor sunt clase CSS alese de server din ID-ul notei; notes-board.js se ocupă doar de comportament (notă nouă, renunțare, deschidere).

Ghidul componentelor și contractul de integrare sunt în [docs/design-system.md](docs/design-system.md). Sigla WN (verde #176C65, cu banda post-it-urilor) este în `wwwroot/images/logo-wn.svg`: apare numai în fereastra editorului — în stânga headerului și în forma minimizată — și este favicon-ul; `wwwroot/favicon.ico` (16, 32 și 48px, generat din același SVG) servește browserele fără favicon SVG. Pagina principală este dashboardul utilizatorului autentificat: notele vizibile pentru el, ca post-it-uri, și butonul Notă nouă (vezi „Note — version_0.01”). Subtitlul din header (de exemplu „Spațiul meu”) este dedus din secțiunea meniului curent prin WorkNotes.Web/Navigation/NavigationSections.cs.

Mesajele de salvare (succes, avertisment, eroare) apar fixe în partea de sus a ferestrei, centrate, cu buton de închidere, și rămân până le închide utilizatorul, cu sau fără JavaScript (`Pages/Shared/_StatusMessage.cshtml`). Paginile le setează prin `TempData.SetStatusMessage(cheie, tip)` (`WorkNotes.Web/Messages`).

## Localizare

WorkNotes.Resources este proiectul independent de resurse, referit de Web:

```text
WorkNotes.Resources/
  Resources/
    SharedResources.cs
    SharedResources.resx       # română, fallback
    SharedResources.ro.resx
    SharedResources.en.resx
    SharedResources.pl.resx
WorkNotes.Web/
  Localization/
    LocalizationConfiguration.cs
    LocalizedMvcOptions.cs
    LocalizedIdentityErrorDescriber.cs
  Pages/Language.cshtml(.cs)
  Pages/Shared/_LanguageSelector.cshtml
  wwwroot/js/language.js
tools/Test-Resources.ps1
```

Culturi: ro-RO (implicită), en-US, pl-PL. Selectorul din header se aplică imediat, prin POST protejat CSRF, și salvează cookie-ul standard .AspNetCore.Culture pentru un an. Ruta și query string-ul curent sunt păstrate; fără JavaScript există buton de aplicare. Schimbarea limbii reîncarcă pagina; datele nesalvate din formulare nu sunt păstrate.

IStringLocalizer<SharedResources> și IStringLocalizer folosesc același catalog. RequestLocalizationMiddleware aplică limba înainte de autentificare/pagini. DataAnnotations și mesajele de model binding folosesc resursele comune. Validarea client consumă atributele data-val-* codificate de Razor, fără dicționare JS duplicate. LocalizedIdentityErrorDescriber este în Web; Business/DataAccess returnează coduri, iar traducerea finală aparține prezentării. TempData transportă chei de succes.

Pentru fiecare text nou completați toate cele patru fișiere .resx cu aceeași cheie și aceiași parametri. Nu traduceți WorkNotes, datele utilizatorilor, identificatorii și logurile tehnice.

Verificare reproductibilă a catalogului (chei identice, valori, fallback, parametri și utilizări):

```powershell
.\tools\Test-Resources.ps1
```

Nu sunt necesare schimbări SQL; versiunea rămâne v.0.01, iar regulile Database First rămân valabile.

## Contexte — version_0.01

Contextele (de exemplu SD Worx, TopDev) sunt prima condiție a modulului Notes. Tabela `dbo.WorkContexts` are `Id`, `Name` (nvarchar(100), unic prin `UX_WorkContexts_Name`) și `Description` (nvarchar(1000), opțională). Numele tabelei nu este `Contexts`: entitatea generată ar fi `Context`, în conflict cu namespace-ul `WorkNotes.DataAccess.Context`.

- /Contexts: contextele în care utilizatorul este membru, ordonate după nume, cu Editează / Șterge; linkul „Contexte” din meniu apare pentru utilizatorii autentificați.
- /Contexts?add=true: context nou; /Contexts?edit={id}: editare. Formularul se deschide peste listă, într-un overlay (dialog); după salvare se revine la listă, iar la erori de validare overlay-ul rămâne deschis.
- /Contexts?members={id}: membrii contextului și adăugarea unui membru după e-mail, într-un overlay; numai pentru proprietar (butonul Membri apare doar pe contextele proprii). Contul este căutat după e-mailul normalizat de Identity, iar noul membru primește rolul `Member`. Mesajele disting e-mail invalid, cont inexistent și membru existent. Tot acolo proprietarul elimină membrii cu rolul `Member` (butonul Elimină, POST cu antiforgery); proprietarul nu se poate elimina, iar membrul eliminat pierde imediat accesul la context.
- /Contexts?delete={id}: confirmarea ștergerii, tot într-un overlay peste listă; ștergerea se face numai prin POST cu antiforgery.

Membri: `Pages/Contexts → IContextMemberService → ContextMemberService → IContextMemberRepository → ContextMemberRepository → WorkNotesDbContext (ContextMembers) + AccountsDbContext (Users)`. Serviciul verifică rolul `Owner` prin `IWorkContextRepository` înainte de citire sau adăugare.

Flux: `Pages/Contexts → IWorkContextService → WorkContextService → IWorkContextRepository → WorkContextRepository → WorkNotesDbContext`. Scriptul este în `Scripts/version_0.01/004_CreateWorkContexts.sql`. Business validează și normalizează (Trim, descriere goală → NULL) și întoarce coduri `WorkContextSaveStatus`; Web le traduce. Unicitatea numelui este garantată de indexul unic, inclusiv la salvări concurente. Fiecare utilizator vede numai contextele în care este membru (orice rol); filtrul este aplicat în repository la toate citirile și modificările, iar pentru un context în care utilizatorul nu este membru paginile răspund 404, fără a confirma existența lui. Numai proprietarul (`Owner`) poate edita și șterge: regula este în `WorkContextService`, butoanele Editează / Șterge apar doar pe rândurile proprii, iar un membru care încearcă direct adresa sau un POST primește 403. Numele rămâne unic global, deci un nume folosit într-un context străin este refuzat ca duplicat.

`dbo.ContextMembers` (`ContextId`, `UserId`, `Role`, `AddedAtUtc`; cheie primară pe `ContextId` + `UserId`) păstrează membrii fiecărui context. Rolurile acceptate de constrângerea `CK_ContextMembers_Role` sunt `Owner` și `Member` (constantele din `ContextRoles`). La crearea unui context, creatorul este adăugat ca `Owner` în aceeași tranzacție. Ștergerea contextului sau a utilizatorului elimină membrii în cascadă. Contextele create înainte de scriptul 005 nu au membri. Relația către `Users` există numai în SQL: `Users` aparține `AccountsDbContext`, iar scaffolding-ul omite cheia externă (mesaj informativ).

```powershell
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\004_CreateWorkContexts.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\005_CreateContextMembers.sql'
```

## Note — version_0.01

`dbo.Notes` (scriptul `006_CreateNotes.sql`) păstrează notele: contextul (obligatoriu), proprietarul, tipul (`Journal` / `Article`), titlul opțional, data jurnalului, vizibilitatea (`Private` / `Context`), auditul creării și al ultimei modificări, arhivarea și `RowVersion`. Deciziile confirmate:

- se pot crea mai multe jurnale pe zi: scriptul `007_AllowSeveralJournalsPerDay.sql` elimină indexul unic creat de 006; `JournalDate` rămâne ziua locală a aplicației (`TimeProvider`);
- notele noi sunt private; o notă cu vizibilitatea `Context` este văzută de membrii contextului;
- numai proprietarul editează nota; orice membru al contextului poate crea note în el;
- un context care are note nu poate fi șters (cheie externă fără cascadă; mesaj „Contextul are note și nu poate fi șters.”).

Există câte o tablă pentru fiecare context: lista de contexte înlocuiește titlul tablei (`/?context={id}`, implicit primul context). Pe tablă notele sunt grupate pe luni după data locală a ultimei modificări, `ISNULL(ModifiedAtUtc, CreatedAtUtc)` (`NoteSummary.LastChangedAtUtc`), cele mai noi luni primele; în fiecare lună apar întâi jurnalele, apoi articolele, fiecare de la cea mai recentă modificare. O notă modificată trece astfel în luna modificării. În `dbo.Notes`, `ModifiedAtUtc` primește la inserare aceeași valoare ca `CreatedAtUtc`, așa că `NoteRepository` raportează nota nemodificată fără dată de modificare (`null`). Gruparea și ordinea sunt în `NoteService`.

Flux: `Pages/Index → INoteService → NoteService → INoteRepository → NoteRepository → WorkNotesDbContext`. `NoteService` validează tipul și titlul și verifică apartenența la context prin `IWorkContextRepository`.

### Editorul text

Open sau dublu-click pe un post-it deschide editorul peste tablă, într-un dialog cu overlay de 90% din fereastră (`/?note={id}`, randat de `Pages/Shared/_NoteEditorDialog.cshtml`); containerul editorului are scroll propriu. Închide, Escape sau click în afara dialogului revin la tablă, cu avertizare dacă există modificări nesalvate. Editorul este CodeMirror 6 (licență MIT, fără cost de licență sau serviciu cloud): text, căutare și înlocuire (Ctrl+F), undo/redo, evidențierea rândului activ, salvare cu butonul sau Ctrl+S și avertizare la părăsirea paginii cu modificări nesalvate. Toate textele editorului, inclusiv panoul de căutare, vin din `.resx`. Notele se deschid în taburi în același editor: Open sau dublu-click pe un card cât timp editorul este în pagină (de exemplu minimizat) adaugă un tab (`/?handler=NoteTab&note={id}`) sau activează tabul existent, fără reîncărcare; fiecare tab își păstrează separat starea. Headerul are taburile și, în dreapta, Minimizează și Închide (toate taburile); × pe un tab închide doar acea notă, cu confirmare dacă are modificări nesalvate. Salvează, contextul și celelalte detalii sunt în footerul fiecărei note. Minimizarea ascunde editorul fără să salveze sau să piardă ceva și afișează în stânga-jos o formă compactă (tip, titlu, data creării și a ultimei modificări, Maximizează, Închide); Maximizează sau dublu-click readuce editorul.

Conținutul se păstrează pe paragrafe în `dbo.NoteBlocks` (scriptul `008_CreateNoteBlocks.sql`): un paragraf este textul dintre rânduri goale, nu rândul vizual. Fiecare paragraf are un id stabil (GUID creat de editor) și audit propriu (creare, ultima modificare). `wwwroot/js/note-editor.js` urmărește paragrafele prin editări:

- editarea păstrează id-ul și data creării; se actualizează doar auditul paragrafelor modificate;
- împărțirea păstrează id-ul pe primul fragment, iar fragmentul desprins primește id nou;
- unirea păstrează id-ul primului paragraf;
- ștergerea urmată de undo readuce id-ul paragrafului;
- textul copiat și lipit primește id nou; mutarea prin tăiere și lipire creează deocamdată tot un paragraf nou.

Bara de informații de sub editor afișează, pentru paragraful de sub mouse (sau, fără mouse, pentru cel cu cursorul), data creării și a ultimei modificări din coloanele de audit `NoteBlocks` (`CreatedAtUtc`, `ModifiedAtUtc`), cu „modificări nesalvate” sau „paragraf nou” când este cazul; paragraful descris este evidențiat discret. Textele sunt compuse de server din `.resx` (`NoteDates.BlockAudit`) și actualizate după fiecare salvare.

Salvarea trimite toată nota (paragrafele în ordine) prin POST JSON cu antiforgery (`/?handler=SaveNote&note={id}`). `NoteService` permite salvarea numai proprietarului, validează textul și limitele, iar `NoteRepository` compară paragrafele cu cele stocate (păstrate, modificate, noi, eliminate). `RowVersion` al notei detectează salvările din ferestre diferite: salvarea învechită este refuzată cu un mesaj, fără a suprascrie. Rezultatul fiecărei salvări apare și ca mesaj fix sus, pe centru, deasupra editorului („Nota a fost salvată.” sau eroarea), până îl închide utilizatorul. Membrii contextului pot citi o notă partajată, numai pentru citire. Fără JavaScript pagina afișează paragrafele doar pentru citire.

Biblioteca este inclusă local în `wwwroot/lib/codemirror/codemirror.js`, cu `THIRD-PARTY-NOTICES.txt` (copyright și licențe). Pentru actualizare: din `tools/codemirror`, `npm ci` apoi `npm run build` (versiuni fixate în `package.json` / `package-lock.json`).

Pe tablă, fiecare post-it arată în header data creării în stânga și data ultimei modificări în dreapta, pe același rând (același format, cu anul, fără etichete vizibile; data modificării lipsește cât timp nota nu a fost modificată), începutul primelor paragrafe și, pentru proprietar, titlul editabil pe loc (Enter salvează, Escape anulează) și ștergerea cu confirmare (`/?delete={id}`; paragrafele se șterg în cascadă). `NoteService.RenameAsync` și `DeleteAsync` permit aceste operații numai proprietarului.

Nu sunt încă implementate: referințele CR/bug, linkurile, autocomplete-ul și popup-urile (necesită `WorkReferences`), salvarea automată.

```powershell
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\006_CreateNotes.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\007_AllowSeveralJournalsPerDay.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\008_CreateNoteBlocks.sql'
```

## Modulul de conturi — version_0.01

- /Account/Register: prenume, nume, e-mail unic, parolă și confirmare; după creare se deschide autentificarea.
- /Account/Login: e-mail/parolă, opțiunea „Ține-mă minte”; succesul redirecționează la pagina principală.
- /Account: acces autorizat, modificare nume/prenume; e-mailul este afișat și rămâne nemodificabil.
- /Account/ChangePassword: verifică parola actuală, validează noua parolă și reface sigur sesiunea.
- /Account/Logout: numai POST, cu antiforgery; revine la pagina principală.

Business definește IAccountService și IAuthenticationService, AccountProfile, AccountResult și AccountRules. DataAccess/Identity implementează operațiile prin UserManager/SignInManager, claims pentru nume/prenume; mesajele Identity sunt localizate în Web/Localization. Web conține paginile și ViewModels, validarea client în wwwroot/js/validation.js și configurarea cookie-urilor/middleware. Componenta ApplicationVersion citește versiunea prin serviciul Business pentru footerul tuturor paginilor.

Nu se implementează roluri, recuperare parolă sau confirmare e-mail. Parolele sunt gestionate exclusiv de Identity; nu se jurnalizează. Politica: minimum 12 caractere, literă mare, literă mică, cifră, caracter special. După 5 încercări eșuate contul este blocat 15 minute; mesajul de autentificare este identic pentru cont absent, parolă incorectă și blocare. Cookie-ul persistent durează 14 zile, cu reînnoire; fără „Ține-mă minte” este cookie de sesiune. Schimbarea parolei actualizează security stamp; celelalte sesiuni sunt invalidate la următoarea verificare standard Identity (implicit maximum 30 minute).

### Schema Identity — Database First

Toate modulele folosesc Database First. Schema SQL este sursa de adevăr; nu există migrări EF sau snapshot-uri. AccountsDbContext mapează Users, AspNetUserClaims, AspNetUserLogins și AspNetUserTokens, păstrând integrarea standard Identity. Mapările Identity se actualizează manual după modificarea SQL; nu le suprascrieți prin scaffolding obișnuit. Indexul unic NormalizedEmail păstrează unicitatea e-mailurilor.

Versiunea curentă este 0.01. Regula folderelor: pentru versiunea 0.0x, scripturile se află în E:\GitRepository\Vali\WorkNotes\Scripts\version_0.0x. Un modul nou nu schimbă automat versiunea. Toate scripturile actuale aparțin version_0.01.

Din folderul soluției, aplicați în ordine:

```powershell
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\002_AddIdentityUsers.sql'
```

002 creează numai tabelele/indexurile lipsă, fără dependență de istoric EF; conturile, hashurile și DatabaseVersion rămân intacte. Scriptul poate fi executat repetat. Pentru modificări viitoare folosiți scripturi ALTER dedicate în folderul versiunii curente, apoi aliniați mapările din DataAccess.

Nu folosiți comenzile EF migrations/database update și nu creați schema la pornire prin Migrate/EnsureCreated. Instrumentul dotnet-ef rămâne disponibil pentru reverse engineering Database First al tabelelor obișnuite.

La găzduire folosiți HTTPS, configurarea conexiunii din mediu și chei Data Protection persistente, protejate și comune instanțelor dacă există mai multe. În Development este permis HTTP local; în celelalte medii cookie-ul necesită HTTPS.

Referință: [configurarea Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0).

Aplicație ASP.NET Core Razor Pages pe .NET 10, cu Entity Framework Core 10.0.12 și SQL Server, Database First.

Schema bazei de date este administrată prin scripturi SQL. Modelul EF și DbContext sunt generate din schema existentă prin reverse engineering (scaffolding). Identity păstrează mapări explicite conforme schemei SQL, fără migrări.

## Arhitectură

- **WorkNotes.Web**: prezentare Razor Pages, configurare și compunerea dependency injection în Program.cs. ApplicationVersionViewComponent primește IApplicationVersionService și afișează rezultatul în layout; nu accesează DbContext și nu selectează versiunea curentă.
- **WorkNotes.Business**: interfețe de serviciu și acces la date, plus regula de selectare a versiunii numerice maxime. Nu depinde de Web, DataAccess sau EF Core.
- **WorkNotes.DataAccess**: context și entități generate din SQL Server, repository concret și extensia de înregistrare DI. Implementează contractul definit în Business.
- **WorkNotes.Business.Tests**: teste independente de SQL Server pentru versiuni, rezultat gol, anulare și erori.

Dependențe: Web → Business; Web → DataAccess numai la compunerea DI; DataAccess → Business; Business.Tests → Business. Serviciul, repository-ul și DbContext folosesc lifetime scoped. Toate operațiile de citire propagă CancellationToken.

Flux: `ApplicationVersionViewComponent → IApplicationVersionService → ApplicationVersionService → IApplicationVersionRepository → ApplicationVersionRepository → WorkNotesDbContext`.

Regulile obligatorii pentru modificări viitoare sunt în [AGENTS.md](AGENTS.md).

## Structură

```text
E:\GitRepository\Vali\WorkNotes\       # Rădăcina Git
├── .gitignore
├── Solution\                          # Folderul soluției
│   ├── WorkNotes.sln
│   ├── AGENTS.md
│   ├── dotnet-tools.json              # dotnet-ef 10.0.12, instrument local
│   ├── README.md
│   ├── docs\design-system.md
│   ├── tools\Test-Resources.ps1
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
└── Scripts\                           # Scripturile SQL, alături de Solution
    └── version_0.01\
        ├── 000_CreateDatabaseVersion.sql
        ├── 001_InsertDatabaseVersion.sql
        ├── 002_AddIdentityUsers.sql
        ├── 004_CreateWorkContexts.sql
        ├── 005_CreateContextMembers.sql
        ├── 006_CreateNotes.sql
        ├── 007_AllowSeveralJournalsPerDay.sql
        └── 008_CreateNoteBlocks.sql
```

Folderele `Scripts` și `Solution` fac parte din același repository Git. Comenzile dotnet se rulează din `Solution`, iar scripturile se referă de acolo ca `..\Scripts\version_0.01\...`.

## Conexiunea locală

În `WorkNotes.Web/appsettings.json`, cheia `ConnectionStrings:WorkNotes`:

```text
Server=localhost\MSSQLSERVER02;Database=WorkNotes.db;Integrated Security=True;Encrypt=True;TrustServerCertificate=True
```

Conexiunea utilizează identitatea Windows a procesului. Aceasta trebuie să aibă drept de citire pentru aplicație și drepturi de modificare a schemei când se execută scripturile SQL. `TrustServerCertificate=True` este configurat pentru instanța locală.

## Restaurare și compilare

Din PowerShell:

```powershell
Set-Location 'E:\GitRepository\Vali\WorkNotes\Solution'
dotnet tool restore
dotnet restore WorkNotes.sln
dotnet build WorkNotes.sln --no-restore
dotnet test WorkNotes.sln --no-build --no-restore
```

## Pregătirea bazei de date prin SQL

Baza `WorkNotes.db` trebuie să existe pe instanța SQL Server. Execută scripturile din `..\Scripts\version_0.01` în ordinea numelor, în SQL Server Management Studio, conectat prin Windows Authentication la `localhost\MSSQLSERVER02`:

1. `000_CreateDatabaseVersion.sql`: creează `dbo.DatabaseVersion` numai dacă lipsește, cu `Version nvarchar(50) NOT NULL` și cheia primară `PK_DatabaseVersion`.
2. `001_InsertDatabaseVersion.sql`: inserează `v.0.01` numai dacă lipsește.
3. `002_AddIdentityUsers.sql`: creează tabelele și indexurile Identity lipsă.
4. `004_CreateWorkContexts.sql`: creează `dbo.WorkContexts` și indexul unic pe `Name`, dacă lipsesc.
5. `005_CreateContextMembers.sql`: creează `dbo.ContextMembers`, cheile externe către `WorkContexts` și `Users` și indexul pe `UserId`, dacă lipsesc.
6. `006_CreateNotes.sql`: creează `dbo.Notes`, constrângerile și indexurile, dacă lipsesc.
7. `007_AllowSeveralJournalsPerDay.sql`: elimină indexul unic `UX_Notes_DailyJournal`, astfel încât sunt permise mai multe jurnale pe zi.
8. `008_CreateNoteBlocks.sql`: creează `dbo.NoteBlocks` (paragrafele notelor, cu audit) și indexul pe `NoteId`, `Position`, dacă lipsesc.

Alternativ, dacă `sqlcmd` este instalat:

```powershell
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\000_CreateDatabaseVersion.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\001_InsertDatabaseVersion.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\002_AddIdentityUsers.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\004_CreateWorkContexts.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\005_CreateContextMembers.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\006_CreateNotes.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\007_AllowSeveralJournalsPerDay.sql'
sqlcmd -S 'localhost\MSSQLSERVER02' -d 'WorkNotes.db' -E -C -b -i '..\Scripts\version_0.01\008_CreateNoteBlocks.sql'
```

Scripturile de creare păstrează tabelele și datele existente. Modificările ulterioare ale structurii se fac prin scripturi ALTER dedicate. La inserare, tranzacția, blocarea verificării și cheia primară previn duplicatele, inclusiv la executări concurente.

## Actualizarea modelului EF — Database First

După modificarea schemei prin SQL, regenerează clasele din baza de date:

```powershell
dotnet tool restore
dotnet ef dbcontext scaffold 'Name=ConnectionStrings:WorkNotes' Microsoft.EntityFrameworkCore.SqlServer --project WorkNotes.DataAccess --startup-project WorkNotes.Web --context WorkNotesDbContext --context-dir Context --output-dir Entities --namespace WorkNotes.DataAccess.Entities --context-namespace WorkNotes.DataAccess.Context --table dbo.DatabaseVersion --table dbo.WorkContexts --table dbo.ContextMembers --table dbo.Notes --table dbo.NoteBlocks --no-onconfiguring --force
dotnet build WorkNotes.sln
dotnet test WorkNotes.sln --no-build --no-restore
```

Comanda folosește Web pentru pornire și citirea configurației, dar generează fișierele numai în DataAccess. `--no-onconfiguring` păstrează conexiunea în configurație, fără să o scrie în clasele generate. Program.cs transmite connection string-ul extensiei AddDataAccess; această extensie înregistrează DbContext și providerul SQL Server.

`--force` suprascrie fișierele generate `WorkNotes.DataAccess/Context/WorkNotesDbContext.cs`, `WorkNotes.DataAccess/Entities/DatabaseVersion.cs`, `WorkNotes.DataAccess/Entities/WorkContext.cs`, `WorkNotes.DataAccess/Entities/ContextMember.cs` `WorkNotes.DataAccess/Entities/Note.cs` și `WorkNotes.DataAccess/Entities/NoteBlock.cs`. Scaffolding-ul scrie fișierele cu CRLF; repository-ul folosește LF. Extensiile scrise manual se pun în fișiere partial separate. Pentru tabele viitoare, extinde lista de opțiuni `--table` astfel încât regenerarea contextului să includă toate entitățile necesare.

Pachetul EF Core Design este referit cu PrivateAssets=all în DataAccess și în Web (startup project), exclusiv pentru tooling. Providerul SQL Server este referit direct de DataAccess. Business nu are pachete EF. Instrumentul local dotnet-ef este păstrat pentru scaffolding. Niciun context nu folosește migrări EF sau istoric de migrări. Aplicația nu creează sau modifică schema și nu inserează versiuni la pornire.

## Rularea aplicației

```powershell
dotnet run --project WorkNotes.Web --launch-profile http
```

Deschide http://localhost:5018. Pagina are header, body și footer; versiunea din baza de date este afișată în dreapta footerului, la baza paginii.

Tabela goală afișează „Versiune neconfigurată”. Schema trebuie aplicată înainte de rulare; erorile de conexiune nu sunt tratate ca tabelă goală.

Întrucât tabela existentă nu conține o dată de instalare, „versiunea curentă” înseamnă cea mai mare versiune numerică înregistrată, cu formatul `v.major.minor[.build[.revision]]`. Textul original se afișează nemodificat: `v.0.01`. Compararea este numerică, astfel că `v.0.10` urmează după `v.0.9`. Etichetele care nu se pot interpreta numeric sunt ordonate la finalul priorității, determinist, după text.

Documentație: [EF Core SQL Server](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/), [comenzile EF Core, inclusiv dbcontext scaffold](https://learn.microsoft.com/en-us/ef/core/cli/dotnet).
