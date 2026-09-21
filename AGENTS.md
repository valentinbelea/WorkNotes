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

Direcția referințelor este Web → Business, Web → DataAccess pentru compunere, DataAccess → Business. Business nu referă Web sau DataAccess. Sunt interzise dependențele circulare.

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

Comanda completă este în README.md. Nu creați migrări EF, snapshot-uri sau `__EFMigrationsHistory` în fluxul curent. Nu apelați Migrate/EnsureCreated la pornirea aplicației. Dacă utilizatorul cere explicit revenirea la migrări, acestea vor aparține exclusiv proiectului DataAccess și regulile/documentația trebuie actualizate în aceeași schimbare.

Scripturile curente se află în `..\Scripts\version_0.01`, alături de rădăcina repository-ului, conform cerinței utilizatorului. Ele nu sunt incluse automat în Git. Nu le mutați fără solicitare; precizați schimbările făcute acolo la predare.

- Scriptul de creare păstrează tabelele existente; schimbările de structură folosesc scripturi ALTER dedicate.
- Scripturile de inserare a versiunilor evită duplicatele și păstrează datele existente. Folosiți tranzacții acolo unde atomicitatea este necesară.
- Nu înlocuiți sau ștergeți tabele/date pentru a rezolva o incompatibilitate de model fără solicitare explicită.
- Păstrați scriptul și versiunea `v.0.01`. Nu hardcodați versiunea afișată în Web sau Business; ea este citită din DatabaseVersion.
- EF Core și instrumentul local dotnet-ef rămân pe versiuni compatibile cu .NET 10. Actualizările majore necesită o cerință explicită și verificarea compatibilității.

## Fluxul versiunii aplicației

`IndexModel → IApplicationVersionService → ApplicationVersionService → IApplicationVersionRepository → ApplicationVersionRepository → WorkNotesDbContext`.

Repository-ul citește valorile. Business alege versiunea numerică maximă și păstrează eticheta originală. Web afișează versiunea în dreapta-jos a footerului sau „Versiune neconfigurată” dacă serviciul întoarce null. Păstrați comportamentul existent când modificați alte funcționalități.

## Verificare obligatorie

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
