# Localizare

Regulile obligatorii — resurse .resx pentru orice text afișat, traduceri complete ro/en/pl, interdicția textelor hardcodate — sunt în [AGENTS.md › Localizare obligatorie](../AGENTS.md#localizare-obligatorie). Acest document descrie implementarea.

## Proiectul de resurse

```text
Solution/
├── WorkNotes.Resources/
│   └── Resources/
│       ├── SharedResources.cs        # clasa marker, namespace WorkNotes.Resources.Resources
│       ├── SharedResources.resx      # română; neutru și fallback, identic cu .ro
│       ├── SharedResources.ro.resx
│       ├── SharedResources.en.resx
│       └── SharedResources.pl.resx
├── WorkNotes.Web/
│   ├── Localization/
│   │   ├── LocalizationConfiguration.cs      # culturile acceptate și providerul de cultură
│   │   ├── LocalizedMvcOptions.cs            # mesajele de model binding
│   │   └── LocalizedIdentityErrorDescriber.cs
│   ├── Pages/Language.cshtml(.cs)            # POST: schimbarea limbii
│   ├── Pages/Shared/_LanguageSelector.cshtml
│   └── wwwroot/js/language.js
└── tools/Test-Resources.ps1
```

`WorkNotes.Resources` nu are dependențe; îl referă numai Web. Catalogul are 168 de chei (2026-09-25), aceleași în toate cele patru fișiere; PR #4 adaugă `Notes_ReferenceTarget` (tooltipul unui link către altă notă: titlul și tipul ei; un link către mai multe note are câte un rând pentru fiecare), deci 169 (verificat cu `Test-Resources.ps1` pe 2026-09-28), și schimbă `Editor_Help` (Ctrl+Enter deschide referința de la cursor); apoi adaugă cheile sertarului referințelor din editor, `Editor_References` (numele sertarului), `Editor_ReferencesHelp` (textul explicativ de deasupra listei) și `Editor_ReferencesEmpty` (nota fără referințe), deci 172, apoi cheile popup-ului referinței abia scrise, `Editor_ReferenceLookup` (numele lui pentru cititoarele de ecran), `Editor_ReferenceSearching` (căutarea), `Editor_ReferenceFound` („Referința {0} deschide:”), `Editor_ReferenceMissing` („Referință inexistentă: nicio notă nu are {0} în titlu.”), `Editor_ReferenceCreate` (butonul) și `Editor_ReferenceHint` (tastele), și schimbă `Editor_ReferencesHelp`, deci 178 (verificat cu `Test-Resources.ps1` pe 2026-09-29).

## Limbile

| Cultură | Limbă | Rol |
| --- | --- | --- |
| `ro-RO` | română | implicită și fallback; fișierul neutru conține româna |
| `en-US` | engleză | tradusă complet |
| `pl-PL` | poloneză | tradusă complet |

- `LocalizationConfiguration` stabilește cultura implicită `ro-RO`, culturile acceptate și un singur provider: `CookieRequestCultureProvider`. Limba browserului (`Accept-Language`) nu este folosită, deci prima vizită și orice cerere fără cookie valid sunt în română. Răspunsurile poartă cultura în antetul `Content-Language`.
- Selectorul din header trimite formularul imediat la schimbare (`language.js`); fără JavaScript apare butonul „Aplică limba”. `POST /Language` verifică cultura în lista acceptată și permite numai o adresă de întoarcere locală, păstrând ruta și query string-ul curent. Cookie-ul standard `.AspNetCore.Culture` durează un an și este HttpOnly, SameSite=Lax, Secure pe HTTPS, esențial, cu calea `PathBase`.
- Schimbarea limbii reîncarcă pagina; datele nesalvate din formulare nu se păstrează. Mesajele de salvare aflate încă în TempData se păstrează (`TempData.Keep()`) și se afișează în limba nouă, pentru că TempData conține cheia, nu traducerea.
- `IStringLocalizer<SharedResources>` și `IStringLocalizer` folosesc același catalog; view-urile îl primesc ca `L` din `_ViewImports.cshtml`.

## Convenții pentru chei

- Cheile sunt `Prefix_Nume` în PascalCase, descriptive și stabile; nu se folosesc propoziții drept chei.
- Parametrii sunt `{0}`, `{1}`… și trebuie să fie aceiași în toate limbile (de exemplu `Validation_MaxLength` = „Câmpul „{0}” poate avea maximum {1} caractere.”).
- Excepție: frazele panoului de căutare CodeMirror (`Editor_Phrase_*`) folosesc `$`, marcajul CodeMirror pentru număr.

| Prefix | Folosit pentru | Chei |
| --- | --- | --- |
| `Navigation_` | meniu, titluri de pagină, subtitlul secțiunii | 13 |
| `Field_` | etichetele câmpurilor | 16 |
| `Button_` | textele butoanelor | 15 |
| `Validation_` | mesajele de validare | 14 |
| `Message_` | mesajele de salvare și tipul lor (`Message_Kind*`) | 17 |
| `Identity_` | erorile Identity localizate | 8 |
| `Notes_`, `NoteType_`, `NoteVisibility_` | tabla, cardurile, tipurile și vizibilitățile notelor | 14 (15 cu PR #4) + 2 + 2 |
| `Editor_` | editorul, taburile, forma minimizată, frazele căutării; cu PR #4, sertarul referințelor și popup-ul referinței abia scrise | 44 (53 cu PR #4) |
| `Contexts_`, `ContextRole_` | contextele, membrii și rolurile | 11 + 2 |
| `Dashboard_`, `Home_`, `Language_`, `Footer_` | tabla, pagina de bun venit, selectorul de limbă, footerul | 3 + 1 + 4 + 2 |

Un prefix nou trebuie adăugat și în expresia regulată din `tools/Test-Resources.ps1`, care caută cheile lipsă.

## Fallback

- Fișierul neutru (`SharedResources.resx`) este identic cu `SharedResources.ro.resx`, deci o cultură fără fișier propriu cade pe română.
- O cheie inexistentă ar fi afișată ca nume de cheie de `IStringLocalizer`; `Test-Resources.ps1` previne acest caz.

## Formatarea datelor

- Datele notelor sunt afișate în ora locală a serverului aplicației, cu formate fixe, aceleași în toate limbile (`WorkNotes.Web/Notes/NoteDates.cs`): `dd.MM.yyyy · HH:mm` pe carduri și în forma minimizată, `dd.MM.yyyy HH:mm` în textele de audit ale paragrafelor, `yyyy-MM-ddTHH:mmzzz` în atributele `datetime`.
- Titlurile lunilor de pe tablă folosesc cultura curentă (`MMMM yyyy`, de exemplu „septembrie 2026”), cu prima literă transformată în majusculă prin CSS.
- Ziua jurnalului și luna unei note se calculează din calendarul local al aplicației (`TimeProvider`).
- TODO: Necesită clarificare — dacă datele de pe carduri și din editor trebuie să urmeze formatul culturii selectate.

## Mesajele de validare

- ViewModel-urile folosesc chei ca mesaje: `[Required(ErrorMessage = "Validation_Required")]`, `[Display(Name = "Field_Email")]`; `AddDataAnnotationsLocalization` le traduce prin catalogul `SharedResources`, iar numele câmpului intră ca parametru `{0}`.
- Mesajele de model binding (valoare invalidă, număr așteptat etc.) sunt înlocuite de `LocalizedMvcOptions` cu `Validation_InvalidValue`.
- Erorile Identity sunt produse de `LocalizedIdentityErrorDescriber`: codul erorii este cheia .resx, iar descrierea este textul localizat; serviciile de cont întorc codurile, iar paginile le traduc.
- Codurile de stare Business (de exemplu `WorkContextSaveStatus.DuplicateName`) sunt traduse în pagini în chei precum `Validation_ContextNameTaken`.

## Traducerile folosite în JavaScript

JavaScript nu conține texte. Textele ajung la scripturi randate de server:

- validarea client citește atributele `data-val-*` generate de Razor;
- tabla primește textele în atribute `data-*` pe `[data-notes-dashboard]` (`data-rename-failed`, `data-reorder-failed`, `data-drag-hint`);
- mesajele de salvare sunt copiate din template-urile `_StatusMessage` randate în pagină (`status-messages.js`);
- editorul citește textele comune și frazele CodeMirror (`EditorState.phrases`) din JSON-ul `#note-editor-data`, iar datele fiecărui tab din JSON-ul panoului;
- PR #4: sertarul referințelor din editor este randat de server, cu textele lui; după o salvare, intrările lui sunt copii ale template-ului randat în sertar, completate cu numele referințelor și etichetele notelor din răspunsul JSON (`referenceList`, `references`);
- PR #4: popup-ul referinței abia scrise este o copie a template-ului din dialogul editorului, cu textele căutării, butonului și tastelor; mesajul găsit sau inexistent (cu referința, ca parametru `{0}`) și tipurile notelor vin localizate în răspunsul JSON al căutării (`message`, `notes`);
- PR #4: după o salvare din editor, textele cardului notei (numele „Fără titlu” al unei note fără titlu și etichetele Open și Delete, cu titlul) vin localizate în răspunsul salvării (`card`), cu cheile folosite de `_NoteCard`;
- răspunsurile JSON ale handlerelor conțin deja mesajul localizat (`message`).

## Texte care nu se traduc

Brandul WorkNotes, datele introduse de utilizatori (titluri, paragrafe, nume de contexte), identificatorii tehnici, mesajele din loguri și detaliile excepțiilor. Simbolurile decorative marcate `aria-hidden` („+”, „×”, „☰”) nu sunt texte.

## Adăugarea unui text

1. Alegeți cheia după convenții și adăugați-o, cu aceeași valoare românească, în `SharedResources.resx` și `SharedResources.ro.resx`, apoi cu traducerile în `.en.resx` și `.pl.resx`, cu aceiași parametri.
2. Folosiți-o prin `L["Cheie"]`, `IStringLocalizer<SharedResources>` sau ca `ErrorMessage` / `Display(Name)` în DataAnnotations; pentru JavaScript, randați textul în pagină.
3. Rulați verificarea și verificați textul în cele trei limbi.

## Verificarea

Din folderul `Solution`, în PowerShell:

```powershell
.\tools\Test-Resources.ps1
```

Scriptul verifică: chei duplicate sau valori goale, aceleași chei în cele patru fișiere, aceiași parametri `{n}`, fallback-ul românesc identic cu `.ro`, cheile neutilizate în codul Web/Business/DataAccess și cheile folosite în cod care lipsesc din catalog. Mesajul de succes începe cu `PASS:`.
