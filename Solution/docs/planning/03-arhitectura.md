# 03 — Arhitectură

Regulile complete sunt în [AGENTS.md](../../AGENTS.md); aici este harta a ceea ce există.

## Straturi

| Proiect | Conține |
| --- | --- |
| `WorkNotes.Web` | Razor Pages, layout, partiale, CSS, JavaScript, localizare, compunerea DI (`Program.cs`) |
| `WorkNotes.Business` | Interfețe (`Abstractions`), servicii (`Services`), modele și reguli (`Models`, de exemplu `NoteRules`, `NoteReferenceRules`, `NoteTypes`) |
| `WorkNotes.DataAccess` | `WorkNotesDbContext` și `AccountsDbContext`, entitățile scaffoldate, repository-urile, Identity |
| `WorkNotes.Resources` | `SharedResources*.resx` (ro neutru + ro/en/pl, aceleași chei) |
| `WorkNotes.Business.Tests` | Teste xUnit pentru servicii, fără bază de date |

Dependențe: Web → Business; Web → DataAccess numai în `Program.cs`; DataAccess → Business. Serviciile scoped primesc repository-urile prin interfețe; `CancellationToken` merge de la request până la EF; citirile folosesc `AsNoTracking`.

## Fluxuri

```text
Pages/Contexts → IWorkContextService / IContextMemberService → servicii → repository-uri → WorkNotesDbContext
Pages/Index    → INoteService → NoteService → INoteRepository → NoteRepository → WorkNotesDbContext
Footer         → ApplicationVersionViewComponent → IApplicationVersionService → … → DatabaseVersion
Conturi        → IAccountService / IAuthenticationService → DataAccess/Identity (UserManager, SignInManager)
```

Business întoarce coduri de stare (`NoteSaveStatus`, `WorkContextSaveStatus` etc.) și chei de mesaj; traducerea se face în Web.

## Pagini și adrese

| Adresă | Ce face |
| --- | --- |
| `/` | Dashboardul (tabla contextului ales) sau pagina de bun venit |
| `/?context={id}` | Tabla unui context |
| `/?new=true` | Cardul „Notă nouă” deschis fără JavaScript |
| `/?note={id}` | Editorul peste tablă, cu nota într-un tab |
| `/?delete={id}` | Confirmarea ștergerii unei note |
| `/?handler=NoteTab&note={id}` (GET) | Un tab nou pentru editorul deja deschis (HTML) |
| `/?handler=SaveNote&note={id}` (POST JSON) | Salvarea unei note din editor |
| `/?handler=CreateNote`, `RenameNote`, `DeleteNote` (POST) | Crearea, redenumirea pe loc, ștergerea |
| `/?handler=SwapNotes` (POST, răspuns JSON) | Schimbul a două note din aceeași lună, după drag-and-drop; răspunde cu ordinea lunii și versiunile noi |
| `/?handler=ReferenceSuggestions&note={id}&number={număr}` (GET JSON) | Notele tablei cu numărul întreg în titlu, pentru sugestia de referință (numai pentru proprietarul notei) |
| `/?handler=ReferenceTargets&note={id}&ids=…` (GET JSON) | Care dintre notele indicate de referințe se pot deschide (pentru referințele lipite) |
| `/Contexts`, `?add=true`, `?edit={id}`, `?delete={id}`, `?members={id}` | Contextele și overlay-urile lor |
| `/Account/...` | Înregistrare, autentificare, cont, parolă, deconectare |

Starea overlay-urilor este în URL: fiecare dialog se poate deschide și fără JavaScript, iar `modal.js` îl transformă în dialog modal.

## Componente Web

| Element | Rol |
| --- | --- |
| `Pages/Shared/_NoteCard.cshtml`, `_NewNoteCard.cshtml` | Post-it-urile de pe tablă |
| `Pages/Shared/_NoteEditorDialog.cshtml` | Fereastra editorului: header cu sigla, taburile, Minimizează, Închide; forma minimizată |
| `_NoteEditorTabButton`, `_NoteEditorTabPanel`, `_NoteEditorTab` | Un tab al editorului (butonul, panoul notei, fragmentul pentru un tab adăugat) |
| `_DeleteNoteDialog.cshtml` | Confirmarea ștergerii |
| `_StatusMessage.cshtml` + `Web/Messages` | Mesajele de salvare (succes / avertisment / eroare) și transportul lor prin TempData |
| `Web/Notes/NoteDates`, `NoteCardStyle` | Formatele datelor, clasele de înclinare ale cardurilor |
| `Web/Notes/NoteReferences` | Textele referințelor (tooltip, tip) și textul unei note cu referințele ca linkuri (card, editor fără JavaScript) |
| `Web/Navigation/NavigationSections` | Subtitlul din header și grupul deschis din meniu |

## JavaScript (numai comportament; aspectul vine din clase CSS)

| Fișier | Rol |
| --- | --- |
| `notes-board.js` | Schimbarea contextului, cardul „Notă nouă”, redenumirea pe loc, deschiderea notelor (în editorul deschis, dacă există; și din referințele previzualizării), schimbul a două carduri prin drag-and-drop, prinse de bandă |
| `note-editor.js` | Editorul: câte un CodeMirror pe tab, identitatea paragrafelor, salvarea, taburile, minimizarea; după un schimb pe tablă, taburile notelor mutate primesc versiunile noi (`note-board:versions`) |
| `note-references.js` | Referințele dintre note în editor: detectarea numărului terminat, sugestia, transformarea în referință, afișarea (link sau marcaj), deschiderea în tab, verificarea referințelor lipite |
| `status-messages.js` | Afișarea mesajelor de salvare din scripturi, din template-uri randate de server |
| `modal.js` | Dialogurile modale (Escape și click în afară revin la adresa de închidere) |
| `navigation.js`, `language.js`, `validation.js` | Meniul, selectorul de limbă, validarea client |
| `lib/codemirror/codemirror.js` | CodeMirror 6, construit local din `tools/codemirror` (licență MIT) |

## CSS

`tokens.css` (paleta), `site.css` (componente comune, mesaje, dialoguri), `navigation.css`, `notes-board.css` (tabla), `postit.css` (hârtia și banda), `note-editor.css` (editorul). Ghidul este [design-system.md](../design-system.md).
