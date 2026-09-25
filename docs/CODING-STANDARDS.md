# Standarde de cod

Regulile obligatorii sunt în [AGENTS.md](../AGENTS.md) și nu se repetă aici; acest document le leagă de convențiile existente în cod, care trebuie urmate pentru consecvență. Codul nou se scrie ca în fișierele din jur: aceeași densitate a comentariilor, aceleași denumiri și aceleași idiomuri.

## Principii SOLID

Regula: [AGENTS.md › SOLID, interfețe și dependency injection](../AGENTS.md#solid-interfețe-și-dependency-injection). Cum se aplică în cod:

- Responsabilitate unică: `NoteRules` validează și normalizează, `NoteService` aplică regulile de acces și ordinea, `NoteRepository` persistă, `IndexModel` orchestrează și traduce rezultatele, `NoteDates` formatează datele.
- Deschis/închis: rezultatele sunt coduri de stare (`NoteSaveStatus`, `WorkContextSaveStatus` etc.), iar Web le mapează într-un singur `switch`; un caz nou se adaugă fără a schimba contractele existente.
- Substituție: stub-urile din teste înlocuiesc repository-urile fără a schimba semantica rezultatelor, a excepțiilor și a anulării.
- Segregarea interfețelor: `IAccountService` (cont) și `IAuthenticationService` (autentificare) sunt separate, deși aceeași clasă, `IdentityAccountService`, le implementează.
- Inversarea dependențelor: Business definește `I…Repository`; DataAccess le implementează; Web compune totul în `Program.cs`.

## C\#

- Namespace-uri file-scoped (`namespace WorkNotes.Business.Services;`), `ImplicitUsings` activat, un tip principal pe fișier.
- Dependențele se primesc prin primary constructors: `public sealed class NoteService(INoteRepository notes, IWorkContextRepository contexts, TimeProvider time) : INoteService`.
- Clasele sunt `sealed` când nu sunt gândite pentru moștenire; entitățile generate rămân `partial`.
- DTO-urile imutabile sunt `sealed record`; actualizările folosesc `with`.
- Regulile și limitele stau în clase statice `*Rules` (`NoteRules`, `WorkContextRules`, `AccountRules`), cu constante pentru lungimi și metode `Valid…` / `Normalize…`. Limitele corespund coloanelor SQL.
- Valorile stocate ca text (`NoteTypes`, `NoteVisibilities`, `ContextRoles`) sunt constante care corespund exact constrângerilor `CHECK` din SQL; o valoare nouă cere și un script SQL.
- Colecțiile din contracte sunt `IReadOnlyList<T>`; se folosesc collection expressions (`[]`) și expresii `switch`.
- Timpul vine din `TimeProvider` injectat, nu din `DateTime.Now`: ziua jurnalului și lunile tablei folosesc calendarul local al aplicației, auditul se păstrează în UTC, trunchiat la secundă.

## Nullable reference types

- Activate în toate proiectele (`<Nullable>enable</Nullable>`) și nu se dezactivează.
- `?` marchează valorile opționale (de exemplu `string? Title`, unde null înseamnă „Fără titlu”); comentariile interfețelor spun ce înseamnă un rezultat null („Null when the note does not exist or the user may not see it”).
- `null!` apare numai în entitățile generate; operatorul `!` se folosește numai acolo unde valoarea este garantată de context (de exemplu ID-ul utilizatorului pe o pagină autorizată).

## async/await și CancellationToken

Regula: [AGENTS.md › Acces asincron și anulare](../AGENTS.md#acces-asincron-și-anulare). Convenții:

- Orice metodă asincronă are sufixul `Async` și primește `CancellationToken cancellationToken` ca ultim parametru; handlerele Razor Pages îl primesc de la model binding (anularea cererii), iar view component-ul folosește `HttpContext.RequestAborted`.
- Serviciile verifică argumentele și anularea înainte de accesul la date: `ArgumentException.ThrowIfNullOrWhiteSpace(userId);` și `cancellationToken.ThrowIfCancellationRequested();`.
- Metodele care doar deleagă întorc direct `Task`-ul, fără `async`/`await` inutil.

## Denumiri

Regula de bază: [AGENTS.md › Configurare și denumire](../AGENTS.md#configurare-și-denumire).

| Element | Convenție | Exemple |
| --- | --- | --- |
| Interfețe | `I…`, în `WorkNotes.Business/Abstractions` | `INoteService`, `INoteRepository` |
| Servicii / repository-uri | `…Service` / `…Repository` | `NoteService`, `NoteRepository` |
| Rezultate | `…Status` (enum), `…Result` (record) | `NoteSaveStatus`, `NoteSaveResult` |
| Reguli și constante | `…Rules`, plural pentru constante | `NoteRules`, `NoteTypes`, `ContextRoles` |
| Intrări Web | `…Input`, corpuri JSON `…Request` | `NewNoteInput`, `NoteSaveRequest` |
| Pagini | `…Model` pentru PageModel | `IndexModel`, `LoginModel` |
| Partiale | prefix `_` în `Pages/Shared` | `_NoteCard.cshtml` |
| Chei .resx | `Prefix_Nume` | `Notes_DeleteConfirm` ([LOCALIZATION.md](LOCALIZATION.md#convenții-pentru-chei)) |
| Obiecte SQL | vezi [DATABASE.md](DATABASE.md#convenții) | `IX_Notes_ContextId_Order` |

Când numele unei entități coincide cu al unui model Business, repository-ul folosește un alias: `using NoteEntity = WorkNotes.DataAccess.Entities.Note;`.

## Interfețe

- Mici și orientate spre consumatori; o interfață de repository expune numai ce folosesc serviciile.
- Contractele întorc modele Business sau tipuri simple, niciodată entități, `IQueryable` sau tipuri SQL.
- Membrii au comentarii scurte care descriu semantica: ce înseamnă null, când apare conflictul, ce filtru de acces se aplică.

## Servicii

- Verifică argumentele și anularea, validează și normalizează intrările prin `*Rules`, verifică apartenența și proprietatea prin repository-uri, apoi apelează persistența.
- Întorc coduri de stare stabile, nu texte localizate, excepții pentru cazuri așteptate sau concepte HTTP.
- Nu cunosc Web, EF Core, SQL sau Identity.

## Controllere și PageModel

- Aplicația folosește Razor Pages; nu există controllere MVC. Dacă apar, respectă aceleași reguli.
- PageModel-ul este subțire: leagă intrarea, apelează serviciul, mapează codul de stare la `ModelState` (cu mesaje din .resx), la cod HTTP sau la cheia mesajului din TempData (`TempData.SetStatusMessage`), apoi redirecționează (Post/Redirect/Get).
- Paginile protejate au `[Authorize]`; paginile publice de cont au `[AllowAnonymous]`. Handlerele POST care sunt deschise direct prin GET redirecționează la pagină.
- Handlerele JSON întorc `{ message }` localizat și codul HTTP potrivit.

## Validare

Patru niveluri, fiecare cu rolul lui:

1. Client: `validation.js` citește atributele `data-val-*` generate de Razor; este numai confort.
2. Server, Web: DataAnnotations pe ViewModel-uri, cu `ErrorMessage` și `Display(Name = …)` ca chei .resx.
3. Business: regulile autoritare (`NoteRules.ValidTitle`, `WorkContextRules.ValidName`, `AccountRules.ValidEmail`), cu normalizare prin `Trim` și respingerea caracterelor de control.
4. SQL: constrângeri `CHECK`, chei unice și externe, ca ultimă protecție, inclusiv la concurență.

## Tratarea excepțiilor

- Cazurile așteptate sunt coduri de stare, nu excepții.
- DataAccess traduce numai erorile cunoscute: `SqlException` 2601/2627 (unicitate), 547 (cheie externă) și `DbUpdateConcurrencyException`; entitatea rămasă în starea eșuată este detașată (`EntityState.Detached`) sau `ChangeTracker` este golit.
- Restul excepțiilor se propagă până la handlerul global ([ARCHITECTURE.md](ARCHITECTURE.md#tratarea-erorilor)); nu se înghit, iar anularea nu se transformă în succes sau rezultat gol.
- `ArgumentException` semnalează erori de programare (de exemplu un utilizator lipsă), nu erori de utilizare.

## JavaScript

- JavaScript fără framework și fără dependențe npm la rulare; singura bibliotecă este bundle-ul CodeMirror din `wwwroot/lib/codemirror`, construit din `Solution/tools/codemirror` (`npm ci`, apoi `npm run build`) și versionat împreună cu `THIRD-PARTY-NOTICES.txt`. Bundle-ul nu se editează manual.
- Modulele ES (`notes-board.js`, `note-editor.js`, `status-messages.js`) exportă funcții reutilizate; scripturile mici (`modal.js`, `navigation.js`, `language.js`, `validation.js`) sunt clasice, încărcate cu `defer`.
- Progressive enhancement: operațiile de bază funcționează fără JavaScript; scriptul preia un link sau un formular randat de server (`event.preventDefault()`) și păstrează aceeași adresă ca rezervă.
- Punctele de legătură sunt atributele `data-*` (`data-note-open`, `data-editor-save`), nu clasele CSS.
- Fără stiluri inline și fără poziționare din script: se adaugă sau se elimină numai clase CSS definite în foile de stil.
- Fără texte sau traduceri în JavaScript: textele vin din atribute `data-*`, din template-uri și din JSON-ul randat de server; textul se scrie cu `textContent`.
- Cererile `fetch` trimit tokenul antiforgery (prin `FormData` din formularul randat de server sau prin antetul `RequestVerificationToken`) și `Accept: application/json` când așteaptă JSON.
- Modulele comunică prin evenimente (`note-editor:open`, `note-board:versions`), nu prin variabile globale.
- Fiecare funcție mai mare are un comentariu care descrie comportamentul; numele interne sunt camelCase.

## CSS

- Culorile, umbrele, razele și tranzițiile sunt tokenuri `--wn-*` din `tokens.css`; valorile noi de paletă se adaugă numai acolo ([UI-UX.md](UI-UX.md#paletă)).
- Câte un fișier pe zonă: `site.css`, `navigation.css`, `notes-board.css`, `postit.css`, `note-editor.css`.
- Clasele urmează forma bloc / element / modificator: `note-card`, `note-card__title`, `note-card--journal`, iar stările temporare sunt modificatori (`note-cell--dragging`).
- Ținte de minimum 44px pentru controalele principale, focus vizibil, `@media (prefers-reduced-motion: reduce)` și `@media (forced-colors: active)` pentru componentele noi.
- Se folosesc `:has()`, container queries și `color-mix()` acolo unde înlocuiesc JavaScript.

## Razor

- Textele se scriu prin localizatorul injectat în `_ViewImports.cshtml` (`L["Cheie"]`, cu parametri `L["Cheie", valoare]`).
- Datele pentru scripturi se randează ca atribute `data-*` sau ca JSON în `<script type="application/json">` cu `Json.Serialize`.
- Formularele folosesc tag helper-ele (`asp-page`, `asp-page-handler`, `asp-for`), care adaugă tokenul antiforgery.
- Comentariile Razor (`@* … *@`) explică rolul partialului și comportamentul fără JavaScript.

## Comentarii

- Comentariile din cod și din scripturile SQL sunt în engleză și explică motivul sau comportamentul, nu repetă codul.
- Nu se folosesc comentarii XML (`///`).
- Documentația Markdown este în română.

## Evitarea duplicării

- Se reutilizează componentele existente: clasele `btn`, `postit-panel`, `modal`, partialul `_StatusMessage`, ajutoarele `NoteDates` și `NoteCardStyle`, regulile `*Rules`.
- O regulă de business există o singură dată, în Business; Web și JavaScript nu o reimplementează.
- Nu se adaugă DTO-uri, interfețe sau clase care doar repetă aceeași structură ([AGENTS.md](../AGENTS.md#configurare-și-denumire)).

## Dimensiunea și responsabilitatea claselor și metodelor

- Regula existentă este calitativă: o responsabilitate clară pe clasă. Metodele publice ale serviciilor corespund unei operații de business.
- TODO: Necesită clarificare — nu există limite numerice aprobate pentru dimensiunea claselor sau a metodelor.

## Fișiere

- Codificare UTF-8 și terminații LF; fișierele generate prin scaffolding se aduc la LF înainte de commit.
- Nu există `.editorconfig` sau `.gitattributes` în repository.
