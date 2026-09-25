# WorkNotes.Web — reguli specifice

Proiectul de prezentare: Razor Pages, ViewModel-uri, localizare, CSS, JavaScript și compunerea DI. Regulile generale sunt în [AGENTS.md](../../AGENTS.md); interfața în [docs/UI-UX.md](../../docs/UI-UX.md); localizarea în [docs/LOCALIZATION.md](../../docs/LOCALIZATION.md); securitatea în [docs/SECURITY.md](../../docs/SECURITY.md).

## Conținut

- `Program.cs` — serviciile, `AddDataAccess`, cookie-urile și pipeline-ul HTTP; singurul loc care referă `WorkNotes.DataAccess`.
- `Pages/` — `Index.cshtml` (tabla, notele, editorul), `Contexts/Index.cshtml`, `Account/*.cshtml`, `Language.cshtml`; `Pages/Shared/` — layout-ul și partialele (`_NoteCard`, `_NewNoteCard`, `_NoteEditor*`, `_DeleteNoteDialog`, `_StatusMessage`, `_MainMenu`, `_LanguageSelector`).
- `ViewModels/` — intrările formularelor și corpul JSON al editorului; `ViewComponents/` — versiunea din footer.
- `Localization/`, `Messages/`, `Navigation/`, `Notes/` — localizarea, mesajele de salvare, secțiunile meniului, formatele datelor și clasele cardurilor.
- `wwwroot/` — `css/`, `js/`, `images/`, `lib/codemirror/` (bundle generat).

## Reguli

- PageModel-urile sunt subțiri: leagă intrarea, apelează serviciul Business, mapează codul de stare la `ModelState`, la cod HTTP (404 pentru resurse din afara apartenenței, `StatusCode(403)` pentru acțiuni nepermise) sau la cheia din `TempData.SetStatusMessage`, apoi redirecționează. Nicio regulă de business, validare de domeniu sau verificare de drepturi nu se reimplementează aici, în view-uri sau în JavaScript.
- Nu folosiți `DbContext`, `DbSet`, repository-uri concrete, `ApplicationUser`, `UserManager`, `SignInManager` sau SQL în pagini, view-uri și componente; verificarea este în [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md#verificarea-regulilor-arhitecturale). `Microsoft.EntityFrameworkCore.Design` rămâne numai pentru tooling.
- Niciun text afișat nu se scrie direct: folosiți `L["Cheie"]` sau `IStringLocalizer<SharedResources>`, iar în ViewModel-uri chei ca `ErrorMessage` și `Display(Name)`; fiecare cheie nouă se adaugă în cele patru fișiere .resx și se verifică cu `tools/Test-Resources.ps1`.
- Modelele Business se afișează direct; ViewModel-urile sunt numai pentru intrări, cu DataAnnotations.
- Fiecare modificare folosește POST cu antiforgery: tag helper-e pentru formulare; `fetch` cu `FormData` din formularul randat de server sau cu antetul `RequestVerificationToken`. Handlerele JSON întorc `{ message }` localizat și codul HTTP potrivit.
- Operațiile de bază funcționează fără JavaScript: starea overlay-urilor este în URL, iar scriptul preia linkul sau formularul existent.
- JavaScript: numai comportament, fără stiluri inline, fără texte sau traduceri (textele vin din atribute `data-*`, template-uri și JSON randat de server), legături prin atribute `data-*`, text scris cu `textContent`.
- CSS: tokenurile din `tokens.css`, fișierul zonei potrivite, clase bloc / element / modificator, suport pentru reduced-motion și forced-colors.
- `wwwroot/lib/codemirror/codemirror.js` nu se editează manual: se reconstruiește din `Solution/tools/codemirror` (`npm ci`, apoi `npm run build`) și se versionează împreună cu `THIRD-PARTY-NOTICES.txt`.
- Aplicația pornește cu `dotnet run --project WorkNotes.Web --launch-profile http` (din `Solution`, cu SQL Server disponibil); opriți la final instanța pornită pentru verificare.
