# Reguli obligatorii pentru WorkNotes

Acest fișier se aplică întregului repository — `Scripts/` (scripturile SQL), `Solution/` (soluția .NET) și documentația — și tuturor modificărilor viitoare. Citiți-l înainte de a modifica proiectul. Cerințele explicite ale utilizatorului au prioritate; nu deduceți aprobarea unor funcționalități noi din documentele de analiză, din planuri sau din existența unor biblioteci.

Regulile obligatorii sunt definite aici, o singură dată. Documentele din [docs](docs) descriu implementarea și trimit la secțiunile de mai jos; indexul documentației este în [README.md](README.md#documentație), iar instrucțiunile specifice Claude Code sunt în [CLAUDE.md](CLAUDE.md). Toate căile sunt relative la rădăcina repository-ului.

## Arhitectură și dependențe

Soluția este `Solution/WorkNotes.sln`. Toate proiectele rămân compatibile cu .NET 10 (`net10.0`).

| Proiect | Responsabilitate | Dependențe permise |
| --- | --- | --- |
| WorkNotes.Web | Razor Pages, controllere dacă devin necesare, view models, layout, CSS, JavaScript, prezentare, configurare și pornire | Business, Resources; DataAccess numai pentru compunerea DI |
| WorkNotes.Business | Reguli, servicii, modele/DTO-uri și contracte de acces la date | Biblioteci independente de infrastructură; nici Web, nici DataAccess, nici EF Core, ASP.NET sau Identity |
| WorkNotes.DataAccess | DbContext, entități și mapări EF, repository-uri, integrare SQL Server, implementarea contractelor Identity și înregistrarea lor în DI | Business și bibliotecile de persistență |
| WorkNotes.Resources | Resurse .resx comune, complete în ro/en/pl, și clasa marker `SharedResources` | Fără dependențe către Web, Business sau DataAccess |
| WorkNotes.Business.Tests | Teste pentru regulile și contractele Business | Business și infrastructura de testare; fără bază de date obligatorie |

- Direcția referințelor este Web → Business, Web → Resources, Web → DataAccess numai pentru compunere, DataAccess → Business, Business.Tests → Business. Business nu referă Web sau DataAccess. Sunt interzise dependențele circulare.
- Referința Web către DataAccess se utilizează numai în punctul de compunere `Program.cs`, prin extensia `AddDataAccess`. Nu injectați și nu folosiți `WorkNotesDbContext`, `AccountsDbContext`, `DbContext`, `DbSet`, repository-uri concrete, SQL ori clienți de baze de date în pagini, controllere, view models, view-uri sau alte componente Web. Înregistrarea DbContext și configurarea providerului rămân în DataAccess.
- Excepție strict de tooling: Web păstrează `Microsoft.EntityFrameworkCore.Design`, cu `PrivateAssets=all`, deoarece este startup project pentru reverse engineering. Aceasta nu autorizează cod EF în Web. Providerul SQL Server se referă direct numai din DataAccess.
- Business și DataAccess transmit chei de mesaj și coduri de stare, fără dependențe noi de infrastructura de localizare. `LocalizedIdentityErrorDescriber` este în `WorkNotes.Web/Localization` și se înregistrează în `Program.cs`.
- Regulile de business (validări de domeniu, normalizări, calcule, selecții, drepturile asupra contextelor și notelor) aparțin Business. Este interzisă plasarea lor în pagini sau controllere, în view-uri Razor și în JavaScript. JavaScript adaugă numai comportament de interfață; serverul rămâne autoritar.

## SOLID, interfețe și dependency injection

- Fiecare clasă are o responsabilitate clară, conform principiilor SOLID. Prezentarea, regulile de business și persistența sunt separate.
- Pagina/controllerul orchestrează apelul serviciului și pregătește prezentarea. Nu include selecția versiunii, calcule sau alte reguli de business.
- Serviciile de business sunt consumate prin interfețe, injectate prin constructor. Business depinde de interfețe de acces la date definite în Business (`WorkNotes.Business/Abstractions`).
- DataAccess implementează aceste interfețe. Contractele expun tipuri simple sau DTO-uri Business; nu expun entități EF, DbContext, DbSet, IQueryable ori tipuri SQL.
- Respectați substituibilitatea implementărilor și semantica rezultatelor, excepțiilor și anulării. Interfețele trebuie să fie mici și orientate spre utilizatorii lor.
- Înregistrați implementările explicit prin DI. Pentru DbContext, repository-uri și serviciile care le utilizează folosiți lifetime scoped. Nu capturați servicii scoped într-un singleton.
- Nu folosiți service locator, instanțiere manuală de DbContext în fluxurile aplicației ori clase statice care ascund dependențe.
- Extindeți modelul prin responsabilități și contracte utile; nu introduceți repository generic, Unit of Work propriu peste EF, mediator, fabrici sau proiecte suplimentare fără necesitate demonstrabilă.
- Datele lipsă și erorile sunt cazuri distincte. O eroare SQL nu devine automat „nu există versiune” sau „nu există date”. Mesajele destinate utilizatorului aparțin Web.

## Acces asincron și anulare

- Accesul la baza de date este asincron: de exemplu `ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync` sau `ExecuteUpdateAsync`, după caz.
- Propagați `CancellationToken` de la request prin serviciu și repository până la EF. Nu înlocuiți tokenul primit cu `CancellationToken.None`.
- Evitați `.Result`, `.Wait()`, `async void` și `Task.Run` pentru I/O de bază de date.
- Utilizați `AsNoTracking` pentru interogări exclusiv de citire și proiectați doar câmpurile necesare.
- Nu efectuați operații concurente pe aceeași instanță DbContext. Nu interceptați anularea ca succes sau rezultat gol.

## Configurare și denumire

- Connection string-ul este citit de Web din `ConnectionStrings:WorkNotes` și transmis înregistrării DataAccess. Nu hardcodați servere, nume de baze, credențiale, URL-uri de mediu sau connection string-uri în clase.
- Folosiți `appsettings.json`, `appsettings.{Environment}.json` și suprascrieri prin configurația mediului. Credențialele și alte secrete nu se salvează în Git; folosiți User Secrets sau variabile de mediu.
- Configurația locală curentă folosește Windows Authentication și `TrustServerCertificate=True`. Nu impuneți automat această opțiune pentru găzduirea viitoare.
- Clasele, interfețele, metodele și proprietățile au nume descriptive PascalCase. Interfețele au prefix `I`, metodele asincrone au sufix `Async`, variabilele și parametrii sunt camelCase.
- Numele fișierului corespunde tipului principal. Namespace-urile urmează proiectul și folderele.
- Foldere: Business — `Abstractions`, `Services`, `Models` (un folder `Dtos` separat numai când devine necesar); DataAccess — `Context`, `Entities`, `Repositories`, `Identity`, mapări manuale separate dacă devin necesare; Web — `Pages`, `Pages/Shared`, `ViewModels`, `ViewComponents`, `wwwroot` și folderele de prezentare existente (`Localization`, `Messages`, `Navigation`, `Notes`); `Controllers` numai când există un caz real.
- Păstrați nullable reference types activate. Nu adăugați DTO-uri, interfețe sau clase care doar multiplică aceeași structură fără utilitate.

## EF Core și schema SQL (Database First)

Abordarea curentă, cerută explicit, este Database First. Schema SQL este sursa pentru entitățile și contextul EF; regulile de business rămân în Business. Comanda completă de scaffolding și detaliile schemei sunt în [docs/DATABASE.md](docs/DATABASE.md).

1. Pregătiți modificarea schemei prin scripturi SQL explicite, ordonate în folderul versiunii curente (vezi [Scripturi SQL și versiuni](#scripturi-sql-și-versiuni)).
2. Aplicați scripturile numai la cererea explicită a utilizatorului și numai asupra bazei indicate pentru sarcină; nu modificați alte baze sau medii implicit. Nu aplicați automat scripturi SQL sau alte actualizări ale bazei de date.
3. Regenerați clasele în DataAccess prin scaffolding, cu Web ca startup project și `Name=ConnectionStrings:WorkNotes`.
4. Folosiți `--no-onconfiguring`. Configurația conexiunii rămâne în aplicație, nu în codul generat.
5. Păstrați particularizările manuale în fișiere partial sau clase separate. `--force` suprascrie fișierele generate; inspectați modificările și includeți toate tabelele necesare în selecția de scaffolding.

- Database First este obligatoriu pentru TOATE modulele, inclusiv Identity și `AccountsDbContext`. Nu creați migrări EF, snapshot-uri sau tabele de istoric (`__EFMigrationsHistory` / `__IdentityMigrationsHistory`). Nu folosiți `dotnet ef migrations` / `dotnet ef database update` și nu apelați `Migrate` / `EnsureCreated`. Schema se creează și se modifică exclusiv prin scripturi SQL explicite; aplicația nu creează și nu modifică schema și nu inserează versiuni la pornire.
- Pentru Identity păstrați clasele standard și moștenirea `IdentityUserContext<ApplicationUser>`; actualizați manual mapările conform schemei SQL, fără scaffolding care suprascrie integrarea Identity.
- EF Core și instrumentul local `dotnet-ef` rămân pe versiuni compatibile cu .NET 10. Actualizările majore necesită o cerință explicită și verificarea compatibilității.

## Scripturi SQL și versiuni

- Rădăcina repository-ului conține `Scripts` (scripturile SQL) și `Solution` (soluția, proiectele și `tools`). Comenzile `dotnet` se rulează din `Solution`; de acolo scripturile se află la `..\Scripts\version_0.0x`.
- Regula folderelor: versiunea curentă 0.0x înseamnă folderul `Scripts/version_0.0x`. Versiunea curentă este indicată în [docs/VERSIONING.md](docs/VERSIONING.md#versiunea-curentă) (la această actualizare: 0.02, eticheta din bază `v.0.02`, folderul `Scripts/version_0.02`).
- TOATE scripturile noi, inclusiv Identity și scripturile de tranziție, se pun în folderul versiunii curente. Nu incrementați versiunea și nu creați un folder de versiune nouă doar pentru un modul sau o modificare; schimbarea versiunii necesită solicitare explicită.
- Păstrați numerotarea ordonată a scripturilor și evitați suprascrierea altor scripturi. Convențiile, ordinea de aplicare și verificările sunt în [Scripts/README.md](Scripts/README.md).
- Nu modificați retroactiv scripturile deja livrate (integrate în `main`). O corecție sau o schimbare ulterioară de structură se face printr-un script nou.
- Scriptul de creare păstrează tabelele existente; schimbările de structură folosesc scripturi `ALTER` dedicate. Scripturile de inserare a versiunilor evită duplicatele și păstrează datele existente. Folosiți tranzacții acolo unde atomicitatea este necesară.
- Nu înlocuiți sau ștergeți tabele/date pentru a rezolva o incompatibilitate de model fără solicitare explicită.
- Păstrați scriptul și versiunea `v.0.01`; `v.0.02` se adaugă ca rând nou prin `version_0.02/000_UpdateDatabaseVersion.sql`. Nu hardcodați versiunea afișată în Web sau Business; ea este citită din `DatabaseVersion`.

## Fluxul versiunii aplicației

`ApplicationVersionViewComponent → IApplicationVersionService → ApplicationVersionService → IApplicationVersionRepository → ApplicationVersionRepository → WorkNotesDbContext`.

Repository-ul citește valorile. Business alege versiunea numerică maximă și păstrează eticheta originală. Web afișează versiunea în dreapta-jos a footerului sau „Versiune neconfigurată” dacă serviciul întoarce null. Păstrați comportamentul existent când modificați alte funcționalități.

## Identitate și autentificare

- Utilizați exclusiv ASP.NET Core Identity. `ApplicationUser` este în DataAccess și adaugă `FirstName`/`LastName`; tabela proprie este `Users`. Nu implementați hashing sau gestionarea cookie-urilor de autentificare manual.
- Este interzisă salvarea, jurnalizarea sau includerea parolelor/parolelor confirmate în URL-uri, mesaje, TempData, DTO-uri de rezultat ori telemetrie. Parolele tranzitează doar formularul și apelurile Identity. Baza păstrează exclusiv hashul standard Identity. Nu activați logarea corpurilor cererilor de cont.
- Business definește `IAccountService`, `IAuthenticationService`, DTO-uri și reguli independente de infrastructură. Nu referă Identity/ASP.NET/EF. DataAccess implementează contractele cu `UserManager` și `SignInManager` și configurează stocarea și politica Identity.
- Web validează formularele, autorizează paginile și configurează middleware/cookie-uri în `Program.cs`; nu folosește `ApplicationUser`, `UserManager`, `SignInManager` sau DbContext în pagini. Prezentarea consumă numai contractele Business.
- E-mailul este normalizat de Identity; `RequireUniqueEmail` și indexul unic pe `NormalizedEmail` sunt obligatorii. E-mailul nu se editează momentan. Numele/prenumele sunt validate și în serviciu, apoi normalizate prin `Trim`.
- Politica explicită: minimum 12 caractere, majuscule, minuscule, cifre și caracter special; 5 autentificări eșuate blochează contul 15 minute. Validarea client nu înlocuiește validarea server/Identity. Mesajele sunt localizate în limba selectată; login-ul nu distinge între cont absent, parolă incorectă și cont blocat.
- Toate modificările și deconectarea folosesc POST și antiforgery Razor Pages. Contul meu și schimbarea parolei necesită autorizare. După schimbarea parolei/profilului folosiți `RefreshSignInAsync`, păstrând cookie-ul persistent conform alegerii utilizatorului.
- Cookie-ul este HttpOnly, SameSite=Lax și Secure obligatoriu în afara dezvoltării. HTTPS este necesar pentru găzduire. Nu implementați roluri, confirmare e-mail sau recuperare parolă fără cerință nouă.
- Propagați `CancellationToken` în interogările EF. `UserManager`/`SignInManager` nu oferă parametru `CancellationToken` pentru toate operațiile: verificați anularea înainte de apel; nu simulați anularea cu `Task.Run` și nu întrerupeți reîmprospătarea cookie-ului după o modificare deja salvată.
- Scripturile Identity sunt idempotente prin verificarea obiectelor SQL (tabele și indexuri), fără istoric EF, și se păstrează în folderul versiunii curente conform regulilor de mai sus. Datele conturilor și hashurile existente nu se șterg și nu se recreează la actualizarea schemei.

## Localizare obligatorie

Utilizarea resurselor .resx este obligatorie pentru toate textele afișate utilizatorului în aplicația Web. Este interzisă introducerea directă a etichetelor, titlurilor, mesajelor, textelor butoanelor sau mesajelor de validare în cod, pagini Razor, view models ori scripturi. Pentru fiecare text nou trebuie creată o cheie în proiectul WorkNotes.Resources, cu traduceri complete în română, engleză și poloneză. Limba română reprezintă limba implicită și fallback-ul aplicației. Implementarea este descrisă în [docs/LOCALIZATION.md](docs/LOCALIZATION.md).

- `SharedResources.resx`, `SharedResources.ro.resx`, `SharedResources.en.resx` și `SharedResources.pl.resx` păstrează exact aceleași chei și parametri de formatare. Fișierul neutru conține româna, identică variantei .ro.
- O funcționalitate nu este finalizată dacă lipsește o traducere în oricare dintre cele trei limbi. Nu permiteți valori goale, chei lipsă sau chei neutilizate create accidental.
- Cheile sunt descriptive, stabile și reutilizabile, cu prefix funcțional (`Navigation_`, `Field_`, `Button_`, `Validation_`, `Message_` etc.). Nu folosiți propoziții drept chei.
- Folosiți `IStringLocalizer<SharedResources>` și mecanismele standard ASP.NET Core. `IStringLocalizer` fără parametru generic este legat în DI la același catalog. Nu introduceți alte proiecte sau biblioteci de localizare fără o nevoie justificată.
- `RequestLocalizationMiddleware` se execută înainte de autentificare și pagini. Culturile sunt ro-RO (implicită), en-US și pl-PL. Cookie-ul standard păstrează selecția; fără cookie valid aplicația folosește ro-RO.
- Selectorul folosește POST cu antiforgery, verifică lista culturilor acceptate și permite redirecționarea numai la o adresă locală. Păstrează ruta și query string-ul, inclusiv PathBase.
- DataAnnotations folosesc chei .resx prin `DataAnnotationLocalizerProvider` comun. JavaScript citește textele randate de server (atributele `data-val-*` generate de Razor, alte atribute `data-*`, template-uri și date JSON din pagină); nu duplicați traduceri în JS și nu injectați HTML necodificat.
- Business/DataAccess returnează chei/coduri pentru erorile destinate utilizatorului, nu texte traduse. Traducerea finală este în Web. `LocalizedIdentityErrorDescriber` produce coduri stabile și descrieri localizate, iar serviciile transmit codurile. TempData păstrează cheia mesajului, nu traducerea.
- Brandul WorkNotes, datele introduse de utilizatori și identificatorii tehnici nu se traduc. Mesajele tehnice din loguri și detaliile interne ale excepțiilor nu se localizează și nu devin texte de interfață.
- Rulați `tools/Test-Resources.ps1` și verificați cele trei limbi, validările client/server, mesajele Identity și paginile protejate când modificați localizarea.

## Design obligatoriu

- Designul aprobat este varianta 1, „Hârtie & salvie”: fond crem, verde închis #176C65, hârtie și salvie. Ghidul este [docs/UI-UX.md](docs/UI-UX.md).
- Utilizați tokenurile din `tokens.css` și componentele din `site.css`, `navigation.css`, `notes-board.css`, `postit.css` și `note-editor.css`. Nu duplicați paleta și stilurile butoanelor în pagini.
- Păstrați stările hover, active, disabled, focus vizibil, selecție și eroare, cu contrast lizibil și suport pentru reduced-motion și forced-colors.
- Toate textele rămân în .resx. Designul nu autorizează introducerea de date fictive sau implementarea unor module fără cerință.
- Aspectul vine numai din clase CSS; JavaScript nu scrie stiluri inline. Operațiile de bază funcționează și fără JavaScript (starea overlay-urilor este în URL); JavaScript adaugă confortul.
- Tabla afișează ordinea primită de la serviciu (luni, apoi, în fiecare lună, `Order` crescător, ultima modificare și crearea descrescător). Singura reordonare DOM este schimbul prin drag-and-drop a două note ale proprietarului din aceeași lună, prinse numai de bandă; după salvare lista urmează ordinea returnată de server. Decalajele și rotațiile mici sunt deterministe din ID, aplicate prin clasele `note-card--tilt-*` alese de server (fără stiluri inline din JavaScript), încadrate în celula proprie; nu reordonează DOM și nu se recalculează aleatoriu la reîncărcare. Pe mobil sunt eliminate.

## Verificarea livrării

După fiecare set coerent de modificări și înainte de predare, din folderul `Solution`:

```powershell
dotnet tool restore
dotnet restore WorkNotes.sln
dotnet build WorkNotes.sln --no-restore
dotnet test WorkNotes.sln --no-build --no-restore
```

- Remediați erorile de compilare și regresiile. Investigați avertismentele nou introduse.
- Opriți procesele aplicației pornite de agent pentru verificare înainte de recompilare și la finalul verificărilor, dacă utilizatorul nu cere explicit să rămână pornite. DLL-urile blocate de un proces WorkNotes.Web pot produce MSB3021/MSB3027 în Visual Studio. Identificați procesul după calea executabilului și opriți numai instanța relevantă; nu opriți global procesele dotnet și nu adăugați opriri forțate în fișierele de build.
- Verificați că dependențele respectă arhitectura și că Web nu folosește direct EF/SQL (comenzile de verificare sunt în [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#verificarea-regulilor-arhitecturale)).
- Pentru schimbări ale fluxului versiunii verificați citirea din SQL Server și afișarea în footer. Verificați și cazul gol prin teste, fără a șterge datele utilizatorului doar pentru testare.
- Testați reguli de business și cazuri relevante, nu copii ale implementării. Testele Business trebuie să ruleze independent de SQL Server.
- Verificările manuale obligatorii sunt în [docs/TESTING.md](docs/TESTING.md). Dacă mediul împiedică o verificare, raportați concret verificarea neefectuată; nu declarați succes neverificat.

## Git și limitele sarcinii

- Nu dezvoltați direct pe `main`. Fiecare sarcină se lucrează într-un feature branch creat din `main` actualizat; fluxul este descris în [docs/GIT-WORKFLOW.md](docs/GIT-WORKFLOW.md).
- Nu faceți commit, push, pull request sau merge fără solicitare explicită. Nimic nu se integrează în `main` fără solicitare explicită.
- Nu executați resetări, `git clean`, force push, rescrieri de istoric, checkout/restore care aruncă modificări, ștergeri de branch-uri sau alte operații Git distructive fără solicitare explicită.
- Păstrați modificările existente ale utilizatorului. Inspectarea statusului și diff-urilor este permisă. Dacă la începutul unei sarcini există modificări locale nesalvate, nu le ștergeți și nu le suprascrieți: opriți-vă și prezentați situația.
- Nu adăugați în Git secrete, connection string-uri cu credențiale, fișiere de build (`bin`, `obj`), `node_modules` sau fișiere locale ale IDE-ului.
- Implementați numai funcționalitatea cerută, simplu și clar. Nu modificați funcționalități, cod sau documente fără legătură cu sarcina. Nu adăugați automat modulele descrise în analiza de produs, autentificare, editor sau servicii externe.

## Întreținerea documentației

La fiecare pas nou, în aceeași sarcină, actualizați documentele afectate:

- [CHANGELOG.md](CHANGELOG.md) — ce s-a schimbat (Added, Changed, Fixed, Database, Documentation);
- [docs/CURRENT-STATUS.md](docs/CURRENT-STATUS.md) și statusurile din [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md);
- [docs/decisions](docs/decisions/README.md) — deciziile noi, ca ADR sau în jurnalul deciziilor;
- [docs/ROADMAP.md](docs/ROADMAP.md) — ce s-a închis sau a apărut;
- [docs/DATABASE.md](docs/DATABASE.md), [Scripts/README.md](Scripts/README.md) și [docs/VERSIONING.md](docs/VERSIONING.md) când se schimbă schema, scripturile sau versiunea;
- [README.md](README.md), [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) și fișierele `CLAUDE.md` când se schimbă structura, comenzile sau fluxurile;
- [docs/UI-UX.md](docs/UI-UX.md), [docs/LOCALIZATION.md](docs/LOCALIZATION.md), [docs/SECURITY.md](docs/SECURITY.md) și [docs/TESTING.md](docs/TESTING.md) când se schimbă interfața, resursele, securitatea sau verificările.

O regulă se definește într-un singur document și se referă prin linkuri relative. Informațiile care nu pot fi determinate din cod sau din cerințe se marchează explicit cu „TODO: Necesită clarificare”; nu se inventează funcționalități, decizii sau versiuni.
