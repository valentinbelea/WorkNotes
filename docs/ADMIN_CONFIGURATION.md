# Administrare și configurarea GitHub

Zona de administrare este separată de aplicația utilizatorilor și se deschide direct la `/admin`; nu există link spre ea în meniul public. Cererile neautentificate sunt trimise la `/admin/login`, iar cookie-ul `WorkNotes.Admin.Auth` nu autentifică utilizatorul în zona publică și nici cookie-ul public în zona admin.

## Schema și instalarea

`Scripts/version_0.03/004_CreateAdministration.sql` creează defensiv:

- `AdminUsers`, cu nume unic, hash de parolă, stare activă și momentele creării, actualizării și ultimei autentificări;
- `GitHubConfigurations`, restrânsă prin `Id = 1` la configurația globală unică. `ProtectedClientId` și `ProtectedClientSecret` conțin numai payload-uri ASP.NET Core Data Protection.

Scriptul nu se aplică automat și nu conține nicio parolă sau valoare GitHub.

## Administratorul inițial

Configurați secretele de deploy `AdminBootstrap:UserName` (opțional, implicit `admin`) și `AdminBootstrap:Password`. La prima pornire cu parola configurată, aplicația folosește `PasswordHasher<AdminUser>` pentru a crea administratorul numai dacă numele lipsește. Valoarea nu se păstrează în fișiere versionate; după creare, eliminați `AdminBootstrap:Password` din mediul de rulare. Dacă nu este configurată nicio parolă, aplicația nu face seed. Cerința nu a furnizat valoarea parolei inițiale, deci aceasta trebuie stabilită sigur de operator.

## Configurarea GitHub

Pagina `/admin/configuration` acceptă Client ID, Client secret, scopes și callback URL. Client secret nu este niciodată returnat formularului: pagina arată numai indicatorul „configurat”; un câmp gol îl păstrează, iar o valoare nouă îl înlocuiește. Client ID este decriptat pentru editare, iar ambele credențiale sunt protejate cu scopul `WorkNotes.GitHubConfiguration.v1` înainte de SQL Server. Serviciile nu le jurnalizează.

## Data Protection și deploy

Setați `DataProtection:KeysPath` la un director persistent, privat identității aplicației și comun tuturor instanțelor. `SetApplicationName("WorkNotes")` trebuie să rămână identic. Pe Windows cheile din director sunt protejate suplimentar cu DPAPI; identitatea pool-ului trebuie păstrată la deploy. Directorul nu poate fi temporar, nu trebuie publicat împreună cu aplicația și trebuie inclus în backup. Pierderea ori schimbarea cheilor face imposibilă decriptarea configurației și a tokenurilor existente.

La hosting verificați: aplicarea scriptului `004`, drepturile SQL minime, existența și persistența directorului de chei după restart/deploy, HTTPS, permisiunile identității asupra directorului și absența secretelor din loguri. Testați login invalid/valid, salvarea și păstrarea secretului gol, restartul, logout-ul și redirectarea tuturor rutelor protejate.

## Consumarea configurației de integrare

`GitHubOAuthClient` citește `IGitHubConfigurationService.GetCredentialAsync` pentru fiecare autorizare, exchange, refresh și revocare. Callback URL din tabelă este sursa autoritară pentru ambele cereri OAuth care trimit `redirect_uri`; Web nu îl suprascrie. O salvare administrativă este observată de operațiile ulterioare fără restart. Dacă valorile se schimbă între pornirea autorizării și callback, exchange-ul folosește configurația curentă și GitHub poate refuza cererea; utilizatorul poate porni o autorizare nouă.
