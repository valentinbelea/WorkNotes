# Administrare și configurarea GitHub

Zona de administrare este separată de aplicația utilizatorilor și se deschide direct la `/admin`; nu există link spre ea în meniul public. Cererile neautentificate sunt trimise la `/admin/login`, iar cookie-ul `WorkNotes.Admin.Auth` nu autentifică utilizatorul în zona publică și nici cookie-ul public în zona admin.

## Schema și instalarea

`Scripts/version_0.03/004_CreateAdministration.sql` creează defensiv:

- `AdminUsers`, cu nume unic, hash de parolă, stare activă și momentele creării, actualizării și ultimei autentificări;
- `GitHubConfigurations`, restrânsă la câte un rând unic pentru `Development` și `Production`. Configurația existentă este migrată la `Production`. `ProtectedClientId` și `ProtectedClientSecret` conțin numai payload-uri ASP.NET Core Data Protection.

Scriptul nu se aplică automat și nu conține nicio parolă sau valoare GitHub.

## Administratorul inițial

Configurați secretele de deploy `AdminBootstrap:UserName` (opțional, implicit `admin`) și `AdminBootstrap:Password`. La prima pornire cu parola configurată, aplicația folosește `PasswordHasher<AdminUser>` pentru a crea administratorul numai dacă numele lipsește. Valoarea nu se păstrează în fișiere versionate; după creare, eliminați `AdminBootstrap:Password` din mediul de rulare. Dacă nu este configurată nicio parolă, aplicația nu face seed. Cerința nu a furnizat valoarea parolei inițiale, deci aceasta trebuie stabilită sigur de operator.

## Configurarea GitHub

Pagina `/admin/configuration` permite selectarea explicită a mediului `Development` sau `Production` și acceptă separat Client ID, Client secret, scopes și callback URL. Client secret nu este niciodată returnat formularului: pagina arată numai indicatorul „configurat”; un câmp gol îl păstrează, iar o valoare nouă îl înlocuiește. Client ID este decriptat pentru editare, iar ambele credențiale sunt protejate cu scopul `WorkNotes.GitHubConfiguration.v1` înainte de SQL Server. Serviciile nu le jurnalizează.

Prima configurație se salvează obligatoriu prin această pagină, nu printr-un `INSERT` cu valorile în clar. Coloanele `ProtectedClientId` și `ProtectedClientSecret` necesită payload-uri create cu key ring-ul Data Protection al mediului; SQL Server nu le poate genera singur, iar un text clar sau un payload din alt mediu va fi considerat nedecriptabil. Client secret se generează în GitHub și se introduce direct în formular, fără a fi copiat în scripturi, documentație sau conversații.

Șablonul cerut de operator este vizibil în soluție la `Solution/temp/InsertGitHubConfiguration.sql`. Este intenționat blocat cât timp payload-urile protejate lipsesc și nu înlocuiește formularul admin; nu face parte din ordinea scripturilor de versiune și nu conține credențiale.

Pentru GitHub App, valoarea necesară formularului se obține prin **Generate a new client secret** din secțiunea **Client secrets**. Cheia din secțiunea **Private keys**, fișierul ei PEM și amprenta `SHA256:...` nu sunt Client secret și nu sunt consumate de fluxul OAuth implementat în WorkNotes; nu se introduc în formular sau în baza de date.

Un Client secret care a apărut într-o captură, conversație, log sau alt canal neautorizat se consideră compromis chiar dacă GitHub îl marchează „Never used”. Nu se salvează în WorkNotes. Operatorul generează mai întâi un secret nou, actualizează WorkNotes direct prin formularul admin, verifică autentificarea, apoi revocă secretul expus din GitHub. Dacă secretul expus este singurul secret, GitHub cere generarea unuia nou înainte de ștergerea lui.

## Data Protection și deploy

Setați `DataProtection:KeysPath` la un director persistent, privat identității aplicației și comun tuturor instanțelor. `SetApplicationName("WorkNotes")` trebuie să rămână identic. Pe Windows cheile din director sunt protejate suplimentar cu DPAPI; identitatea pool-ului trebuie păstrată la deploy. Directorul nu poate fi temporar, nu trebuie publicat împreună cu aplicația și trebuie inclus în backup. Pierderea ori schimbarea cheilor face imposibilă decriptarea configurației și a tokenurilor existente.

Directorul este providerul implementat în prezent, nu o limitare a Data Protection. Un key ring în SQL Server ar necesita o tabelă și un provider separate, plus protecția cheilor la repaus; stocarea în aceeași bază a payload-urilor și a cheilor neprotejate ar elimina separarea dintre datele criptate și cheia lor. O asemenea schimbare se tratează separat ca modificare Database First și de arhitectură. Analiza compromisurilor este în [GITHUB_CONFIGURATION_ANALYSIS.md](GITHUB_CONFIGURATION_ANALYSIS.md#de-ce-un-director-și-nu-aceeași-bază-de-date).

La hosting verificați: aplicarea scriptului `004`, drepturile SQL minime, existența și persistența directorului de chei după restart/deploy, HTTPS, permisiunile identității asupra directorului și absența secretelor din loguri. Testați login invalid/valid, salvarea și păstrarea secretului gol, restartul, logout-ul și redirectarea tuturor rutelor protejate.

## Consumarea configurației de integrare

`GitHubOAuthClient` citește `IGitHubConfigurationService.GetCredentialAsync`, care selectează `Development` când `IHostEnvironment.IsDevelopment()` și `Production` când `IsProduction()`; alte nume (inclusiv `Staging`) nu reutilizează implicit secretele Production și rămân neconfigurate. Clientul citește configurația pentru fiecare autorizare, exchange, refresh și revocare. Callback URL din tabelă este sursa autoritară pentru ambele cereri OAuth care trimit `redirect_uri`; Web nu îl suprascrie. O salvare administrativă este observată de operațiile ulterioare fără restart. Dacă valorile se schimbă între pornirea autorizării și callback, exchange-ul folosește configurația curentă și GitHub poate refuza cererea; utilizatorul poate porni o autorizare nouă.


## Medii OAuth

Sunt necesare două OAuth Apps GitHub independente: **WorkNotes Local / Development**, cu callback `http://localhost:5018/Account/GitHub/Callback`, și **WorkNotes Production**, cu callback `https://worknotes.eu/Account/GitHub/Callback`. Fiecare rând păstrează propriile Client ID, Client secret, scopes și callback URL. `ASPNETCORE_ENVIRONMENT` este sursa autoritară; configurația de build Debug/Release și `#if DEBUG` nu participă la selecție. Lipsa rândului curent produce un mesaj controlat care numește mediul și un warning fără secrete în log. Secretul gol la editare îl păstrează numai pe cel al mediului selectat.
