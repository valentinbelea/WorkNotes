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

- La nivel de pagină: `[Authorize]` pe `/Contexts`, `/Account` și `/Account/ChangePassword`; `[AllowAnonymous]` pe autentificare, înregistrare și schimbarea limbii. Pagina principală este publică: vizitatorul vede panoul de bun venit, iar handlerele ei cer autentificarea (redirect la autentificare sau 401 JSON).
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
- Toate modificările, schimbarea limbii și deconectarea folosesc POST. Handlerele GET nu modifică date; adresele handlerelor POST deschise prin GET doar redirecționează.
- Cookie-urile de autentificare și de cultură sunt SameSite=Lax.

## Protecția XSS

- Razor codifică implicit ieșirea HTML; aplicația nu folosește `Html.Raw`.
- Datele pentru scripturi sunt serializate cu `Json.Serialize` în `<script type="application/json">`; encoderul implicit System.Text.Json codifică `<`, `>`, `&`, `'` și `"` (verificat pe 2026-09-25), deci textul utilizatorului nu poate închide elementul `script`.
- Scripturile scriu textul cu `textContent`. Singura inserare de HTML (`innerHTML` în `note-editor.js`) primește fragmentul unui tab randat și codificat de server (`?handler=NoteTab`).
- Nu este configurat un antet Content-Security-Policy. TODO: Necesită clarificare — dacă se adaugă CSP și alte antete de securitate pentru mediile găzduite.

## Siguranța conținutului editorului

- Conținutul notelor este text simplu: CodeMirror afișează text, cardurile îl afișează codificat, iar fără JavaScript paragrafele sunt randate codificat, numai pentru citire. Nu se interpretează HTML sau Markdown.
- Textul se păstrează în `NoteBlocks.Content` (nvarchar(max)), cu terminațiile de rând normalizate la `\n`; caracterele de control sunt respinse.
- Fiecare salvare este condiționată de versiunea notei, deci un editor învechit nu poate suprascrie modificări mai noi.

## Referințele interne

- Pe `main` nu există referințe între note.
- PR #4 (neintegrat) adaugă referințe interne; proiectul PR-ului prevede: sugestii numai din celelalte note ale aceluiași context pe care utilizatorul le poate vedea, numai pentru proprietarul notei; verificarea destinațiilor pe server; o referință care nu se mai poate deschide nu afișează titlul destinației; un număr limitat de ID-uri pe cerere de verificare. Aceste reguli se documentează aici la integrarea PR-ului.
- Pentru referințele de lucru planificate (CR/bug, `WorkReferences`), toate asocierile trebuie să respecte contextul notei ([DOMAIN-MODEL.md](DOMAIN-MODEL.md#entități-planificate)).

## Secretele

- `appsettings.json` conține numai conexiunea locală de dezvoltare, cu Windows Authentication (`Integrated Security=True`), fără utilizator sau parolă.
- Credențialele și alte secrete se configurează prin User Secrets sau variabile de mediu și nu se salvează în Git. Proiectul Web nu are încă `UserSecretsId`; folosirea User Secrets necesită inițializarea lor.
- Pentru găzduire: cheile Data Protection (care protejează cookie-urile și tokenurile antiforgery) trebuie să fie persistente, protejate și comune instanțelor, dacă sunt mai multe. Aplicația nu configurează încă Data Protection. TODO: Necesită clarificare — locul și protecția cheilor în mediile găzduite.
- `AllowedHosts` este `*`. TODO: Necesită clarificare — lista de host-uri permise în producție.

## Logging

- Se folosește numai configurația implicită de logging ASP.NET Core; codul aplicației nu scrie loguri proprii ([ARCHITECTURE.md](ARCHITECTURE.md#logging)).
- Parolele și corpurile cererilor de cont nu se jurnalizează; detaliile excepțiilor nu se afișează utilizatorilor în afara Development.

## Transportul

- În afara Development: HSTS și redirecționare HTTPS; HTTPS este obligatoriu pentru găzduire.
- Redirecționările după schimbarea limbii acceptă numai adrese locale (`Url.IsLocalUrl`, `LocalRedirect`).

## Uploaduri

Aplicația nu acceptă fișiere încărcate de utilizatori. Imaginile (sigla, banda post-it-urilor, favicon-ul) sunt fișiere statice din `wwwroot`.
