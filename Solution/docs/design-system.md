# WorkNotes — Hârtie & salvie

Design aprobat: varianta 1. Temă luminoasă, fond crem, verde închis, suprafețe de hârtie și post-it-uri pastelate. Nu este implementată o temă întunecată.

## Fișiere și ordine de încărcare

1. wwwroot/css/tokens.css — paletă, font, raze, umbre, tranziții.
2. wwwroot/css/site.css — layout, text, butoane, formulare, mesaje.
3. wwwroot/css/notes-board.css — tabla și post-it-urile.
4. wwwroot/js/notes-board.js — modul ES pentru comportamentul tablei (notă nouă, renunțare, deschidere); nu schimbă aspectul sau poziția cardurilor.

Nu duplicați culorile în pagini. Modificările de paletă se fac în tokens.css.

## Paletă

| Utilizare | Token | Valoare |
| --- | --- | --- |
| Fundal | --wn-bg | #F5F3EB |
| Suprafață | --wn-surface | #FFFFFF |
| Text | --wn-text | #243A36 |
| Text secundar | --wn-muted | #576960 |
| Acțiune principală | --wn-primary | #176C65 |
| Hover principal | --wn-primary-hover | #10564F |
| Apăsat | --wn-primary-active | #0C443E |
| Selecție | --wn-selection | #CCE6DF |
| Rând activ | --wn-active-line | #E7F1EC |
| Evidențiere | --wn-highlight | #F5DF93 |
| Post-it hârtie (note) | --wn-note-paper | #FFF0B7 |
| Post-it salvie, panouri (postit-panel) | --wn-note-sage | #DDEADB |

Paleta de bază este: accent #176C65, hover #10564F, fond #F5F3EB, note #FFF0B7 și selecție #CCE6DF. Panourile de conținut folosesc verdele salvie al post-it-urilor (#DDEADB); pe ele, selecția de text și hover-ul butoanelor discrete folosesc galbenul notelor, ca să rămână vizibile. Evidențierea (mark, #F5DF93) și rândul activ (active-line) rămân neschimbate. Piersica și albastrul sunt variante opționale. Nu atribuiți aleatoriu culori cu semnificație de status. Folosiți etichete vizibile, nu doar culoare.

## Componente

- Acțiune principală: btn btn-primary. O singură acțiune dominantă într-un grup.
- Acțiune secundară: btn btn-secondary.
- Acțiune discretă: btn btn-ghost.
- Acțiune distructivă: btn btn-danger; logica de confirmare aparține funcționalității.
- Dezactivare: atributul nativ disabled pe button. aria-disabled doar descrie starea unui link; codul funcțional trebuie să împiedice activarea lui.
- Grup de acțiuni: actions.
- Text lung: prose. Paragrafe de 16px, line-height 1.7, maximum 72ch.
- Ajutor/metadate: help-text / text-muted.
- Evidențiere: mark sau text-highlight; rând activ: active-line.
- Formulare: field, check, field-error; stările invalide acceptă aria-invalid și input-validation-error.
- Mesaje: status-message și validation-summary-errors; asociați role=status / role=alert în markup.
- Pagini de listă: page-panel postit-panel, cu page-heading (titlu, descriere și acțiunea principală în dreapta). Formularele de adăugare/editare/confirmare folosesc account-card postit-panel.
- Overlay pentru formulare deschise peste o pagină: dialog.modal cu open și data-modal, conținutul în modal-panel postit-panel. Starea este în URL (de exemplu ?add=true, ?edit={id}, ?delete={id}), deci funcționează și fără JavaScript ca overlay CSS; wwwroot/js/modal.js îl transformă în dialog modal (focus captiv, Escape și click pe fundal revin la data-close-url). Renunță este un link către aceeași adresă; în confirmările distructive primește focusul inițial (autofocus).
- Tabele: data-table; numele rândului este th scope=row, iar acțiunile sunt aliniate la dreapta. Sub 700px rândurile devin blocuri. Etichetele doar pentru cititoare de ecran folosesc visually-hidden.

Focus vizibil pentru tastatură, ținte de minimum 44px pentru controalele principale, reducerea animației când utilizatorul solicită acest lucru și contururi în forced-colors.

## Tabla de note

Există câte o tablă pentru fiecare context. În locul titlului, dashboardul (Pages/Index) are lista de contexte (board-switch, cu bordură întreruptă ca tabla); primul context este selectat implicit, iar alegerea altuia încarcă /?context={id} (fără JavaScript, cu butonul Afișează). Titlul „Pe tabla mea” rămâne numai pentru cititoarele de ecran. Contextul nu mai apare pe post-it.

Notele primite de la INoteService sunt grupate pe luni (notes-month, cu titlul lunii), după data locală a creării, lunile cele mai noi primele; în fiecare lună apar întâi jurnalele, apoi articolele, fiecare de la cel mai nou. Cardurile sunt randate de server din partialele Pages/Shared/_NoteCard.cshtml (notă salvată) și Pages/Shared/_NewNoteCard.cshtml (notă nouă, nesalvată):

```html
<section class="notes-month">
    <h2 class="notes-month__title">septembrie 2026</h2>
    <ul class="notes-board" data-new-note-target="true">
        <li class="note-cell">
            <article class="note-card note-card--journal note-card--tilt-3" data-note-id="12">
                <p class="note-card__meta"><span>Jurnal</span> <time datetime="…">23.09 · 09:40</time></p>
                <h3 class="note-card__title">…</h3>
                <div class="note-card__footer"><span>Privat</span><button class="note-card__open" data-note-open>…</button></div>
            </article>
        </li>
    </ul>
</section>
```

- Data are formatul zz.LL · HH:mm (de exemplu 23.09 · 17:44).
- Culoarea urmează tipul: note-card--journal (galben #FFF0B7), note-card--article (salvie #DDEADB). Cardul nou arată culoarea tipului ales în switch, numai prin CSS (:has).
- Decalajul și rotația sunt deterministe din ID: serverul alege una dintre clasele note-card--tilt-0 … note-card--tilt-11 (NoteCardStyle.TiltClass), în limitele ±12px și ±2°. Marginea de 22px a celulei păstrează cardul în celula proprie. Nu există stiluri inline și niciun script nu poziționează cardurile. Pe ecrane de maximum 450px notele sunt drepte, pe o coloană.
- Ordinea DOM este ordinea din serviciu și cea de navigare cu tastatura. Grila (auto-fill) mută celelalte carduri spre dreapta și în jos când apare un card nou la început.
- Luna curentă este randată întotdeauna (ca țintă pentru cardul nou) și ascunsă prin CSS cât timp nu are carduri.
- Cardul salvat este article; singurul control din el este butonul Open (note-card__open). Nu puneți controale interactive unul în altul.

### Notă nouă

- Butonul „Notă nouă” este un link către /?new=true. Fără JavaScript, serverul randează cardul nou primul pe tablă; Renunță este un link înapoi la tablă.
- Cu JavaScript, wwwroot/js/notes-board.js copiază cardul din &lt;template id="new-note-template"&gt; (randat de server, cu textele din .resx și tokenul antiforgery) la începutul lunii curente a tablei alese și mută focusul pe titlu. Renunță sau Escape elimină cardul și readuc focusul pe buton.
- Cardul nou conține: switch-ul de tip cu iconuri (jurnal / articol), fără bordură și fundal; tipul ales are iconul colorat și subliniat, iar numele tipului apare ca popover la hover și la focus (este și eticheta accesibilă a radio-ului). Urmează titlul editabil pe loc (opțional), Salvează și Renunță. Nota se salvează în contextul tablei selectate.
- Salvează trimite formularul (POST ?handler=CreateNote); după salvare tabla se reîncarcă, iar nota apare prima, fără butoanele de editare inițială, cu iconul Open. La erori (de exemplu un titlu prea lung) cardul rămâne primul, cu valorile introduse.
- Mesajul „Tabla este goală” dispare prin CSS cât timp lista are cel puțin un card.

### Deschiderea unei note

Iconul Open al cardului este un link către /?note={id}, deci funcționează și fără JavaScript. Dublu-click pe card apelează openNoteEditor(card) din notes-board.js, care urmează același link; adresa este generată de server.

## Editorul notei

- Editorul se deschide peste tablă, în dialogul cu overlay al aplicației (dialog.modal, ca la adăugare/editare), cu panoul modal-panel--editor: 90% din lățimea și înălțimea ferestrei. Tabla din spate rămâne pe loc; containerul editorului (note-sheet__editor) are scroll propriu. Fără JavaScript dialogul este un overlay CSS cu textul doar pentru citire.
- Închide, Escape și click în afara panoului revin la tabla notei (/?context={id}); dacă există modificări nesalvate, browserul cere confirmarea. Escape în panoul de căutare închide doar panoul.
- Panoul are culoarea tipului (note-sheet--journal / note-sheet--article) și conține: bara de instrumente (tipul, contextul, vizibilitatea, starea salvării, Salvează, Închide), titlul editabil pe loc, editorul și bara de informații.
- Bara de informații (note-editor-info), sub editor: pentru paragraful de sub mouse, iar fără mouse pentru cel cu cursorul, arată data creării și a ultimei modificări (auditul NoteBlocks), „modificări nesalvate” sau „paragraf nou”; în dreapta, scurtăturile. Paragraful descris primește clasa cm-hoveredParagraph (evidențiere discretă). Bara nu acoperă textul și funcționează și de la tastatură.
- Stilurile WorkNotes pentru editor sunt în note-editor.css, pe clasele CodeMirror (.cm-editor, .cm-content, .cm-activeLine, panoul de căutare). Rândul activ folosește stilul active-line al paletei, selecția --wn-selection, rezultatele căutării --wn-highlight. CodeMirror își injectează doar stilurile de bază.
- Starea salvării (Salvat / Modificări nesalvate / Se salvează… / eroare) este un role=status; o eroare folosește clasa note-editor-toolbar__status--error.
- Pentru cine poate doar citi, editorul nu este editabil, butonul Salvează lipsește, iar bara de informații o spune.
- Textele (placeholder, stări, informațiile de audit, frazele panoului de căutare) sunt randate de server din .resx în datele paginii; note-editor.js nu conține traduceri.

## Verificare vizuală

Verificați ecranele de cont și headerul în ro/en/pl, la 320px și desktop, cu tastatură și erori de validare. Pentru tabla viitoare verificați titluri lungi, metadate, ordine, selectare și încadrarea în celule înainte de conectarea datelor.

## Navigare
Headerul păstrează brandul, numele utilizatorului și selectorul de limbă. Titlul WorkNotes (buton cu indicator de meniu) deschide un sertar modal în stânga, stilizat în navigation.css. Dialogul nativ păstrează focusul în meniu; Escape, fundalul și butonul de închidere îl închid și restabilesc focusul.
Dashboard duce la pagina principală: pentru utilizatorul autentificat aceasta este tabla: secțiunea dashboard ocupă toată lățimea și înălțimea zonei principale, cu o margine mică; titlul, descrierea și butonul Notă nouă stau în partea de sus a tablei, iar notele (ul.notes-board) se vor afișa sub ele, în locul mesajului notes-board-empty; vizitatorii văd panoul de bun venit cu autentificare și înregistrare. Butonul Notă nouă este dezactivat nativ până la implementarea modulului Notes. Lângă brand, headerul afișează subtitlul secțiunii din meniul curent (Spațiul meu pentru Dashboard, Contul meu pentru paginile de cont), calculat de NavigationSections din ruta paginii; aceeași regulă deschide grupul Contul meu în meniu. Grupul Contul meu conține datele contului, schimbarea parolei și deconectarea POST; pentru vizitatori conține autentificarea și înregistrarea. Pagina curentă are aria-current. Fără JavaScript navigarea rămâne vizibilă ca secțiune obișnuită.

## Panouri pe tabla crem
Fundalul principal folosește tokenul --wn-bg, crem cald. Toate panourile de conținut (bun venit pentru vizitatori, autentificare, înregistrare, cont și schimbare parolă) folosesc clasa postit-panel: hârtie verde salvie (#DDEADB), bandă superioară discretă și umbră de post-it. Panourile noi din body trebuie să reutilizeze această clasă. Containerele interne pentru câmpuri și butoane rămân parte din aceeași foaie, fără umbre suprapuse. Formularele nu se rotesc, pentru lizibilitate.

### Hârtie lipită cu bandă
postit.css aplică panourilor culoarea plină #DDEADB (--wn-note-sage), iar cardurilor tablei galbenul notelor #FFF0B7 (--wn-note-paper), fără gradient. Tot aspectul hârtiei (fundal, margine, rază, umbră, bandă) este definit numai în postit.css; notes-board.css păstrează doar dispunerea, tipografia și starea hover a cardurilor. Imaginea decorativă images/postit-tape.svg este suprapusă central peste marginea de sus. Umbra inferioară este un singur strat box-shadow (--wn-shadow-paper) aplicat direct elementului, fără pseudo-element la bază și fără un al doilea strat decalat (ambele produc o margine dublată). Aspectul hârtiei este integral CSS static prin clasele postit-panel / note-card, fără JavaScript sau stiluri injectate dinamic. Pseudo-elementele nu interceptează clickuri și nu adaugă text accesibil. Păstrați spațiu deasupra panourilor și overflow vizibil; nu aplicați efectul fiecărui container intern. Stilurile pentru contrast forțat elimină decorațiile.
