# Analiza deblocării configurării GitHub OAuth

## Concluzie executivă

Mesajul „Integrarea GitHub nu este disponibilă momentan. Lipsesc setările tehnice ale aplicației.” nu indică o funcționalitate OAuth care trebuie construită de la zero. Fluxul, tabela, repository-ul, serviciul și pagina de administrare există deja în cod. În starea documentată a mediului lipsește punerea lor în funcțiune: scriptul `Scripts/version_0.03/004_CreateAdministration.sql` nu a fost aplicat de agent, nu a fost creat administratorul inițial, iar existența unui rând configurat și decriptabil în `dbo.GitHubConfigurations` nu este demonstrată. Mesajul din imagine dovedește numai că aplicația nu a obținut credențialele decriptate.

Pentru deblocare sunt necesare, în ordine: aplicarea explicită a scripturilor asupra bazei autorizate, configurarea persistentă a cheilor Data Protection, bootstrap-ul unui administrator, înregistrarea aplicației OAuth la GitHub și salvarea valorilor reale din `/admin/configuration`. Nu este necesară mutarea configurației din `appsettings`: această mutare este deja implementată.

## 1. Sursa mesajului și traseul până la interfață

Textele sunt resurse localizate, nu literale în pagina Razor:

- `GitHub_NotConfiguredHelp` conține mesajul din card, inclusiv îndemnul de a contacta administratorul;
- `GitHub_NotConfigured` conține mesajul scurt afișat ca status după apăsarea butonului;
- aceleași chei există în catalogul neutru/român și în traducerile engleză și poloneză din `Solution/WorkNotes.Resources/Resources/SharedResources*.resx`.

Traseul paginii este:

1. `Pages/Account/GitHub.cshtml` afișează `GitHub_NotConfiguredHelp` când `GitHubModel.IsConfigured` este `false`. Butonul rămâne intenționat vizibil și trimite `POST /Account/GitHub?handler=Connect` (convențional, handler-ul Razor Pages `Connect`).
2. `GitHubModel.OnGetAsync` cere `IGitHubConnectionService.IsConfiguredAsync`; modelul transmis UI este chiar PageModel-ul, cu `IsConfigured`, `HasEmail` și `Connection` (`GitConnection?`). Nu există un DTO separat numai pentru acest card.
3. `GitHubConnectionService.IsConfiguredAsync` deleagă către `IGitHubOAuthClient.IsConfiguredAsync`.
4. `GitHubOAuthClient.IsConfiguredAsync` consideră integrarea configurată numai dacă `IGitHubConfigurationService.GetCredentialAsync` întoarce o configurație.
5. `GitHubConfigurationService.GetCredentialAsync` deleagă către `IGitHubConfigurationRepository`, iar `GitHubConfigurationRepository.GetCredentialAsync` citește singletonul din `WorkNotesDbContext.GitHubConfigurations` și încearcă să decripteze ambele credențiale.
6. Lipsa rândului sau imposibilitatea decriptării `ProtectedClientId` ori `ProtectedClientSecret` întoarce `null` și produce starea „neconfigurat”. Erorile SQL nu sunt convertite în această stare și trebuie să rămână erori operaționale vizibile operatorului.

La apăsarea butonului, `GitHubModel.OnPostConnectAsync` verifică mai întâi că utilizatorul WorkNotes are email, apoi apelează `GitHubConnectionService.StartAuthorizationAsync`. Dacă URL-ul nu poate fi construit deoarece configurația lipsește, handler-ul pune cheia `GitHub_NotConfigured` în `TempData` și redirecționează înapoi la pagină; layout-ul afișează notificarea din imagine.

## 2. Setările obligatorii și cele de transport

### Configurația OAuth globală, citită din baza de date

Formularul și modelul curent folosesc exact patru valori:

| Valoare | Persistență | Regula aplicată la salvare | Folosire |
| --- | --- | --- | --- |
| Client ID | `ProtectedClientId` | obligatoriu după `Trim` | query-ul de autorizare, exchange, refresh și revocare |
| Client secret | `ProtectedClientSecret` | obligatoriu la prima salvare; ulterior câmpul gol păstrează valoarea existentă | exchange, refresh și revocare |
| Scopes | `Scopes` | poate fi gol; altfel acceptă tokenuri formate din litere/cifre/`:`, `_`, `-`, separate prin spații sau virgule | parametrul `scope` din autorizare; omis dacă este gol |
| Callback URL | `CallbackUrl` | URL absolut cu schema `http` sau `https` | `redirect_uri` în autorizare și exchange |

Limitele SQL sunt 500 de caractere pentru scopes și 1000 pentru callback URL. Validarea aplicației nu tratează endpoint-urile GitHub drept parte din această configurație administrativă.

Un detaliu important: la citire, disponibilitatea este definită în prezent prin existența rândului și decriptarea reușită a ambelor credențiale. Repository-ul nu revalidează valorile citite. Valorile create prin pagina admin trec validarea Business; un rând inserat sau alterat manual ar putea fi decriptabil, dar semantic incorect. De aceea configurația trebuie scrisă prin `/admin/configuration`, nu direct prin SQL.

### Endpoint-uri nesensibile, citite din configurația aplicației

`GitHubOptions` păstrează separat:

- `AuthorizationEndpoint`, implicit `https://github.com/login/oauth/authorize`;
- `TokenEndpoint`, implicit `https://github.com/login/oauth/access_token`;
- `ApiBaseAddress`, implicit `https://api.github.com/`;
- `Timeout`, implicit 20 de secunde.

`Program.cs` transmite secțiunea `GitHub` către `AddIntegrations`. `appsettings.json` conține o secțiune `GitHub` goală, deci se folosesc valorile implicite; `appsettings.Development.json` nu adaugă setări GitHub, iar în repository nu există `appsettings.Production.json` sau `Startup.cs`. `ClientId`, `ClientSecret`, `Scopes` și `CallbackUrl` nu sunt citite din `appsettings`, User Secrets sau variabile de mediu. Vechea abordare este înlocuită explicit de configurația din baza de date.

## 3. Starea tabelei și a zonei admin

Implementarea există deja:

- `Scripts/version_0.03/004_CreateAdministration.sql` creează idempotent `AdminUsers` și `GitHubConfigurations`; constrângerea `Id = 1` impune un singur rând global. Scriptul nu inserează niciun secret și nu este aplicat automat.
- `WorkNotesDbContext` expune `GitHubConfigurations`, iar entitatea și maparea corespund coloanelor SQL, inclusiv `CreatedAtUtc` și `UpdatedAtUtc`.
- `GitHubConfigurationRepository` protejează reversibil Client ID și Client secret cu purpose string-ul `WorkNotes.GitHubConfiguration.v1` înainte de `SaveChangesAsync`.
- `GitHubConfigurationService` validează și normalizează datele formularului.
- `/admin` redirecționează la `/admin/configuration`; pagina este protejată cu schema separată `WorkNotes.Admin.Auth` și permite editarea celor patru valori. Secretul nu este încărcat niciodată înapoi în formular.

Administratorul inițial este creat numai dacă operatorul furnizează `AdminBootstrap:Password` la pornire; `AdminBootstrap:UserName` este opțional și are implicit valoarea `admin`. După creare, parola de bootstrap trebuie eliminată din configurația mediului. Aceste valori sunt secrete de deploy, spre deosebire de credențialele GitHub, care se introduc în zona admin.

Prin urmare, întrebarea „tabela există?” are două răspunsuri distincte:

- **în sursele proiectului:** da, scriptul, entitatea, maparea și accesul există;
- **în instanța SQL din imagine:** nu poate fi demonstrat numai din repository. Starea curentă documentează că agentul nu a aplicat scriptul. Verificarea/aplicarea bazei trebuie făcută explicit pe instanța autorizată, nu automat în această analiză.

### De ce nu se poate construi un `INSERT` funcțional din capturile GitHub

Capturile GitHub permit identificarea Client ID-ului și a callback-ului, dar arată explicit că încă trebuie generat un Client secret și nu arată o valoare de secret sau o listă completă de permisiuni/scopes. Client secret nu trebuie transmis în conversații, capturi, Git sau scripturi versionate.

Captura suplimentară din secțiunea **Private keys** nu conține Client secret. Șirul `SHA256:...` este amprenta publică a unei chei private generate pentru GitHub App, nu materialul cheii și nu credentialul OAuth cerut de implementarea WorkNotes. Cheia privată este folosită de fluxurile GitHub App care semnează JWT-uri pentru tokenuri de instalare; clientul curent WorkNotes nu implementează acel flux și nu are câmp pentru PEM/private key. El trimite explicit `client_id` și `client_secret` la exchange și folosește aceeași pereche la revocare. Prin urmare, amprenta nu se salvează în `ProtectedClientSecret`, iar fișierul cheii private nu trebuie încărcat în WorkNotes sau inclus în SQL/Git.

Dacă o captură ulterioară afișează valoarea completă a unui Client secret, acel secret este deja expus și nu mai este acceptabil pentru configurarea aplicației. Faptul că nu a fost încă folosit nu îl face sigur. Se generează un secret nou fără a-l publica, acesta se introduce direct în `/admin/configuration`, apoi secretul expus se revocă în GitHub. Nici documentația, nici fișierul SQL temporar nu trebuie completate cu valoarea expusă.

Mai important, coloanele SQL nu acceptă semantic valorile GitHub în clar: `ProtectedClientId` și `ProtectedClientSecret` trebuie să conțină payload-uri produse de ASP.NET Core Data Protection cu purpose string-ul `WorkNotes.GitHubConfiguration.v1` și cu key ring-ul mediului care va rula aplicația. SQL Server nu poate reproduce singur apelul `IDataProtector.Protect` al aplicației. Un `INSERT` cu Client ID-ul ori secretul în clar ar satisface tipurile SQL, dar `Unprotect` ar eșua, iar integrarea ar rămâne „neconfigurată”. Un payload produs cu key ring-ul altui mediu ar avea același rezultat.

Calea sigură și funcțională pentru prima inserare este `POST /admin/configuration`: repository-ul protejează credențialele cu key ring-ul curent și creează rândul `Id = 1` în aceeași operație. SQL se folosește numai pentru crearea tabelei și pentru verificări care nu expun payload-urile. Nu se adaugă un script de date cu secrete sau placeholder-e care ar putea fi rulate accidental.

Valorile nesensibile observabile sunt callback-ul `https://worknotes.eu/Account/GitHub/Callback` și Client ID-ul afișat în GitHub. Client ID-ul se copiază direct de operator în formularul admin, fără a fi duplicat în documentația sau scripturile versionate. Înainte de salvare, operatorul trebuie să folosească **Generate a new client secret** din secțiunea **Client secrets** și să introducă valoarea afișată atunci direct în formular; **Generate a private key** este altă funcție și nu produce secretul cerut de fluxul existent.

În configurația OAuth observată, redirectul de producție `https://worknotes.eu/Account/GitHub/Callback` corespunde rutei aplicației, iar redirectul local cu portul IIS Express `44389` corespunde profilului versionat. Câmpul **Homepage URL** nu este callback: pentru claritate trebuie să indice pagina de bază `https://worknotes.eu`, în timp ce callback-ul rămâne numai în **Redirect URIs**. Device Flow poate rămâne dezactivat deoarece WorkNotes folosește authorization code cu callback, `state` și PKCE. Scopes nu pot fi deduse din aceste capturi și trebuie confirmate separat înaintea salvării.

## 4. Data Protection

`Program.cs` configurează `AddDataProtection().SetApplicationName("WorkNotes")`. Dacă `DataProtection:KeysPath` este setat, cheile sunt scrise în acel director; pe Windows sunt protejate suplimentar cu DPAPI. Dacă setarea lipsește, ASP.NET Core folosește depozitul implicit al utilizatorului procesului.

Nu există tabelă SQL pentru cheile Data Protection și codul nu configurează persistența cheilor în baza de date. Folosirea depozitului implicit poate supraviețui unui restart în același profil, dar nu oferă garanția necesară pentru un deploy care schimbă utilizatorul, mașina/containerul ori discul și nici pentru mai multe instanțe.

### De ce un director și nu aceeași bază de date

Persistența este obligatorie pentru **cheile Data Protection**, nu obligatoriu pentru un anumit tip de mediu de stocare. Directorul persistent este mecanismul implementat acum, nu singura opțiune posibilă. Client ID, Client secret și tokenurile sunt deja în SQL Server, dar sub formă de payload-uri criptate; cheile Data Protection sunt materialul care permite decriptarea lor.

Stocarea key ring-ului în aceeași bază de date este posibilă tehnic, dar nu înseamnă doar mutarea valorilor existente în `GitHubConfigurations`. Ar necesita o tabelă separată pentru chei, integrarea providerului Data Protection cu EF Core, script SQL Database First și teste de restart/deploy. În plus, dacă payload-urile și cheile neprotejate sunt în aceeași bază, compromiterea acelei baze oferă ambele componente. Pentru păstrarea separării de securitate, cheile din SQL ar trebui protejate la rândul lor cu o cheie externă (de exemplu un certificat sau un serviciu de management al cheilor); apare inevitabil o rădăcină de încredere în afara tabelei cu secretele aplicației.

Avantajul directorului este că implementarea există deja, funcționează înaintea accesului la DbContext și permite separarea drepturilor dintre baza cu payload-uri și key ring. Dezavantajul este necesitatea unui volum persistent și comun instanțelor. Avantajul SQL este administrarea centralizată și accesul comun pentru mai multe instanțe; dezavantajele sunt schimbarea de schemă/dependențe, disponibilitatea bazei pentru operațiile Data Protection și necesitatea protejării cheilor la repaus.

Pentru deblocarea imediată, planul descrie comportamentul deja implementat: `DataProtection:KeysPath`. Dacă se decide explicit că key ring-ul trebuie păstrat în SQL Server, aceasta este o schimbare separată de arhitectură și schemă, nu o condiție pentru crearea rândului `GitHubConfigurations`. Indiferent de mediu, cheia nu poate fi pierdută la restart/deploy; altfel rândul rămâne în SQL, dar nu mai poate fi decriptat.

În mediile găzduite trebuie configurat un `DataProtection:KeysPath` care:

- este persistent peste restart și deploy;
- este comun tuturor instanțelor aceleiași aplicații;
- este accesibil numai identității aplicației și are drepturi de scriere;
- nu este publicat ori versionat cu aplicația și este inclus în strategia de backup;
- păstrează același application name, `WorkNotes`.

Pierderea sau schimbarea cheilor face imposibilă decriptarea configurației GitHub și a tokenurilor utilizatorilor deja stocate. În acel caz, pagina va raporta configurația drept „neconfigurată”; remedierea este recuperarea cheilor originale sau reintroducerea configurației și reconectarea utilizatorilor, nu copierea secretelor în clar în SQL.

## 5. Butonul și fluxul OAuth existent

Ruta efectivă a butonului este `POST /Account/GitHub?handler=Connect`, cu antiforgery generat de formularul Razor. Fluxul este:

1. `OnPostConnectAsync` confirmă emailul utilizatorului WorkNotes.
2. `StartAuthorizationAsync` generează criptografic `state` și `code_verifier`, apoi calculează provocarea PKCE S256.
3. `GitHubOAuthClient.GetAuthorizationUrlAsync` citește configurația curentă din baza de date. Dacă lipsește sau nu poate fi decriptată, întoarce `null` și nu are loc niciun redirect.
4. Dacă există, clientul construiește URL-ul de la `GitHubOptions.AuthorizationEndpoint`, cu `client_id`, `redirect_uri`, `state`, `code_challenge`, `code_challenge_method=S256`, `allow_signup=false` și, dacă nu este gol, `scope`.
5. Web salvează `state` și verifier-ul într-un cookie protejat, valabil 10 minute și legat de utilizator, apoi redirecționează browserul la GitHub.
6. GitHub revine prin `GET /Account/GitHub/Callback`. Handler-ul consumă cookie-ul, Business compară `state` în timp constant, clientul schimbă codul folosind Client ID, Client secret, callback URL și verifier-ul PKCE, citește identitatea GitHub și salvează conexiunea utilizatorului.

Oprirea actuală are loc la pasul 3: `GetCredentialAsync` întoarce `null`. Cauzele posibile sunt numai lipsa rândului sau imposibilitatea decriptării uneia dintre cele două credențiale; dacă tabela însăși lipsește ori SQL nu este disponibil, rezultatul așteptat este o eroare SQL, nu mesajul benign din imagine.

## 6. Callback URL

Ruta este deja stabilită de cod: `/Account/GitHub/Callback`. URL-ul complet trebuie să fie URL-ul public real al mediului plus această rută și trebuie introdus identic atât în aplicația GitHub, cât și în `/admin/configuration`.

Profilele locale publicate în repository sunt:

- `http://localhost:5018/Account/GitHub/Callback` pentru profilul HTTP;
- `https://localhost:7190/Account/GitHub/Callback` pentru profilul HTTPS.

Pentru online forma este `https://<hostul-real>/Account/GitHub/Callback`; hostul concret nu trebuie inventat. Dacă aplicația rulează sub un PathBase sau în spatele unui proxy, URL-ul înregistrat trebuie să includă adresa externă reală. GitHub trebuie să accepte exact callback-ul ales, iar reverse proxy-ul trebuie configurat corect pentru HTTPS și host.

## 7. Configurație globală versus conexiune per utilizator

Separarea cerută există:

- `GitHubConfigurations` este singletonul global al aplicației și conține credențialele OAuth protejate, scopes și callback URL;
- `GitConnections` are cheia compusă `UserId` + `Provider`, cheie externă spre `Users`, identitatea GitHub (`AccountId`, `AccountLogin`), tokenurile access/refresh protejate, expirările, scopes și momentele conectării/validării.

Conexiunea nu duplică emailul. Asocierea este făcută prin cheia internă stabilă `UserId`; emailul utilizatorului autentificat este doar o precondiție verificată înaintea operațiilor GitHub. Scriptul `Scripts/version_0.03/001_CreateGitConnections.sql`, entitatea, maparea și repository-ul există, dar aplicarea scriptului pe baza țintă trebuie verificată separat. Pentru ecranul din imagine, prioritatea imediată rămâne configurația globală; după redirect, tabela `GitConnections` devine necesară pentru finalizarea callback-ului.

### Ce înseamnă „globală” și ce rol are emailul

Configurația globală **nu este contul GitHub al administratorului** și nu limitează conectarea la acel cont. Ea reprezintă identitatea aplicației WorkNotes în relația cu GitHub: o singură înregistrare OAuth, cu un singur Client ID, Client secret și callback, folosită pentru a porni autorizarea fiecărui utilizator WorkNotes. Client secret dovedește către GitHub identitatea aplicației, nu identitatea utilizatorului.

Utilizatorul final trebuie să fie deja autentificat în WorkNotes și să aibă un email nevid în profilul WorkNotes. După apăsarea butonului, GitHub își afișează propria pagină de autentificare/autorizare. Utilizatorul se autentifică direct la GitHub cu metoda acceptată de GitHub pentru contul său; WorkNotes nu primește parola și nu îi trimite GitHub adresa de email din WorkNotes.

Codul curent nu cere, nu citește și nu compară emailul contului GitHub. După autorizare, apelează endpoint-ul GitHub `user` și acceptă identitatea dacă răspunsul are un ID numeric pozitiv și un login nevid. În `GitConnections` salvează legătura dintre `Users.Id` din WorkNotes și `AccountId`/`AccountLogin` din GitHub. Astfel:

- emailul WorkNotes și emailul GitHub pot fi diferite;
- emailul GitHub poate fi privat și fluxul funcționează fără scope-ul `user:email`;
- aceeași configurație globală permite fiecărui utilizator WorkNotes eligibil să își autorizeze propriul cont GitHub;
- un cont GitHub nu este folosit pentru autentificarea în WorkNotes; OAuth realizează numai conectarea contului extern la sesiunea WorkNotes existentă;
- accesul efectiv poate fi totuși restricționat de GitHub, de organizațiile utilizatorului sau de tipul și permisiunile aplicației GitHub, nu de o comparație de email făcută de WorkNotes.

Prin urmare, formularea exactă a condiției este: aplicația obține configurația globală atunci când există rândul singleton `Id = 1`, iar `ProtectedClientId` și `ProtectedClientSecret` pot fi decriptate cu key ring-ul curent. Aceasta deblochează redirectul pentru toți utilizatorii WorkNotes care au email în profil; fiecare dintre ei își autorizează apoi separat propriul cont GitHub.

## 8. Plan minim de deblocare

Acesta este un plan operațional, nu o solicitare de schimbare majoră a codului:

1. **Confirmați baza și scripturile.** Pe baza autorizată, verificați și, numai cu acord explicit, aplicați în ordinea documentată scripturile 0.03 necesare, cel puțin `001_CreateGitConnections.sql` și `004_CreateAdministration.sql`. Nu folosiți migrări EF.
2. **Asigurați persistența Data Protection prin mecanismul curent.** Pentru implementarea existentă, alegeți directorul privat/persistent al mediului, acordați permisiuni identității aplicației și setați `DataProtection:KeysPath` înainte de salvarea oricărui secret sau token. Persistența în SQL este o alternativă posibilă, dar necesită o schimbare separată de schemă, provider și protecție la repaus; nu este implementată acum.
3. **Înregistrați aplicația la GitHub.** Folosiți callback-ul exact al mediului (`/Account/GitHub/Callback`) și permisiunile minime necesare. Păstrați Client secret în afara logurilor, Git și comenzilor partajate.
4. **Creați administratorul inițial.** Furnizați temporar `AdminBootstrap:Password` (și, dacă este necesar, numele), porniți aplicația pentru creare, apoi eliminați parola de bootstrap din mediu.
5. **Salvați configurația din admin.** Autentificați-vă la `/admin`, deschideți `/admin/configuration` și salvați Client ID, Client secret, scopes și callback URL. Nu inserați manual payload-uri în SQL.
6. **Verificați disponibilitatea.** Reîncărcați `/Account/GitHub`; mesajul „neconfigurat” trebuie să dispară fără restart, deoarece valorile sunt citite la fiecare operație.
7. **Testați OAuth cap-coadă.** Apăsați „Conectează GitHub”, verificați hostul GitHub și parametrii nesensibili ai redirectului, acceptați/refuzați autorizarea, verificați callback-ul și rândul per utilizator fără a afișa tokenurile.
8. **Testați restartul.** Reporniți/republicați cu același key ring și confirmați că setarea globală și conexiunea existentă rămân decriptabile.

Nu este necesar în acest pas să se modifice serviciul ca să citească din DB, să se creeze zona admin sau să se adauge tabela: toate sunt deja implementate. Eventuala întărire a validării configurației la citire poate fi analizată separat, dar nu este cauza normală a ecranului curent și nu trebuie amestecată cu deblocarea operațională.

## 9. Riscuri și verificări

### Riscuri

- aplicarea scripturilor asupra unei baze greșite;
- callback diferit între GitHub și tabela aplicației;
- key ring temporar, inaccesibil sau diferit între instanțe;
- rotirea/pierderea cheilor înainte de backup;
- inserarea manuală a secretelor în clar sau expunerea lor în loguri, capturi, URL-uri ori output CI;
- schimbarea configurației între pornirea autorizării și callback, caz în care exchange-ul folosește configurația nouă și GitHub poate refuza codul;
- configurarea doar a tabelei globale, fără `GitConnections`, ceea ce permite redirectul dar împiedică persistarea conexiunii la callback.

### Teste de acceptanță

- fără rând: pagina arată „neconfigurat”, iar POST Connect revine cu mesajul scurt;
- cu rând valid: pagina nu mai arată avertismentul, iar Connect redirecționează la endpoint-ul GitHub configurat;
- URL-ul de autorizare conține Client ID, callback, scopes, `state` și PKCE, dar nu Client secret;
- callback valid creează/actualizează un singur `GitConnections` pentru utilizator și provider;
- refuzul, state invalid/expirat și indisponibilitatea GitHub produc stările localizate corespunzătoare;
- payload-urile SQL pentru credențiale și tokenuri nu coincid cu valorile în clar;
- după restart/deploy cu același `DataProtection:KeysPath`, configurația și tokenurile se decriptează;
- resursele ro/en/pl și testele automate existente rămân valide.

În toate verificările, Client secret, access token, refresh token, parolele și conținutul cookie-urilor protejate trebuie mascate sau omise complet.
