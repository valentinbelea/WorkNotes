# ADR-004: Conectarea la GitHub prin OAuth

- **Status:** Accepted — cerută explicit de utilizator pe 2026-09-29, pentru versiunea 0.03 (branch `main_task_03`). Completată în aceeași zi, la cererile următoare: importul repository-urilor (deciziile 111–116) și referințele Git din editor (deciziile 117–126).
- **Data:** 2026-09-29.
- **Surse:** cererea utilizatorului („Trebuie să ne integrăm cu GIT. Pentru asta este necesară o secțiune de autentificare în GIT”) și răspunsurile lui: furnizorul este GitHub (deocamdată); WorkNotes citește branch-uri, commit-uri și pull request-uri legate de CR-uri și buguri; autentificarea preferată este OAuth; codul furnizorului stă într-un proiect nou. Deciziile 99–110 din [jurnal](README.md#jurnalul-deciziilor); codul din `GitHubConnectionService`, `GitAuthorizationRules`, `WorkNotes.Integrations/GitHub/GitHubOAuthClient.cs`, `GitConnectionRepository`, `Pages/Account/GitHub` și `Scripts/version_0.03/001_CreateGitConnections.sql`.

## Context

Versiunea 0.03 leagă WorkNotes de Git. Primul pas este autentificarea: fiecare utilizator WorkNotes își conectează contul GitHub, pentru ca aplicația să poată citi, în numele lui, branch-urile, commit-urile și pull request-urile legate de CR-uri și buguri. Citirea lor nu face parte din acest pas. Aplicația rulează local (`localhost`) și, mai târziu, găzduită; conturile WorkNotes rămân ASP.NET Core Identity, iar autentificarea în WorkNotes nu se schimbă.

## Decizie

1. **OAuth web flow, cu PKCE:** pagina `/Account/GitHub` trimite browserul la `https://github.com/login/oauth/authorize` cu `client_id`, `redirect_uri`, un `state` aleatoriu și `code_challenge` (S256). GitHub îl întoarce la `/Account/GitHub/Callback` cu `code` și `state`. Serverul verifică `state`, schimbă codul pe token (`POST /login/oauth/access_token`, cu `client_secret` și `code_verifier`), citește contul (`GET /user`) și salvează conexiunea (deciziile 99, 100).
2. **O aplicație GitHub înregistrată, recomandat GitHub App:** codul funcționează și cu o GitHub App (tokenuri care expiră după 8 ore, cu refresh token, permisiuni fine numai de citire), și cu o OAuth App (tokenuri fără expirare, scopuri largi: `repo` pentru repository-uri private). Recomandarea este GitHub App, cu permisiunile Contents, Metadata și Pull requests numai de citire (decizia 101).
3. **Conectarea nu este autentificare:** contul GitHub se leagă de utilizatorul WorkNotes deja autentificat, nu îl autentifică. Nu se folosește handlerul OAuth al ASP.NET Core și nici conturile externe Identity (`AspNetUserLogins`) (decizia 102).
4. **Stocarea:** tabela `dbo.GitConnections`, câte un rând pe utilizator și furnizor (`GitHub`), cu contul (ID-ul stabil și login-ul), tokenurile criptate, expirările, scopurile și datele conectării și ale ultimei verificări. Tokenurile sunt criptate cu ASP.NET Core Data Protection în `GitConnectionRepository`, înainte de SQL Server; tokenuri care nu se mai pot decripta înseamnă „conectați din nou” (deciziile 103, 104).
5. **Straturile:** Business are regulile (`GitAuthorizationRules`: state, PKCE, reîmprospătare, limite), serviciul `IGitHubConnectionService` / `GitHubConnectionService` și contractele `IGitHubOAuthClient` și `IGitConnectionRepository`. Proiectul nou `WorkNotes.Integrations` implementează `IGitHubOAuthClient` peste `HttpClient` (`GitHubOAuthClient`, `GitHubOptions`, `AddIntegrations`). DataAccess implementează repository-ul. Web are pagina și cookie-ul autorizării în curs (deciziile 105, 106).
6. **Autorizarea în curs:** `state` și `code_verifier` stau în browser, în cookie-ul `WorkNotes.GitHubAuthorization`, criptat cu Data Protection pentru utilizatorul curent, valabil 10 minute, HttpOnly, SameSite=Lax, limitat la `/Account/GitHub` și citit o singură dată (decizia 107).
7. **Verificarea și deconectarea:** „Verifică conexiunea” reîmprospătează întâi un token care expiră (și salvează imediat tokenurile noi, pentru că refresh token-ul vechi nu mai poate fi folosit), apoi citește contul și actualizează login-ul. „Deconectează” șterge rândul, apoi revocă autorizarea la GitHub (`DELETE /applications/{client_id}/grant`); dacă GitHub nu o poate revoca, utilizatorul este avertizat să o revoce din setările GitHub (deciziile 108, 109).
8. **Configurarea (decizia 128, care înlocuiește decizia 110):** Client ID, Client secret, scopes și callback URL vin din singletonul protejat `GitHubConfigurations`, administrat în `/admin/configuration` și citit asincron la fiecare operație OAuth. Lipsa ori imposibilitatea decriptării înseamnă „neconfigurat”; erorile SQL se propagă. Callback URL din tabelă este autoritar. Cheile Data Protection au numele aplicației `WorkNotes` și un folder persistent pe mediile găzduite (`DataProtection:KeysPath`).

9. **Importul repository-urilor** (a doua cerere: „o pagină de import al listei de repositories, afișăm lista tuturor din GIT și bifăm pe cele pe care dorim să le importăm în WorkNotes”): pagina `/Repositories` citește la fiecare vizită, cu tokenul utilizatorului, toate repository-urile pe care GitHub i le arată (`GET /user/repos`, proprii, de colaborator și ale organizațiilor, 100 pe pagină, cel mult 1000), le afișează în ordinea numelui, cu câte o bifă, și salvarea face din cele bifate repository-urile importate ale utilizatorului, în `dbo.GitRepositories` (debifarea le scoate). Obținerea unui token valid, cu reîmprospătare, este un serviciu comun, `IGitHubTokenService` (deciziile 111–116).

10. **Referințele Git din editor** (a treia cerere: în popup-ul de adăugare a referințelor din editor, opțiunea existentă devine „Referință aplicație”, iar o opțiune nouă, „Referință Git”, alege un repository importat și caută branch-ul care conține un șir asemănător cu `CR <nr>` sau `bug <nr>`, după regula stabilită pentru referințe): proprietarul notei alege repository-ul dintre cele importate, aplicația citește de la GitHub branch-urile lui și le păstrează pe cele al căror nume conține referința (`NoteReferenceParser`: tipul, apoi `-`, `_` sau nimic, apoi numărul, ca cuvinte întregi, în orice combinație de litere), iar branch-ul ales se leagă de referința paragrafului în `dbo.NoteBlockGitReferences`, cu branch-ul o singură dată în catalogul `dbo.GitReferences` și referința în `dbo.WorkReferences`. Linkurile se văd în sertarul referințelor, sub cele ale aplicației (deciziile 117–126).

## Motive

- OAuth a fost cerut explicit; utilizatorul nu copiază tokenuri, iar autorizarea poate fi revocată de la GitHub.
- PKCE și `state` protejează callback-ul GET, pe care protocolul nu îl poate proteja cu antiforgery.
- O GitHub App dă acces numai de citire; o OAuth App nu are un scop numai de citire pentru repository-urile private.
- Criptarea în DataAccess face ca o copie a bazei să nu dezvăluie tokenurile; Data Protection este mecanismul standard al ASP.NET Core, deja folosit pentru cookie-uri.
- Proiectul nou a fost ales de utilizator: codul HTTP al furnizorilor nu este persistență și are alt ciclu de viață decât EF Core; Business rămâne fără dependențe.

## Alternative cunoscute

- Token personal (PAT) lipit într-un formular: mai simplu și fără aplicație înregistrată, dar respins de utilizator în favoarea OAuth.
- Handlerul `AddOAuth` / `AddGitHub` al ASP.NET Core ca schemă de autentificare: gândit pentru autentificare, amestecat cu cookie-ul extern Identity; pentru o legătură a unui cont deja autentificat, fluxul explicit este mai clar și testabil.
- Tokenurile în DataAccess, fără proiect nou: respins de utilizator.
- Tokenurile necriptate în bază, protejate numai de drepturile SQL: respins, pentru că un backup sau o copie a bazei le-ar dezvălui.

## Consecințe

- Fiecare mediu are nevoie de o aplicație GitHub înregistrată, cu URL-ul de callback al mediului (`https://<host>/Account/GitHub/Callback`; local `http://localhost:5018/Account/GitHub/Callback` sau `https://localhost:7190/Account/GitHub/Callback`), și de `ClientId` / `ClientSecret` în configurația lui.
- Pierderea cheilor Data Protection face tokenurile stocate de nedecriptat (și deconectează utilizatorii WorkNotes); conexiunile se refac din pagină.
- Setarea `SetApplicationName("WorkNotes")` schimbă, o singură dată, cheile folosite până acum pentru cookie-uri: utilizatorii autentificați trebuie să se autentifice din nou după prima pornire.
- Două verificări simultane ale unui token GitHub App pot consuma același refresh token; a doua folosește tokenurile salvate de prima, iar dacă nici acestea nu există, cere reconectarea.
- Pentru o GitHub App, `GET /user/repos` arată numai repository-urile conturilor și organizațiilor pe care aplicația este instalată: aplicația trebuie instalată (cu „All repositories” sau cu cele alese) pe fiecare cont sau organizație.
- Un branch legat de o notă partajată cu contextul își arată repository-ul și numele oricărui membru care vede nota, chiar dacă acela nu are acces la repository; linkul duce la GitHub, care aplică propriile drepturi. Un repository privat își dezvăluie astfel numele în context (ca orice text al notei).
- Căutarea citește branch-urile prin `GET /repos/{owner}/{repo}/branches` (GitHub nu are căutare după nume în REST) și filtrează local; un repository cu mai mult de 1000 de branch-uri este citit doar în parte, iar popup-ul spune acest lucru.
- Pasul următor (citirea commit-urilor și pull request-urilor branch-urilor legate) va folosi `IGitHubTokenService` pentru tokenul valid al utilizatorului. TODO: Necesită clarificare — ce repository-uri se citesc (configurate pe context?) și cum se leagă un branch, un commit sau un pull request de un CR sau bug (numele branch-ului, mesajul commit-ului, titlul pull request-ului).
