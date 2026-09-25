# Reguli obligatorii pentru WorkNotes

Acest fișier se aplică întregului repository și tuturor modificărilor viitoare. Citiți-l înainte de a modifica proiectul. Cerințele explicite ale utilizatorului au prioritate; nu deduceți aprobarea unor funcționalități noi din documentele de analiză sau din existența unor biblioteci.

## Arhitectură și dependențe

Soluția este `WorkNotes.sln`. Toate proiectele rămân compatibile cu .NET 10 (`net10.0`).

| Proiect | Responsabilitate | Dependențe permise |
| --- | --- | --- |
| WorkNotes.Web | Razor Pages, controllere dacă devin necesare, view models, layout, CSS, prezentare, configurare și pornire | Business; DataAccess numai pentru compunerea DI |
| WorkNotes.Business | Reguli, servicii, modele/DTO-uri și contracte de acces la date | Biblioteci independente de infrastructură; nici Web, nici DataAccess, nici EF Core |
| WorkNotes.DataAccess | DbContext, entități și mapări EF, repository-uri, integrare SQL Server și înregistrarea lor în DI | Business și bibliotecile de persistență |
| WorkNotes.Business.Tests | Teste pentru regulile și contractele Business | Business și infrastructura de testare; fără bază de date obligatorie |
| WorkNotes.Resources | Resurse .resx comune, complete în ro/en/pl, și clasa marker SharedResources | Fără dependențe către Web, Business sau DataAccess |

Direcția referințelor este Web → Business, Web → DataAccess pentru compunere, DataAccess → Business. Business nu referă Web sau DataAccess. Sunt interzise dependențele circulare.

Web referă și WorkNotes.Resources pentru prezentarea localizată. Business și DataAccess transmit chei de mesaj, fără dependențe noi de infrastructura de localizare. IdentityErrorDescriber localizat este în Web/Localization și se înregistrează în Program.cs.

Referința Web către DataAccess se utilizează numai în punctul de compunere `Program.cs`, prin extensia `AddDataAccess`. Nu injectați sau folosiți `WorkNotesDbContext`, `DbContext`, `DbSet`, repository-uri concrete, SQL ori clienți de baze de date în pagini, controllere, view models, view-uri sau alte componente Web. Înregistrarea DbContext și configurarea providerului rămân în DataAccess.

Excepție strict de tooling: Web păstrează `Microsoft.EntityFrameworkCore.Design`, cu `PrivateAssets=all`, deoarece este startup project pentru reverse engineering. Aceasta nu autorizează cod EF în Web. Providerul SQL Server se referă direct numai din DataAccess.

## SOLID, interfețe și dependency injection

- Fiecare clasă are o responsabilitate clară. Prezentarea, regulile de business și persistența sunt separate.
- Pagina/controllerul orchestrează apelul serviciului și pregătește prezentarea. Nu include selecția versiunii, calcule sau alte reguli de business.
- Serviciile de business sunt consumate prin interfețe, injectate prin constructor. Business depinde de interfețe de acces la date definite în Business.
- DataAccess implementează aceste interfețe. Contractele expun tipuri simple sau DTO-uri Business; nu expun entități EF, DbContext, DbSet, IQueryable ori tipuri SQL.
- Respectați substituibilitatea implementărilor și semantica rezultatelor, excepțiilor și anulării. Interfețele trebuie să fie mici și orientate spre utilizatorii lor.
- Registrați implementările explicit prin DI. Pentru DbContext, repository-uri și serviciile care le utilizează folosiți lifetime scoped. Nu capturați servicii scoped într-un singleton.
- Nu folosiți service locator, instanțiere manuală de DbContext în fluxurile aplicației ori clase statice care ascund dependențe.
- Extindeți modelul prin responsabilități și contracte utile; nu introduceți repository generic, Unit of Work propriu peste EF, mediator, fabrici sau proiecte suplimentare fără necesitate demonstrabilă.
- Datele lipsă și erorile sunt cazuri distincte. O eroare SQL nu devine automat „nu există versiune”. Mesajele destinate utilizatorului aparțin Web.

## Acces asincron și anulare

- Accesul la baza de date este asincron: de exemplu ToListAsync, FirstOrDefaultAsync și SaveChangesAsync, după caz.
- Propagați CancellationToken de la request prin serviciu și repository până la EF. Nu înlocuiți tokenul primit cu CancellationToken.None.
- Evitați .Result, .Wait(), async void și Task.Run pentru I/O de bază de date.
- Utilizați AsNoTracking pentru interogări exclusiv de citire și proiectați doar câmpurile necesare.
- Nu efectuați operații concurente pe aceeași instanță DbContext. Nu interceptați anularea ca succes sau rezultat gol.

## Configurare și denumire

- Connection string-ul este citit de Web din `ConnectionStrings:WorkNotes` și transmis înregistrării DataAccess. Nu hardcodați servere, nume de baze, credențiale, URL-uri de mediu sau connection string-uri în clase.
- Folosiți appsettings.json, appsettings.{Environment}.json și suprascrieri prin configurația mediului. Credencialele secrete nu se salvează în Git; folosiți User Secrets sau variabile de mediu.
- Configurația locală curentă folosește Windows Authentication și TrustServerCertificate=True. Nu impuneți automat această opțiune pentru găzduirea viitoare.
- Clasele, interfețele, metodele și proprietățile au nume descriptive PascalCase. Interfețele au prefix I, metodele asincrone au sufix Async, variabilele/parametrii sunt camelCase.
- Numele fișierului corespunde tipului principal. Namespace-urile urmează proiectul și folderele.
- Business: `Abstractions`, `Services`, iar `Models`/`Dtos` numai când sunt necesare. DataAccess: `Context`, `Entities`, `Repositories`; mapări manuale separate dacă devin necesare. Web: `Pages`, `Pages/Shared`, `wwwroot`, eventual `ViewModels`/`Controllers` când există un caz real.
- Păstrați nullable reference types activate. Nu adăugați DTO-uri, interfețe sau clase care doar multiplică aceeași structură fără utilitate.

## EF Core și schema SQL — Database First

Abordarea curentă, cerută explicit, este Database First. Schema SQL este sursa pentru entitățile și contextul EF; regulile de business rămân în Business.

1. Pregătiți modificarea schemei prin scripturi SQL explicite, ordonate în folderul versiunii.
2. Aplicați scripturile asupra bazei autorizate pentru sarcină; nu modificați alte baze sau medii implicit.
3. Regenerați clasele în DataAccess prin scaffolding, cu Web ca startup project și `Name=ConnectionStrings:WorkNotes`.
4. Folosiți `--no-onconfiguring`. Configurația conexiunii rămâne în aplicație, nu în codul generat.
5. Păstrați particularizările manuale în fișiere partial sau clase separate. `--force` suprascrie fișierele generate; inspectați modificările și includeți toate tabelele necesare în selecția de scaffolding.

Comanda completă este în README.md. Database First este obligatoriu pentru TOATE modulele, inclusiv Identity și AccountsDbContext. Nu creați migrări EF, snapshot-uri sau tabele de istoric (__EFMigrationsHistory / __IdentityMigrationsHistory). Nu folosiți dotnet ef migrations / database update și nu apelați Migrate/EnsureCreated. Schema se creează și se modifică exclusiv prin scripturi SQL explicite. Pentru Identity păstrați clasele standard și moștenirea IdentityUserContext/ApplicationUser; actualizați manual mapările conform schemei SQL, fără scaffolding care suprascrie integrarea Identity.

Versiunea curentă este 0.01 (eticheta din bază: v.0.01). Regula obligatorie: versiunea curentă 0.0x înseamnă folderul E:\GitRepository\Vali\WorkNotes\Scripts\version_0.0x. Pentru 0.01, TOATE scripturile, inclusiv Identity și scripturile de tranziție, se pun în E:\GitRepository\Vali\WorkNotes\Scripts\version_0.01. Nu incrementați versiunea și nu creați un folder de versiune nouă doar pentru un modul sau o modificare; schimbarea versiunii necesită solicitare explicită. Păstrați numerotarea ordonată a scripturilor și evitați suprascrierea altor scripturi. Rădăcina repository-ului Git este E:\GitRepository\Vali\WorkNotes și conține Scripts (scripturile SQL) și Solution (soluția, AGENTS.md, README.md, docs, tools). Comenzile dotnet se rulează din Solution; din Solution scripturile se află la ..\Scripts\version_0.0x.

- Scriptul de creare păstrează tabelele existente; schimbările de structură folosesc scripturi ALTER dedicate.
- Scripturile de inserare a versiunilor evită duplicatele și păstrează datele existente. Folosiți tranzacții acolo unde atomicitatea este necesară.
- Nu înlocuiți sau ștergeți tabele/date pentru a rezolva o incompatibilitate de model fără solicitare explicită.
- Păstrați scriptul și versiunea `v.0.01`. Nu hardcodați versiunea afișată în Web sau Business; ea este citită din DatabaseVersion.
- EF Core și instrumentul local dotnet-ef rămân pe versiuni compatibile cu .NET 10. Actualizările majore necesită o cerință explicită și verificarea compatibilității.

## Fluxul versiunii aplicației

`ApplicationVersionViewComponent → IApplicationVersionService → ApplicationVersionService → IApplicationVersionRepository → ApplicationVersionRepository → WorkNotesDbContext`.

Repository-ul citește valorile. Business alege versiunea numerică maximă și păstrează eticheta originală. Web afișează versiunea în dreapta-jos a footerului sau „Versiune neconfigurată” dacă serviciul întoarce null. Păstrați comportamentul existent când modificați alte funcționalități.

## Identitate și autentificare

- Utilizați exclusiv ASP.NET Core Identity. ApplicationUser este în DataAccess și adaugă FirstName/LastName; tabela proprie este Users. Nu implementați hashing sau gestionarea cookie-urilor de autentificare manual.
- Este interzisă salvarea, jurnalizarea sau includerea parolelor/parolelor confirmate în URL-uri, mesaje, TempData, DTO-uri de rezultat ori telemetrie. Parolele tranzitează doar formularul și apelurile Identity. Baza păstrează exclusiv hashul standard Identity. Nu activați logarea corpurilor cererilor de cont.
- Business definește IAccountService, IAuthenticationService, DTO-uri și reguli independente de infrastructură. Nu referă Identity/ASP.NET/EF. DataAccess implementează contractele cu UserManager și SignInManager și configurează stocarea și politica Identity.
- Web validează formularele, autorizează paginile și configurează middleware/cookie-uri în Program.cs; nu folosește ApplicationUser, UserManager, SignInManager sau DbContext în pagini. Prezentarea consumă numai contractele Business.
- E-mailul este normalizat de Identity; RequireUniqueEmail și indexul unic pe NormalizedEmail sunt obligatorii. E-mailul nu se editează momentan. Numele/prenumele sunt validate și în serviciu, apoi normalizate prin Trim.
- Politica explicită: minimum 12 caractere, majuscule, minuscule, cifre și caracter special; 5 autentificări eșuate blochează contul 15 minute. Validarea client nu înlocuiește validarea server/Identity. Mesajele sunt localizate în limba selectată; login-ul nu distinge între cont absent, parolă incorectă și cont blocat.
- Toate modificările și deconectarea folosesc POST și antiforgery Razor Pages. Contul meu și schimbarea parolei necesită autorizare. După schimbarea parolei/profilului folosiți RefreshSignInAsync, păstrând cookie-ul persistent conform alegerii utilizatorului.
- Cookie-ul este HttpOnly, SameSite=Lax și Secure obligatoriu în afara dezvoltării. HTTPS este necesar pentru găzduire. Nu implementați roluri, confirmare e-mail sau recuperare parolă fără cerință nouă.
- Propagați CancellationToken în interogările EF. UserManager/SignInManager nu oferă parametru CancellationToken pentru toate operațiile: verificați anularea înainte de apel; nu simulați anularea cu Task.Run și nu întrerupeți reîmprospătarea cookie-ului după o modificare deja salvată.
- Scripturile Identity sunt idempotente prin verificarea obiectelor SQL (tabele și indexuri), fără istoric EF. Se păstrează în folderul versiunii curente conform regulii de mai sus. Datele conturilor și hashurile existente nu se șterg sau recreează la actualizarea schemei.

## Localizare obligatorie

Utilizarea resurselor .resx este obligatorie pentru toate textele afișate utilizatorului în aplicația Web. Este interzisă introducerea directă a etichetelor, titlurilor, mesajelor, textelor butoanelor sau mesajelor de validare în cod, pagini Razor, view models ori scripturi. Pentru fiecare text nou trebuie creată o cheie în proiectul WorkNotes.Resources, cu traduceri complete în română, engleză și poloneză. Limba română reprezintă limba implicită și fallback-ul aplicației.

- Fișierele SharedResources.resx, SharedResources.ro.resx, SharedResources.en.resx și SharedResources.pl.resx păstrează exact aceleași chei și parametri de formatare. Fișierul neutru conține româna, identică variantei .ro.
- O funcționalitate nu este finalizată dacă lipsește o traducere în oricare dintre cele trei limbi. Nu permiteți valori goale, chei lipsă sau chei neutilizate create accidental.
- Cheile sunt descriptive, stabile și reutilizabile, cu prefix funcțional (Navigation_, Field_, Button_, Validation_, Message_ etc.). Nu folosiți propoziții drept chei.
- Folosiți IStringLocalizer<SharedResources> și mecanismele standard ASP.NET Core. IStringLocalizer fără parametru generic este legat în DI la același catalog. Nu introduceți alte proiecte sau biblioteci de localizare fără o nevoie justificată.
- RequestLocalizationMiddleware se execută înainte de autentificare și pagini. Culturile sunt ro-RO (implicită), en-US și pl-PL. Cookie-ul standard păstrează selecția; fără cookie valid aplicația folosește ro-RO.
- Selectorul folosește POST cu antiforgery, verifică lista culturilor acceptate și permite redirecționarea numai la o adresă locală. Păstrează ruta și query string-ul, inclusiv PathBase.
- DataAnnotations folosesc chei .resx prin DataAnnotationLocalizerProvider comun. JavaScript citește mesajele generate de Razor în atributele data-val-*; nu duplicați traduceri în JS și nu injectați HTML necodificat.
- Business/DataAccess returnează chei/coduri pentru erorile destinate utilizatorului, nu texte traduse. Traducerea finală este în Web. LocalizedIdentityErrorDescriber produce coduri stabile și descrieri localizate, iar serviciile transmit codurile. TempData păstrează cheia mesajului de succes, nu traducerea.
- Brandul WorkNotes, datele introduse de utilizatori și identificatorii tehnici nu se traduc. Mesajele tehnice din loguri și detaliile interne ale excepțiilor nu se localizează și nu devin texte de interfață.
- Rulați tools/Test-Resources.ps1 și verificați cele trei limbi, validările client/server, mesajele Identity și paginile protejate când modificați localizarea.

## Design obligatoriu — Hârtie & salvie

- Varianta 1 este designul aprobat: fond crem, verde închis #176C65, hârtie și salvie. Ghidul este docs/design-system.md.
- Utilizați tokenurile din tokens.css și componentele din site.css / notes-board.css. Nu duplicați paleta și stilurile butoanelor în pagini.
- Păstrați stările hover, active, disabled, focus vizibil, selecție și eroare, cu contrast lizibil și suport pentru reduced-motion.
- Toate textele rămân în .resx. Designul nu autorizează introducerea de date fictive sau implementarea unor module fără cerință.
- Tabla afișează ordinea primită de la serviciu (luni, apoi jurnale și articole). Decalajele și rotațiile mici sunt deterministe din ID, aplicate prin clasele note-card--tilt-* alese de server (fără stiluri inline din JavaScript), încadrate în celula proprie; nu reordonează DOM și nu se recalculează aleatoriu la reîncărcare. Pe mobil sunt eliminate.

## Verificarea livrării

După fiecare set coerent de modificări și înainte de predare:

```powershell
dotnet tool restore
dotnet restore WorkNotes.sln
dotnet build WorkNotes.sln --no-restore
dotnet test WorkNotes.sln --no-build --no-restore
```

- Remediați erorile de compilare și regresiile. Investigați avertismentele nou introduse.
- Opriți procesele aplicației pornite de agent pentru verificare înainte de recompilare și la finalul verificărilor, dacă utilizatorul nu cere explicit să rămână pornite. DLL-urile blocate de un proces WorkNotes.Web pot produce MSB3021/MSB3027 în Visual Studio. Identificați procesul după calea executabilului și opriți numai instanța relevantă; nu opriți global procesele dotnet și nu adăugați opriri forțate în fișierele de build.
- Verificați că dependențele respectă arhitectura și că Web nu folosește direct EF/SQL.
- Pentru schimbări ale fluxului versiunii verificați citirea din SQL Server și afișarea în footer. Verificați și cazul gol prin teste fără a șterge datele utilizatorului doar pentru testare.
- Testați reguli de business și cazuri relevante, nu copii ale implementării. Testele Business trebuie să ruleze independent de SQL Server.
- Actualizați README.md când structura sau comenzile se schimbă. Dacă mediul împiedică o verificare, raportați concret verificarea neefectuată; nu declarați succes neverificat.

## Git și limitele sarcinii

- Nu faceți commit sau push fără solicitare explicită.
- Nu executați resetări, git clean, force push, rescrieri de istoric, checkout/restore care aruncă modificări, ștergeri de branch-uri sau alte operații Git distructive fără solicitare explicită.
- Păstrați modificările existente ale utilizatorului. Inspectarea statusului și diff-urilor este permisă.
- Implementați numai funcționalitatea cerută, simplu și clar. Nu adăugați automat modulele descrise în analiza de produs, autentificare, editor sau servicii externe.
