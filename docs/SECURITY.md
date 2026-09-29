# Securitate

Regulile obligatorii pentru conturi, parole, cookie-uri și secrete sunt în [AGENTS.md › Identitate și autentificare](../AGENTS.md#identitate-și-autentificare) și [AGENTS.md › Configurare și denumire](../AGENTS.md#configurare-și-denumire). Acest document descrie protecțiile implementate și punctele deschise. Nu a fost efectuat un audit de securitate dedicat.

## Autentificarea

- ASP.NET Core Identity: `AddIdentityCore<ApplicationUser>` cu `SignInManager`, stocare EF în `AccountsDbContext` și cookie-urile Identity (`AddIdentityCookies`). Conturile se creează prin `/Account/Register` (prenume, nume, e-mail unic, parolă și confirmare).
- Autentificarea (`/Account/Login`) folosește `PasswordSignInAsync` cu blocare la eșec; mesajul de eșec este același pentru cont absent, parolă greșită și cont blocat. După succes utilizatorul ajunge pe pagina principală; nu există parametru `returnUrl`.
- Cookie-ul de autentificare `WorkNotes.Auth`: HttpOnly, SameSite=Lax, `Secure` întotdeauna în afara Development (în Development, ca cererea); cu „Ține-mă minte” este persistent 14 zile, cu reînnoire (sliding expiration), altfel cookie de sesiune. `LoginPath` și `AccessDeniedPath` sunt `/Account/Login`.
- Deconectarea se face numai prin `POST /Account/Logout` cu antiforgery și revine la pagina principală; GET răspunde 405.
- Schimbarea parolei (`/Account/ChangePassword`) verifică parola actuală, actualizează security stamp-ul și reface sesiunea curentă cu `RefreshSignInAsync`; celelalte sesiuni sunt invalidate la următoarea validare standard Identity (implicit cel mult 30 de minute).
- Numele și prenumele sunt adăugate în claims (`AccountClaimsPrincipalFactory`) și afișate în header.
- Nu există roluri, confirmarea e-mailului, recuperarea parolei, autentificare în doi pași sau conturi externe; ele nu se implementează fără o cerință nouă.
- Referință: [configurarea ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0).

## Autorizarea

- La nivel de pagină: `[Authorize]` pe `/Contexts`, `/Account`, `/Account/ChangePassword` și `/Account/GitHub` (inclusiv callback-ul); `[AllowAnonymous]` pe autentificare, înregistrare și schimbarea limbii. Pagina principală este publică: vizitatorul vede panoul de bun venit, iar handlerele ei cer autentificarea (redirect la autentificare sau 401 JSON).
- La nivel de resursă, regulile sunt în Business și sunt aplicate și în interogările repository-urilor:
  - un context este vizibil numai membrilor lui (orice rol); numai `Owner` îl editează, îl șterge și îi gestionează membrii;
  - orice membru poate crea note în context; numai proprietarul notei o editează, o redenumește, o șterge și îi schimbă locul pe tablă.
- O resursă din afara apartenenței răspunde 404, fără a-i confirma existența; o acțiune rezervată proprietarului, cerută de un membru, răspunde 403.

## Stocarea parolelor

- Parolele sunt gestionate exclusiv de Identity: baza păstrează numai hash-ul standard (`Users.PasswordHash`), produs de hasher-ul implicit Identity; aplicația nu implementează hashing propriu.
- Politica (lungime minimă, clase de caractere, blocarea după încercări eșuate) este configurată în `WorkNotes.DataAccess/DependencyInjection.cs` și în `WorkNotes.Business/Models/AccountRules.cs`; valorile obligatorii sunt în [AGENTS.md](../AGENTS.md#identitate-și-autentificare). Validarea client (`validation.js`) este doar confort.
- Parolele nu apar în loguri, URL-uri, mesaje, TempData sau rezultate.

## Accesul utilizatorilor la propriile note

Filtrul `VisibleTo` din `NoteRepository` se aplică la tablă, editor, card și salvare. O notă este vizibilă unui utilizator numai dacă:

1. nu este arhivată (`ArchivedAtUtc` este null);
2. utilizatorul este membru al contextului notei;
3. utilizatorul este proprietarul ei sau nota are vizibilitatea `Context`.

Notele noi sunt private (`Private`). O notă `Context` este citită de membri numai în modul read-only; modificările filtrează din nou după proprietar. Schimbarea vizibilității nu are încă interfață. Principiul din planificare: un eventual rol de administrator nu dă acces la notele private.

## Validarea datelor

- Intrările trec prin DataAnnotations în Web, apoi prin regulile Business, apoi prin constrângerile SQL ([CODING-STANDARDS.md](CODING-STANDARDS.md#validare)).
- Salvarea din editor este validată complet pe server: cel mult 5000 de paragrafe și 1 000 000 de caractere, ID-uri nenule și unice, text nevid fără caractere de control (în afară de rând nou și Tab), titlu de cel mult 200 de caractere; un ID de paragraf nou nu poate prelua un paragraf existent al altei note.
- Versiunea trimisă de editor (Base64, 8 octeți) este verificată; o valoare invalidă este tratată ca un conflict.

## Protecția CSRF

- Razor Pages validează automat tokenul antiforgery pentru toate handlerele POST. Formularele îl primesc prin tag helper-e; cererile `fetch` îl trimit prin `FormData` din formularele randate de server (redenumire, schimbul ordinii) sau prin antetul `RequestVerificationToken` (salvarea JSON din editor, cu tokenul din `Html.AntiForgeryToken()`).
- Toate modificările, schimbarea limbii și deconectarea folosesc POST. Singura excepție este callback-ul OAuth GitHub (`GET /Account/GitHub/Callback`), protejat de `state` ([mai jos](#conectarea-github)). Handlerele GET nu modifică date; adresele handlerelor POST deschise prin GET doar redirecționează.
- Cookie-urile de autentificare și de cultură sunt SameSite=Lax.

## Protecția XSS

- Razor codifică implicit ieșirea HTML; aplicația nu folosește `Html.Raw`.
- Datele pentru scripturi sunt serializate cu `Json.Serialize` în `<script type="application/json">`; encoderul implicit System.Text.Json codifică `<`, `>`, `&`, `'` și `"` (verificat pe 2026-09-25), deci textul utilizatorului nu poate închide elementul `script`.
- Scripturile scriu textul cu `textContent`. Singura inserare de HTML (`innerHTML` în `note-editor.js`) primește fragmentul unui tab randat și codificat de server (`?handler=NoteTab`); PR #4 adaugă una de același fel în `notes-board.js`, pentru fereastra editorului (`?handler=NoteEditor`).
- Nu este configurat un antet Content-Security-Policy. TODO: Necesită clarificare — dacă se adaugă CSP și alte antete de securitate pentru mediile găzduite.

## Siguranța conținutului editorului

- Conținutul notelor este text simplu: CodeMirror afișează text, cardurile îl afișează codificat, iar fără JavaScript paragrafele sunt randate codificat, numai pentru citire. Nu se interpretează HTML sau Markdown.
- Textul se păstrează în `NoteBlocks.Content` (nvarchar(max)), cu terminațiile de rând normalizate la `\n`; caracterele de control sunt respinse.
- Fiecare salvare este condiționată de versiunea notei, deci un editor învechit nu poate suprascrie modificări mai noi.

## Referințele interne

- Pe `main` nu există referințe între note.
- PR #4 (neintegrat, [ADR-003](decisions/ADR-003-internal-references.md)) adaugă referințe interne CR/bug. Contextul rămâne granița: o referință deschide numai note ale aceluiași context, nearhivate, pe care proprietarul paragrafului le poate vedea (ale lui sau partajate cu contextul), deci legăturile nu dezvăluie notele private ale colegilor.
- Un cititor vede ca link numai referințele stocate cu cel puțin o notă destinație pe care o poate vedea el însuși, iar linkul deschide numai acele note (`GetTargetsAsync` aplică `VisibleTo` pentru notă și pentru fiecare destinație); celelalte rămân text simplu, fără titlurile destinațiilor.
- Referințele se citesc din text și se rezolvă numai pe server (`NoteReferenceRules`, `NoteReferenceService`); editorul primește pozițiile linkurilor și titlurile destinațiilor ca date JSON codificate, iar CodeMirror afișează textul ca text. Fără JavaScript, paragrafele sunt HTML codificat, cu linkuri `/?note={id}`. HTML-ul scris într-o notă rămâne text.
- Sertarul referințelor din editor (PR #4) este construit din aceleași linkuri și destinații ca textul, deci arată numai referințele și notele pe care cititorul le vede deja ca linkuri; serverul îl randează codificat, iar intrările refăcute după o salvare sunt copii ale template-urilor, completate cu `textContent`.
- Căutarea referinței abia scrise (PR #4, `POST /?handler=ReferenceLookup&note={id}`) folosește antiforgery ca salvarea și răspunde numai proprietarului notei: un cititor primește 403, o notă pe care utilizatorul nu o vede 404. Trimite cel mult textul rândului de dinaintea cursorului (`NoteReferenceRules.LookupLength`, 79 de caractere), în corpul cererii, nu în adresă; serverul citește numai sfârșitul lui și arată numai notele pe care proprietarul le poate vedea, ca la salvare. Mesajele sunt compuse pe server, iar popup-ul le scrie cu `textContent`.
- Recalcularea după schimbarea titlului sau crearea unei note, ca și ștergerea unei note, modifică legăturile paragrafelor tuturor membrilor contextului, nu textul lor: legăturile sunt derivate din text și titluri, iar fiecare paragraf urmează ce poate vedea proprietarul lui. Numai proprietarul notei o salvează, o redenumește sau o șterge.
- `004_ReplaceNoteReferences.sql` modifică textul paragrafelor care conțin legături de forma veche `[[note:{id}|{număr}]]`, înlocuindu-le cu numărul afișat; `005_CreateNoteReferenceTargets.sql`, `006`–`008` (catalogul `WorkReferences` și cheia lui în `NoteReferences`) și `009`–`012` (tipurile și reindexarea) schimbă numai tabelele referințelor, nu textul. Toate se aplică numai la cerere explicită, de cine are drepturi asupra bazei.
- Tipurile de referință (`ReferenceTypes`, PR #4) sunt o configurare comună, schimbată numai în SQL, de cine are drepturi asupra bazei; aplicația doar le citește. Expresia regulată se construiește numai din tipuri validate (1–10 litere ASCII mari), nu din text introdus de utilizatori.
- Catalogul `WorkReferences` (PR #4) este comun tuturor contextelor, dar un rând conține numai tipul și numărul unei referințe stocate, fără text, notă, context sau utilizator; aplicația nu îl afișează și nu îl folosește pentru acces: o legătură trece mereu prin paragraf, notă și context, cu filtrele de mai sus.
- Pentru asocierile planificate cu referințele de lucru (`NoteWorkReferences`, `NoteBlockWorkReferences`), toate trebuie să respecte contextul notei ([DOMAIN-MODEL.md](DOMAIN-MODEL.md#entități-planificate)). TODO: Necesită clarificare — catalogul fiind comun, titlul și URL-ul extern planificate pentru un CR ar fi vizibile în toate contextele; dacă ele trebuie păstrate pe context.

## Secretele

- `appsettings.json` conține numai conexiunea locală de dezvoltare, cu Windows Authentication (`Integrated Security=True`), fără utilizator sau parolă.
- Credențialele și alte secrete (inclusiv `GitHub:ClientSecret`) se configurează prin User Secrets sau variabile de mediu și nu se salvează în Git. Proiectul Web are `UserSecretsId` (versiunea 0.03).
- Pentru găzduire: cheile Data Protection (care protejează cookie-urile și tokenurile antiforgery) trebuie să fie persistente, protejate și comune instanțelor, dacă sunt mai multe. Din versiunea 0.03 ele protejează și tokenurile GitHub stocate. `Program.cs` fixează numele aplicației (`WorkNotes`) și, cu `DataProtection:KeysPath`, păstrează cheile într-un folder (criptate cu DPAPI pe Windows); fără setare se folosește folderul implicit al utilizatorului procesului. TODO: Necesită clarificare — folderul și protecția cheilor în mediile Test și Production.
- `AllowedHosts` este `*`. TODO: Necesită clarificare — lista de host-uri permise în producție.

## Conectarea GitHub

Regulile obligatorii sunt în [AGENTS.md › Integrarea GitHub](../AGENTS.md#integrarea-github); decizia este [ADR-004](decisions/ADR-004-github-oauth.md).

- Fluxul este OAuth authorization code cu PKCE (S256) și `state`: 32 de octeți aleatori fiecare (`RandomNumberGenerator`), comparați în timp constant. `state` și `code_verifier` stau în cookie-ul `WorkNotes.GitHubAuthorization`, criptat și semnat cu Data Protection pentru utilizatorul curent (ID-ul lui face parte din scopul protectorului), valabil 10 minute, HttpOnly, SameSite=Lax, `Secure` în afara Development, cu calea `/Account/GitHub`, șters la callback. Un callback fără cookie, cu alt `state`, al altui utilizator sau expirat nu schimbă codul și nu salvează nimic, nici măcar o eroare.
- Callback-ul acceptă numai o conectare pornită prin POST cu antiforgery (`Connect`) de același utilizator, în același browser. Adresa de redirecționare către GitHub este construită pe server, din configurație; nu vine din cerere.
- Tokenurile nu ajung în browser: pagina afișează login-ul contului și datele, `GitConnection` nu conține tokenuri, iar `GitTokens` își lasă tokenurile afară din `ToString()`. Clientul HTTP trimite tokenurile numai în corpul cererilor și în antetul `Authorization`, niciodată în adrese, deci logurile `HttpClient` (care conțin adresele) nu le conțin.
- În bază, `ProtectedAccessToken` și `ProtectedRefreshToken` sunt criptate cu Data Protection (scopul `WorkNotes.GitConnections.Tokens`); o copie a bazei fără cheile aplicației nu le dezvăluie. Tokenurile care nu se mai pot decripta cer reconectarea și se pot deconecta.
- Accesul: fiecare utilizator vede și modifică numai conexiunea lui (`UserId` din claims, filtrul în fiecare interogare a repository-ului). Deconectarea șterge rândul și revocă autorizarea la GitHub; ștergerea unui utilizator îi șterge conexiunea în cascadă.
- Permisiunile sunt ale aplicației GitHub înregistrate: recomandat o GitHub App numai cu citire (Contents, Metadata, Pull requests), cu tokenuri care expiră după 8 ore și se reîmprospătează. O OAuth App cere scopul `repo` pentru repository-uri private, care permite și scrierea.
- Erorile furnizorului sunt coduri de stare (`Rejected`, `Unavailable`) traduse în mesaje localizate, fără detalii tehnice; răspunsurile GitHub nu se afișează.

## Logging

- Se folosește numai configurația implicită de logging ASP.NET Core; codul aplicației nu scrie loguri proprii ([ARCHITECTURE.md](ARCHITECTURE.md#logging)).
- Parolele și corpurile cererilor de cont nu se jurnalizează; detaliile excepțiilor nu se afișează utilizatorilor în afara Development.

## Transportul

- În afara Development: HSTS și redirecționare HTTPS; HTTPS este obligatoriu pentru găzduire.
- Redirecționările după schimbarea limbii acceptă numai adrese locale (`Url.IsLocalUrl`, `LocalRedirect`).

## Uploaduri

Aplicația nu acceptă fișiere încărcate de utilizatori. Imaginile (sigla, banda post-it-urilor, favicon-ul) sunt fișiere statice din `wwwroot`.
