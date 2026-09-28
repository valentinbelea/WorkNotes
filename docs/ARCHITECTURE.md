# Arhitectura WorkNotes

Harta a ceea ce există în cod pe `main`. Regulile obligatorii sunt în [AGENTS.md](../AGENTS.md#arhitectură-și-dependențe); decizia de arhitectură este [ADR-001](decisions/ADR-001-solution-architecture.md), iar editorul este descris în [ADR-002](decisions/ADR-002-note-editor.md).

## Proiectele soluției

| Proiect | Responsabilitate | Conținut |
| --- | --- | --- |
| `WorkNotes.Web` | Prezentare Razor Pages, configurare, pornire, compunerea DI | `Program.cs`, `Pages/`, `Pages/Shared/` (layout, partiale), `ViewComponents/`, `ViewModels/`, `Localization/`, `Messages/`, `Navigation/`, `Notes/` (formate și clase de prezentare), `wwwroot/` (css, js, images, lib/codemirror) |
| `WorkNotes.Business` | Reguli, servicii, modele și contracte de acces la date | `Abstractions/` (interfețe de servicii și repository-uri), `Services/`, `Models/` (DTO-uri `record`, reguli `*Rules`, coduri de stare `*Status`, constante `NoteTypes`, `NoteVisibilities`, `ContextRoles`) |
| `WorkNotes.DataAccess` | Persistență SQL Server prin EF Core, implementarea Identity, înregistrarea DI | `Context/` (`WorkNotesDbContext` generat, `AccountsDbContext` manual), `Entities/`, `Repositories/`, `Identity/`, `DependencyInjection.cs` (`AddDataAccess`) |
| `WorkNotes.Resources` | Catalogul de texte localizate | `Resources/SharedResources.cs` (clasa marker) și `SharedResources.resx` / `.ro` / `.en` / `.pl` |
| `WorkNotes.Business.Tests` | Teste xUnit pentru Business, fără bază de date | `*Tests.cs`, cu stub-uri scrise manual |

Pachete NuGet: DataAccess — `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design` (numai tooling), toate 10.0.12, plus `FrameworkReference Microsoft.AspNetCore.App`; Web — `Microsoft.EntityFrameworkCore.Design` 10.0.12 cu `PrivateAssets=all`; Business și Resources — niciun pachet; testele — vezi [TESTING.md](TESTING.md).

## Direcția dependențelor

```text
WorkNotes.Web ─────► WorkNotes.Business ◄───── WorkNotes.DataAccess
   │   └──────────► WorkNotes.Resources
   └──────────────► WorkNotes.DataAccess   (numai Program.cs → AddDataAccess)
WorkNotes.Business.Tests ─► WorkNotes.Business
```

Business nu are nicio referință de proiect sau pachet. Interfețele de acces la date sunt definite în Business și implementate în DataAccess (inversarea dependențelor).

## Servicii, interfețe și repository-uri

| Contract (`WorkNotes.Business/Abstractions`) | Implementare | Folosit de |
| --- | --- | --- |
| `IApplicationVersionService` | `WorkNotes.Business/Services/ApplicationVersionService.cs` | `ApplicationVersionViewComponent` (footer) |
| `IWorkContextService` | `WorkNotes.Business/Services/WorkContextService.cs` | `Pages/Contexts/Index.cshtml`, `Pages/Index.cshtml` |
| `IContextMemberService` | `WorkNotes.Business/Services/ContextMemberService.cs` | `Pages/Contexts/Index.cshtml` |
| `INoteService` | `WorkNotes.Business/Services/NoteService.cs` | `Pages/Index.cshtml` |
| `IAccountService` | `WorkNotes.DataAccess/Identity/IdentityAccountService.cs` | `Pages/Account` (Register, Index, ChangePassword) |
| `IAuthenticationService` | `WorkNotes.DataAccess/Identity/IdentityAccountService.cs` | `Pages/Account` (Login, Logout) |
| `IApplicationVersionRepository` | `WorkNotes.DataAccess/Repositories/ApplicationVersionRepository.cs` | `ApplicationVersionService` |
| `IWorkContextRepository` | `WorkNotes.DataAccess/Repositories/WorkContextRepository.cs` | `WorkContextService`, `ContextMemberService`, `NoteService` |
| `IContextMemberRepository` | `WorkNotes.DataAccess/Repositories/ContextMemberRepository.cs` | `ContextMemberService` |
| `INoteRepository` | `WorkNotes.DataAccess/Repositories/NoteRepository.cs` | `NoteService` |

Contractele de cont sunt implementate direct în DataAccess, peste `UserManager` / `SignInManager`; regulile independente de infrastructură sunt în `WorkNotes.Business/Models/AccountRules.cs`. Serviciile Business verifică apartenența la context și proprietatea prin `IWorkContextRepository` și `INoteRepository` înainte de orice modificare.

## Dependency injection

`Program.cs` (Web):

- localizarea: `AddLocalization`, `RequestLocalizationOptions` din `LocalizationConfiguration`, `IStringLocalizer` fără parametru generic legat (singleton) la `IStringLocalizer<SharedResources>`, `LocalizedMvcOptions` pentru mesajele de model binding, `AddRazorPages().AddDataAnnotationsLocalization` cu catalogul `SharedResources`;
- `AddProblemDetails`, autentificarea cu cookie-urile Identity (`AddIdentityCookies`), `AddAuthorization` și `ConfigureApplicationCookie` (vezi [SECURITY.md](SECURITY.md));
- serviciile Business, scoped: `IApplicationVersionService`, `IWorkContextService`, `IContextMemberService`, `INoteService`; `TimeProvider.System` ca singleton;
- `AddDataAccess(connectionString)`, cu `ConnectionStrings:WorkNotes` obligatoriu (excepție la pornire dacă lipsește);
- `IdentityErrorDescriber` → `LocalizedIdentityErrorDescriber`, scoped.

`AddDataAccess` (DataAccess): `WorkNotesDbContext` și `AccountsDbContext` cu `UseSqlServer` pe același connection string; repository-urile, scoped; `AddIdentityCore<ApplicationUser>` cu politica de parolă și blocare, `AddEntityFrameworkStores<AccountsDbContext>`, `AddSignInManager`, `AccountClaimsPrincipalFactory` (adaugă prenumele și numele în claims); `IdentityAccountService` scoped, expus prin `IAccountService` și `IAuthenticationService`.

Pipeline-ul HTTP, în ordine: `UseRequestLocalization` → în afara Development: `UseExceptionHandler`, `UseHsts`, `UseHttpsRedirection` → `UseAuthentication` → `UseAuthorization` → `MapStaticAssets` → `MapRazorPages().WithStaticAssets()`.

## DTO-uri și ViewModel-uri

- Modelele Business (`WorkNotes.Business/Models`) sunt `sealed record`-uri imutabile: `WorkContext`, `ContextMemberDetails`, `AccountProfile`, `AccountResult`, `NewNote`, `NoteSummary`, `NoteDocument`, `NoteBlockDetails`, `NoteBlockInput`, `NoteBlockAudit`, `NoteChanges`, `NoteMonthGroup`, `NoteSaveResult`, `NoteRenameResult`, `NoteOrderResult`, `NoteVersionChange`. Ele circulă între Business, DataAccess și Web; paginile și partialele le afișează direct (de exemplu `_NoteCard.cshtml` primește un `NoteSummary`, editorul un `NoteDocument`).
- ViewModel-urile Web (`WorkNotes.Web/ViewModels`) sunt numai pentru intrări, cu DataAnnotations ale căror mesaje sunt chei .resx: `LoginInput`, `RegisterInput` (extinde `ProfileInput`), `ProfileInput`, `ChangePasswordInput`, `WorkContextInput`, `ContextMemberInput`, `NewNoteInput`; `NoteSaveRequest` / `NoteBlockRequest` sunt corpul JSON trimis de editor.
- Entitățile EF (`WorkNotes.DataAccess/Entities`) nu ies din DataAccess: repository-urile le proiectează în modele Business și folosesc alias-uri (`using NoteEntity = WorkNotes.DataAccess.Entities.Note;`) acolo unde numele coincid.
- Ajutoarele de prezentare din Web: `Notes/NoteDates.cs` (formatele datelor și textele de audit), `Notes/NoteCardStyle.cs` (clasele de culoare și înclinare), `Navigation/NavigationSections.cs` (subtitlul din header și grupul de meniu deschis), `Messages/StatusMessage*.cs` (mesajele de salvare prin TempData).

## Accesul la date

- Două contexte pe aceeași bază: `WorkNotesDbContext` (generat prin scaffolding: `ContextMembers`, `DatabaseVersion`, `Notes`, `NoteBlocks`, `WorkContexts`) și `AccountsDbContext` (`IdentityUserContext<ApplicationUser>`, mapare manuală a `Users` și a tabelelor `AspNetUser*`). `ContextMemberRepository` citește membrii din primul și conturile din al doilea, prin interogări separate.
- Citirile folosesc `AsNoTracking` și proiecții în modele Business; previzualizarea cardurilor citește în SQL numai primele 3 paragrafe (câte 300 de caractere), iar `NoteRules.BuildPreview` construiește textul.
- Scrierile folosesc entități urmărite și `SaveChangesAsync` (contexte, membri, note noi, salvarea editorului) sau instrucțiuni set-based `ExecuteUpdateAsync` / `ExecuteDeleteAsync` (redenumire, ștergere, eliminarea unui membru, schimbul ordinii).
- Filtrele de acces sunt aplicate în fiecare interogare: `ForMember(userId)` pentru contexte și `VisibleTo(userId)` pentru note (nearhivate, în contexte în care utilizatorul este membru, proprii sau cu vizibilitatea `Context`); modificările filtrează și după proprietar.
- Tranzacții explicite: crearea unei note (citirea `MIN([Order])` cu `UPDLOCK, HOLDLOCK`) și schimbul a două note (un singur `UPDATE` condiționat de `RowVersion`, apoi citirea noilor versiuni). Concurența, cheile și regulile de ștergere sunt în [DATABASE.md](DATABASE.md).
- Erorile SQL așteptate sunt traduse în coduri de stare: 2601/2627 (unicitate) → nume duplicat, membru existent sau e-mail folosit; 547 (cheie externă) → context în uz, cont sau context dispărut; `DbUpdateConcurrencyException` → conflict.
- Valorile `datetime2` citite primesc `DateTimeKind.Utc`; auditul este salvat la precizia de o secundă a coloanelor.

## Tratarea erorilor

- Rezultatele așteptate sunt coduri de stare Business (`NoteSaveStatus`, `WorkContextSaveStatus` etc.), nu excepții. Web le transformă: `NotFound` → 404, `Forbidden` → 403 (`StatusCode(403)`, nu `Forbid()`, care ar redirecționa la autentificare), erorile de validare → `ModelState` cu mesaje din .resx, succesul → cheia mesajului în TempData și redirect (Post/Redirect/Get).
- O resursă din afara contextelor utilizatorului răspunde 404, fără a-i confirma existența; o acțiune rezervată proprietarului, cerută de un membru, răspunde 403.
- Handlerele JSON (`SaveNote`, `SwapNotes`, `RenameNote` cu `Accept: application/json`) răspund `{ "message": "…" }` localizat, cu 400, 401, 403, 404 sau 409.
- Erorile de programare (de exemplu `ArgumentException` pentru un utilizator lipsă) și cele neașteptate se propagă. În afara Development, `UseExceptionHandler()` împreună cu `AddProblemDetails()` produce un răspuns ProblemDetails; în Development se folosește pagina de excepții pentru dezvoltatori, implicită în ASP.NET Core. Nu există pagini proprii pentru 404/403/500 (vezi [CURRENT-STATUS.md](CURRENT-STATUS.md#probleme-cunoscute)).
- Anularea (`OperationCanceledException`) se propagă; serviciile verifică tokenul înainte de accesul la date. Datele lipsă nu sunt tratate ca erori și invers.

## Logging

- Se folosește numai configurația implicită ASP.NET Core: `Logging:LogLevel` cu `Default = Information` și `Microsoft.AspNetCore = Warning`, în `appsettings.json` și `appsettings.Development.json`. Codul aplicației nu folosește `ILogger` și nu configurează furnizori proprii.
- Restricțiile obligatorii (fără parole, fără corpurile cererilor de cont, mesaje tehnice nelocalizate) sunt în [AGENTS.md](../AGENTS.md#identitate-și-autentificare).
- TODO: Necesită clarificare — strategia de logging pentru mediile găzduite (destinație, nivel, retenție).

## Fluxul unei cereri web

Afișarea tablei, `GET /?context=5`:

1. Middleware: limba din cookie, autentificarea din cookie-ul `WorkNotes.Auth`, autorizarea.
2. `IndexModel.OnGetAsync` → `IWorkContextService.GetForMemberAsync` → `WorkContextRepository` (filtrul `ForMember`); un ID necunoscut sau străin revine la primul context.
3. `INoteService.GetBoardAsync` → `NoteRepository.GetBoardAsync` (filtrul `VisibleTo`) → `NoteService` grupează pe luni locale și ordonează în fiecare lună.
4. `Index.cshtml` randează lunile și cardurile (`_NoteCard`); layout-ul randează meniul, selectorul de limbă, mesajul din TempData și footerul, unde `ApplicationVersionViewComponent` citește versiunea prin `IApplicationVersionService`.

Salvarea din editor, `POST /?handler=SaveNote&note={id}` cu corp JSON:

1. Razor Pages validează tokenul antiforgery din antetul `RequestVerificationToken`.
2. `IndexModel.OnPostSaveNoteAsync` → `INoteService.SaveAsync`: validează titlul și paragrafele (`NoteRules`), verifică vizibilitatea, proprietarul și versiunea.
3. `NoteRepository.SaveAsync` compară paragrafele cu cele salvate și scrie numai diferențele, cu verificarea `RowVersion`.
4. Răspunsul JSON conține noua versiune, mesajul localizat, data ultimei modificări și textele de audit ale paragrafelor; o eroare conține numai mesajul.

Fluxurile principale:

```text
Pages/Index    → INoteService → NoteService → INoteRepository → NoteRepository → WorkNotesDbContext
Pages/Contexts → IWorkContextService → WorkContextService → IWorkContextRepository → WorkContextRepository → WorkNotesDbContext
Pages/Contexts → IContextMemberService → ContextMemberService → IContextMemberRepository → ContextMemberRepository → WorkNotesDbContext (ContextMembers) + AccountsDbContext (Users)
Pages/Account  → IAccountService / IAuthenticationService → IdentityAccountService → UserManager / SignInManager → AccountsDbContext
Footer         → ApplicationVersionViewComponent → IApplicationVersionService → … → DatabaseVersion
```

`NoteService` și `ContextMemberService` verifică apartenența și rolul `Owner` prin `IWorkContextRepository` înainte de citire sau modificare. Fluxul versiunii este detaliat în [AGENTS.md](../AGENTS.md#fluxul-versiunii-aplicației).

## Pagini și adrese

| Adresă | Ce face |
| --- | --- |
| `/` | Tabla contextului ales (utilizator autentificat) sau pagina de bun venit (vizitator) |
| `/?context={id}` | Tabla unui context |
| `/?new=true` | Cardul „Notă nouă” deschis fără JavaScript |
| `/?note={id}` | Editorul peste tablă, cu nota într-un tab |
| `/?delete={id}` | Confirmarea ștergerii unei note |
| `/?handler=NoteTab&note={id}` (GET) | Un tab nou pentru editorul deja deschis (fragment HTML) |
| `/?handler=SaveNote&note={id}` (POST JSON) | Salvarea unei note din editor |
| `/?handler=CreateNote`, `RenameNote`, `DeleteNote` (POST) | Crearea, redenumirea pe loc, ștergerea; adresele deschise cu GET revin la tablă |
| `/?handler=SwapNotes` (POST, răspuns JSON) | Schimbul a două note din aceeași lună; răspunde cu ordinea lunii și noile versiuni |
| `/Contexts`, `?add=true`, `?edit={id}`, `?delete={id}`, `?members={id}` | Contextele și overlay-urile lor; handlerele POST `Delete`, `AddMember`, `RemoveMember` |
| `/Account/Register`, `/Account/Login`, `/Account`, `/Account/ChangePassword`, `/Account/Logout` | Conturile; deconectarea numai prin POST |
| `/Language` (POST) | Schimbarea limbii |

Starea overlay-urilor este în URL: fiecare dialog se poate deschide și fără JavaScript, iar `modal.js` îl transformă în dialog modal.

## Componente Web

| Element | Rol |
| --- | --- |
| `Pages/Shared/_Layout.cshtml`, `_MainMenu.cshtml`, `_LanguageSelector.cshtml` | Header, meniul sertar, selectorul de limbă, zona de mesaje, footerul |
| `Pages/Shared/_NoteCard.cshtml`, `_NewNoteCard.cshtml` | Post-it-urile de pe tablă (notă salvată, notă nouă) |
| `Pages/Shared/_NoteEditorDialog.cshtml` | Fereastra editorului: header cu sigla, taburile, Minimizează, Închide; forma minimizată |
| `_NoteEditorTabButton`, `_NoteEditorTabPanel`, `_NoteEditorTab` | Un tab al editorului: butonul, panoul notei, fragmentul pentru un tab adăugat |
| `_DeleteNoteDialog.cshtml` | Confirmarea ștergerii unei note |
| `_StatusMessage.cshtml` și `WorkNotes.Web/Messages` | Mesajele de salvare (succes, avertisment, eroare) și transportul lor prin TempData |
| `Shared/Components/ApplicationVersion/Default.cshtml` | Versiunea din footer |

## JavaScript

Numai comportament; aspectul vine din clase CSS ([CODING-STANDARDS.md](CODING-STANDARDS.md#javascript)).

| Fișier | Încărcare | Rol |
| --- | --- | --- |
| `notes-board.js` | layout, modul ES | Schimbarea contextului, cardul „Notă nouă”, redenumirea pe loc, deschiderea notelor (în editorul deschis, dacă există), schimbul a două carduri prin drag-and-drop |
| `note-editor.js` | pagina principală, modul ES, numai cu editorul deschis | Câte un CodeMirror pe tab, identitatea paragrafelor, salvarea, taburile, minimizarea; primește noile versiuni după un schimb (`note-board:versions`) |
| `status-messages.js` | importat de cele două module | Afișarea mesajelor de salvare din template-urile randate de server |
| `modal.js`, `navigation.js`, `language.js`, `validation.js` | layout, `defer` | Dialogurile modale, meniul, selectorul de limbă, validarea client |
| `lib/codemirror/codemirror.js` | importat de `note-editor.js` | CodeMirror 6, construit din `Solution/tools/codemirror` |

## CSS

Ordinea de încărcare din layout: `tokens.css` (paleta și variabilele), `site.css` (componente comune, mesaje, dialoguri), `navigation.css`, `notes-board.css` (tabla), `postit.css` (hârtia și banda), `note-editor.css` (editorul). Ghidul vizual: [UI-UX.md](UI-UX.md).

## Verificarea regulilor arhitecturale

Comenzi reproductibile din rădăcina repository-ului; fiecare trebuie să nu returneze nimic:

```powershell
# Web nu folosește EF, DbContext sau tipurile Identity în cod și în view-uri
git grep -n -E "EntityFrameworkCore|DbContext|DbSet|UserManager|SignInManager|ApplicationUser" -- 'Solution/WorkNotes.Web/*.cs' 'Solution/WorkNotes.Web/*.cshtml'
# Business nu are referințe de pachete sau proiecte
git grep -n -E "PackageReference|ProjectReference" -- Solution/WorkNotes.Business/WorkNotes.Business.csproj
# Fără stiluri inline în view-uri și în scripturi
git grep -n -E "style=|\.style\." -- 'Solution/WorkNotes.Web/*.cshtml' 'Solution/WorkNotes.Web/wwwroot/js/*.js'
```

Singura referință la DataAccess din Web este `using WorkNotes.DataAccess;` din `Program.cs`, pentru `AddDataAccess`. Comenzile au fost rulate pe 2026-09-25, fără rezultate.
