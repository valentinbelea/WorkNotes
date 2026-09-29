# Interfața și experiența utilizatorului

Ghidul vizual „Hârtie & salvie” și comportamentul interfeței, așa cum sunt implementate pe `main`. Regulile obligatorii de design sunt în [AGENTS.md › Design obligatoriu](../AGENTS.md#design-obligatoriu); cerințele și statusul lor în [REQUIREMENTS.md](REQUIREMENTS.md).

Marcaje: **[x]** implementat · **[~]** în dezvoltare (neintegrat în `main`) · **[ ]** planificat · **[!]** necesită remediere sau clarificare.

## Principii de design

- [x] Design aprobat: varianta 1. Temă luminoasă, fond crem, verde închis, suprafețe de hârtie și post-it-uri pastelate. Nu există temă întunecată.
- [x] Aspectul este integral CSS, prin clase; JavaScript nu scrie stiluri și nu poziționează elemente.
- [x] Toate textele vin din .resx; designul nu introduce date fictive.
- [x] Operațiile de bază funcționează și fără JavaScript: starea overlay-urilor este în URL (`?add=true`, `?edit={id}`, `?delete={id}`, `?members={id}`, `?new=true`, `?note={id}`), iar JavaScript adaugă confortul (editare pe loc, editor, taburi, drag-and-drop).

## Fișiere și ordinea de încărcare

1. `wwwroot/css/tokens.css` — paleta, fontul, razele, umbrele, tranzițiile.
2. `wwwroot/css/site.css` — layout, text, butoane, formulare, mesaje, dialoguri, tabele.
3. `wwwroot/css/navigation.css` — headerul și meniul sertar.
4. `wwwroot/css/notes-board.css` — tabla și post-it-urile (dispunere, tipografie, stări).
5. `wwwroot/css/postit.css` — hârtia: fundal, margine, rază, umbră, bandă.
6. `wwwroot/css/note-editor.css` — editorul.

Scripturile sunt descrise în [ARCHITECTURE.md](ARCHITECTURE.md#javascript). Nu duplicați culorile în pagini; modificările de paletă se fac în `tokens.css`.

## Paletă

| Utilizare | Token | Valoare |
| --- | --- | --- |
| Fundal | `--wn-bg` | #F5F3EB |
| Suprafață | `--wn-surface` | #FFFFFF |
| Text | `--wn-text` | #243A36 |
| Text secundar | `--wn-muted` | #576960 |
| Acțiune principală | `--wn-primary` | #176C65 |
| Hover principal | `--wn-primary-hover` | #10564F |
| Apăsat | `--wn-primary-active` | #0C443E |
| Selecție | `--wn-selection` | #CCE6DF |
| Rând activ | `--wn-active-line` | #E7F1EC |
| Evidențiere | `--wn-highlight` | #F5DF93 |
| Post-it hârtie (jurnal) | `--wn-note-paper` | #FFF0B7 |
| Post-it salvie (articol, panouri) | `--wn-note-sage` | #DDEADB |

- Paleta de bază: accent #176C65, hover #10564F, fond #F5F3EB, note #FFF0B7, selecție #CCE6DF. Panourile de conținut folosesc verdele salvie #DDEADB; pe ele, selecția de text și hover-ul butoanelor discrete folosesc galbenul notelor, ca să rămână vizibile. Cu PR #4, foaia editorului urmează aceeași regulă: pe foaia salvie a unui articol selecția este galbenă, iar pe foaia galbenă a unui jurnal rămâne verdele de selecție (vezi [Highlight-uri](#highlight-uri)). Evidențierea (`mark`, #F5DF93) și rândul activ rămân neschimbate.
- Alte tokenuri: bordura `--wn-border` #D8DFD9, bordura câmpurilor `--wn-input-border` #87988E, pericol `--wn-danger` #A12D32 pe `--wn-danger-bg` #FFF0EE, succes `--wn-success-bg` #E7F1EC, avertisment `--wn-warning` #7C5700 pe `--wn-warning-bg` #FFF0B7, stări dezactivate, raze (`--wn-radius` 8px, `--wn-radius-panel` 14px), umbre și `--wn-motion` (140ms). Fontul: Segoe UI, apoi fontul sistemului.
- Piersica (#F6E2D3) și albastrul (#DEEBEE) sunt variante opționale (`note-card--peach`, `note-card--blue`), nefolosite de tipurile de note.
- Nu atribuiți aleatoriu culori cu semnificație de status și nu transmiteți informația numai prin culoare: folosiți etichete vizibile sau texte pentru cititoarele de ecran.

## Componente

- Acțiuni: principală `btn btn-primary` (o singură acțiune dominantă într-un grup), secundară `btn btn-secondary`, discretă `btn btn-ghost`, distructivă `btn btn-danger` (confirmarea aparține funcționalității). Grupul de acțiuni: `actions`.
- Dezactivare: atributul nativ `disabled` pe `button`; `aria-disabled` doar descrie starea unui link, iar codul funcțional trebuie să împiedice activarea lui.
- Text lung: `prose` (16px, line-height 1.7, maximum 72ch). Ajutor și metadate: `help-text` / `text-muted`. Evidențiere: `mark` sau `text-highlight`; rând activ: `active-line`.
- Formulare: `field`, `check`, `field-error`; stările invalide folosesc `aria-invalid` și `input-validation-error`. Erorile de validare rămân în formulare: `validation-summary-errors` (`role=alert`) și mesajele de lângă câmpuri.
- Pagini de listă: `page-panel postit-panel`, cu `page-heading` (titlu, descriere și acțiunea principală în dreapta). Formularele de cont folosesc `account-card postit-panel`.
- Overlay pentru formulare deschise peste o pagină: `dialog.modal` cu `open` și `data-modal`, conținutul în `modal-panel postit-panel`. Fără JavaScript este un overlay CSS; `modal.js` îl transformă în dialog modal (focus captiv, Escape și click pe fundal revin la `data-close-url`; cu PR #4, fereastra editorului fără modificări nesalvate se închide singură, pe loc, prin evenimentul anulabil `modal:close`). „Renunță” este un link către aceeași adresă; în confirmările distructive primește focusul inițial (`autofocus`).
- Tabele: `data-table`; numele rândului este `th scope=row`, acțiunile sunt aliniate la dreapta; sub 700px rândurile devin blocuri. Etichetele numai pentru cititoarele de ecran folosesc `visually-hidden`.
- Mesajele de salvare (succes, avertisment, eroare) — partialul `Pages/Shared/_StatusMessage.cshtml`, cu clasele `status-message--success` / `--warning` / `--error`, un icon și numele tipului pentru cititoarele de ecran; eroarea are `role=alert`:
  - stau în `status-region`, fixată sus, centrată (pe mobil pe toată lățimea, cu margini de 16px), și rămân până le închide utilizatorul cu butonul ×; cel mai nou mesaj apare primul, cu o scurtă animație de intrare (fără animație la reduced-motion); zona lasă click-urile să treacă pe lângă mesaje;
  - butonul de închidere funcționează și fără JavaScript: mesajul este un dialog nemodal deschis (`dialog open`), iar butonul este într-un `form method="dialog"`;
  - un mesaj care aparține unui dialog modal (membrii contextului, editorul) se randează în dialog, ca să rămână deasupra overlay-ului;
  - scripturile (`status-messages.js`) copiază template-urile randate de server; un mesaj deschis identic (același tip și text) este înlocuit, ca salvările repetate să nu se adune;
  - paginile setează mesajul prin `TempData.SetStatusMessage(cheie, tip)` (`WorkNotes.Web/Messages`); TempData păstrează cheia și tipul, nu traducerea. De exemplu „Contextul are note și nu poate fi șters.” este un avertisment.

## Layout, header și footer

- [x] Headerul păstrează brandul, subtitlul secțiunii curente, numele utilizatorului autentificat și selectorul de limbă. Headerul site-ului nu are siglă.
- [x] Subtitlul de lângă brand este dedus din secțiunea meniului curent prin `Navigation/NavigationSections.cs`: „Spațiul meu” pentru Dashboard, „Contexte” pentru contexte, „Contul meu” pentru paginile de cont; aceeași regulă deschide grupul „Contul meu” din meniu.
- [x] Footerul afișează „WorkNotes” și, în dreapta, versiunea din baza de date (sau „Versiune neconfigurată”).

## Navigare

- [x] Titlul WorkNotes din header este un buton cu indicator de meniu care deschide un sertar modal în stânga (`navigation.css`). Dialogul nativ păstrează focusul în meniu; Escape, fundalul și butonul de închidere îl închid și readuc focusul pe buton. Fără JavaScript, navigarea rămâne vizibilă ca secțiune obișnuită.
- [x] Meniul: Dashboard (pagina principală), Contexte și, din versiunea 0.03, Repository-uri GitHub (numai pentru utilizatorii autentificați) și grupul „Contul meu”: datele contului, schimbarea parolei, Conectare GitHub (versiunea 0.03) și deconectarea (POST) pentru utilizatorul autentificat, respectiv autentificarea și înregistrarea pentru vizitatori. Pagina curentă are `aria-current`.
- [x] Pagina principală este tabla pentru utilizatorul autentificat; vizitatorii văd panoul de bun venit, cu Autentificare și Înregistrare.
- [!] Textul panoului de bun venit este „Aplicația este pregătită pentru dezvoltare.” (`Home_Introduction`). TODO: Necesită clarificare — textul definitiv al paginii de bun venit.

## Sigla

- [x] Sigla WN este `wwwroot/images/logo-wn.svg`: pătrat rotunjit verde #176C65, literele WN albe și banda adezivă în verdele de selecție #CCE6DF.
- [x] Apare numai în fereastra editorului: în stânga headerului editorului și în forma minimizată (clasa `note-editor-logo`, 32px, decorativă, cu `alt` gol).
- [x] Este favicon-ul (SVG), cu `wwwroot/favicon.ico` (16, 32 și 48px, generat din același SVG) pentru browserele fără favicon SVG. Nu modificați culorile sau proporțiile; și sub 16px se folosește tot acest fișier.

## Panouri și hârtia post-it

- [x] Fundalul principal este crem (`--wn-bg`). Toate panourile de conținut (bun venit, autentificare, înregistrare, cont, schimbarea parolei, contexte, dialoguri) folosesc `postit-panel`: hârtie salvie #DDEADB, bandă discretă sus și umbră de post-it. Panourile noi trebuie să reutilizeze această clasă. Containerele interne rămân parte din aceeași foaie, fără umbre suprapuse. Formularele nu se rotesc, pentru lizibilitate.
- [x] `postit.css` aplică panourilor culoarea plină #DDEADB și cardurilor jurnal galbenul #FFF0B7, fără gradient. Tot aspectul hârtiei (fundal, margine, rază, umbră, bandă) este definit numai în `postit.css`; `notes-board.css` păstrează doar dispunerea, tipografia și starea hover a cardurilor.
- [x] Umbra este un singur strat `box-shadow` (`--wn-shadow-paper`) aplicat direct elementului, fără pseudo-element la bază și fără un al doilea strat decalat (ambele produc o margine dublată).
- [x] Banda adezivă este imaginea decorativă `images/postit-tape.svg` (crem) sau `images/postit-tape-sage.svg` (verde, aceeași transparență), centrată peste marginea de sus, ușor rotită, prin variabila `--postit-tape`. Ca pseudo-element nu interceptează clickuri și nu adaugă text accesibil. Păstrați spațiu deasupra panourilor și `overflow` vizibil; nu aplicați efectul fiecărui container intern. În forced-colors decorațiile sunt eliminate.

## Pagina Contexte

- [x] `/Contexts` (link „Contexte” în meniu, numai pentru utilizatorii autentificați) arată contextele în care utilizatorul este membru, ordonate după nume, într-un `data-table` pe `page-panel postit-panel`, cu acțiunea principală „Context nou”; fără contexte apare „Nu există încă niciun context.”.
- [x] Butoanele Membri, Editează și Șterge apar numai pe rândurile contextelor proprii (`Owner`); un membru care deschide direct adresa sau trimite POST-ul primește 403.
- [x] Adăugarea (`?add=true`) și editarea (`?edit={id}`) se deschid peste listă, într-un overlay; după salvare se revine la listă, iar la erori de validare overlay-ul rămâne deschis, cu valorile introduse.
- [x] Ștergerea (`?delete={id}`) cere confirmare în overlay, cu focusul pe „Renunță”; se execută numai prin POST cu antiforgery. Un context cu note nu se șterge: apare avertismentul „Contextul are note și nu poate fi șters.”.
- [x] Membrii (`?members={id}`, numai pentru proprietar) se afișează într-un overlay lat: tabelul cu nume, e-mail și rol (proprietarul primul), butonul „Elimină” pe membrii cu rolul `Member` și formularul de adăugare după e-mail. Mesajele (adăugat, eliminat, membru deja eliminat) apar în dialog, deasupra overlay-ului; erorile (e-mail invalid, cont inexistent, membru existent) rămân lângă câmp.

## Paginile de cont

- [x] Înregistrarea, autentificarea, „Contul meu” și schimbarea parolei folosesc `account-card postit-panel`, cu sumarul erorilor (`role=alert`) și mesajele lângă câmpuri; validarea client folosește aceleași texte ca serverul.
- [x] Formularele de parolă afișează politica („Parola trebuie să conțină minimum 12 caractere, o literă mică, o literă mare, o cifră și un caracter special.”) ca text de ajutor; câmpurile au `autocomplete` potrivit (`username`, `current-password`, `new-password`, `given-name`, `family-name`, `email`).
- [x] „Contul meu” afișează e-mailul ca text, fără câmp de editare.
- [~] Conectare GitHub (versiunea 0.03) folosește `account-card postit-panel`: descrierea, apoi contul GitHub (login-ul, datele cu `time`) și acțiunile într-un rând `actions` (Verifică conexiunea și Conectează din nou `btn-secondary`, Deconectează `btn-danger`), sau Conectează GitHub (`btn-primary`) când nu este conectat, sau o notă `text-muted` când GitHub nu este configurat; când contul este conectat, „Alege repository-urile” (`btn-primary`) duce la pagina repository-urilor. Fiecare acțiune este un formular POST, deci funcționează fără JavaScript; rezultatele apar ca mesaje de salvare (succes, avertisment, eroare).
- [~] Repository-uri GitHub (`/Repositories`, versiunea 0.03) folosește `page-panel postit-panel`, cu `page-heading` (titlul și descrierea), numărul repository-urilor și o `data-table`: bifa (`check`, cu numele repository-ului pentru cititoarele de ecran), numele ca link către GitHub (într-o filă nouă), vizibilitatea, branch-ul implicit și descrierea; un repository pe care GitHub nu-l mai arată are o notă sub nume. „Salvează selecția” (`btn-primary`) este sub tabel. Fără conexiune sau fără răspuns de la GitHub, un mesaj `text-muted` (cu link către Conectare GitHub) și numai repository-urile importate, fără bife. Funcționează fără JavaScript.
- [x] Rezultatele (cont creat, date actualizate, parolă schimbată) apar ca mesaje de salvare după redirecționare.

## Tabla de note

- [x] Există câte o tablă pentru fiecare context. În locul titlului, dashboardul are lista de contexte (`board-switch`, cu bordură întreruptă ca tabla); primul context este selectat implicit, iar alegerea altuia încarcă `/?context={id}` (fără JavaScript, cu butonul „Afișează”). Titlul „Pe tabla mea” există numai pentru cititoarele de ecran. La focus, bordura întreruptă gri a listei devine ea însăși verde plin (`--wn-primary`), în același loc, fără un al doilea contur.
- [x] Contextul se alege pe tablă, înainte de a deschide o notă; editorul îl afișează, nu îl schimbă. Contextul nu apare pe post-it.
- [x] Dashboardul ocupă toată zona principală, cu o margine mică și bordură întreruptă; sus sunt lista de contexte, descrierea („Notele contextului, grupate pe luni după ultima modificare.”) și butonul „Notă nouă”, iar dedesubt notele.
- [x] Grupurile sunt lunile: notele sunt grupate după luna locală a ultimei modificări — `ISNULL(ModifiedAtUtc, CreatedAtUtc)` —, cele mai noi luni primele (`notes-month`, cu titlul lunii în limba curentă). În fiecare lună ordinea este `Order` crescător, apoi ultima modificare, crearea și `Id`, cele mai recente primele. O notă modificată trece în luna modificării.
- [x] Luna curentă este randată întotdeauna, ca țintă pentru cardul nou, și ascunsă prin CSS cât timp nu are carduri.
- [x] Ordinea DOM este ordinea serviciului și ordinea de navigare cu tastatura. Grila (`auto-fill`, celule pătrate de minimum 17rem) mută celelalte carduri spre dreapta și în jos când apare un card nou la început.
- [x] Cardurile sunt randate de server din `Pages/Shared/_NoteCard.cshtml` (notă salvată) și `_NewNoteCard.cshtml` (notă nouă):

```html
<section class="notes-month">
    <h2 class="notes-month__title">septembrie 2026</h2>
    <ul class="notes-board" data-new-note-target="true">
        <li class="note-cell">
            <article class="note-card note-card--journal note-card--tilt-3" data-note-id="12">
                <span class="note-card__tape" aria-hidden="true" data-note-drag-handle></span>
                <p class="note-card__meta note-card__meta--dates">
                    <span class="visually-hidden">Jurnal</span>
                    <span class="note-card__date"><span class="visually-hidden">Creată</span> <time datetime="…">23.09.2026 · 09:40</time></span>
                    <span class="note-card__date" data-note-modified><span class="visually-hidden">Modificată</span> <time datetime="…">24.09.2026 · 17:05</time></span>
                </p>
                <form class="note-card__rename" data-note-rename><input class="note-card__title-field" name="title" data-note-title></form>
                <p class="note-card__excerpt">începutul primelor paragrafe…</p>
                <div class="note-card__footer">
                    <span>Privat</span>
                    <span class="note-card__actions"><a class="note-card__icon" data-note-open>…</a><a class="note-card__icon note-card__icon--danger">…</a></span>
                </div>
            </article>
        </li>
    </ul>
</section>
```

## Post-it-ul

### Aspect și culoare

- [x] Culoarea urmează tipul: `note-card--journal` galben #FFF0B7, `note-card--article` salvie #DDEADB. Tipul nu are etichetă vizibilă: culoarea deosebește jurnalul de articol, iar tipul este scris pentru cititoarele de ecran (`visually-hidden`).
- [x] Decalajul și rotația sunt deterministe din ID: serverul alege una dintre clasele `note-card--tilt-0` … `note-card--tilt-11` (`NoteCardStyle.TiltClass`), în limitele ±12px și ±2°; marginea de 22px a celulei păstrează cardul în celula proprie. Nu există stiluri inline și niciun script nu poziționează cardurile. La hover cardul se îndreaptă și se ridică ușor.
- [x] Cardul salvat este un `article`; controalele lui (câmpul de titlu, Open, Delete) sunt separate, fără controale interactive unul în altul.

### Headerul și banda adezivă

- [x] Banda adezivă („fâșia de lipici”) stă peste marginea de sus: crem pe hârtia galbenă, verde pe hârtia salvie (carduri de articol, panouri, foaia editorului pentru articole). Pe cardurile proprietarului banda este elementul `note-card__tape` (în locul pseudo-elementului, în aceeași poziție și cu aceeași imagine) și este mânerul pentru drag-and-drop.
- [x] Headerul cardului arată data creării în stânga și data ultimei modificări în dreapta, pe același rând, în formatul `zz.LL.aaaa · HH:mm` (de exemplu 23.09.2026 · 17:44), cu anul și fără etichete vizibile; „Creată” și „Modificată” sunt citite doar de cititoarele de ecran.
- [x] Ca să încapă ambele pe rând, datele se scalează cu lățimea cardului (container query, font între 9 și 12px, calculat pentru Segoe UI); cu un font mult mai lat, data modificării trece pe rândul următor, tot în dreapta.
- [x] Data modificării este ascunsă (`hidden`) cât timp nota nu a fost modificată sau se citește la fel ca data creării (`NoteDates.ShowsModified`).

### Previzualizarea

- [x] `note-card__excerpt` arată începutul primelor 3 paragrafe, câte unul pe rând (`NoteRules.BuildPreview`, maximum 280 de caractere, tăiat la un cuvânt). Ocupă spațiul dintre titlu și subsol; ultimul rând vizibil se estompează în loc să fie tăiat.

### Footerul

- [x] Subsolul arată vizibilitatea („Privat” sau „În context”) și, în dreapta, iconul Open (link către `/?note={id}`) și, pentru proprietar, iconul Delete (`note-card__icon--danger`, link către `/?delete={id}`, care deschide confirmarea peste tablă). Iconurile au 44px și etichete accesibile cu titlul notei.
- [x] Ștergerea se face numai prin POST cu antiforgery, după confirmare, și elimină nota cu tot conținutul ei.

### Editarea titlului pe loc

- [x] Proprietarul redenumește titlul direct pe card (`note-card__title-field`): Enter salvează, părăsirea câmpului cu titlul schimbat salvează și ea, Escape readuce titlul salvat. Un titlu gol face nota „Fără titlu”. Pentru ceilalți membri titlul este text (`h3`).
- [x] Cu JavaScript salvarea se face în fundal, iar rezultatul apare ca mesaj de salvare, până este închis; cardul arată imediat noua dată a modificării (formatată de server) și își păstrează locul până la următoarea încărcare a tablei. Fără JavaScript, Enter trimite formularul și tabla se reîncarcă cu același mesaj.
- [x] Dublu-click pe card deschide editorul, cu excepția câmpului de titlu și a iconurilor.

## Nota nouă și switch-ul jurnal/articol

- [x] Butonul „Notă nouă” este un link către `/?new=true`. Fără JavaScript, serverul randează cardul nou primul pe tablă; „Renunță” este un link înapoi la tablă.
- [x] Cu JavaScript, `notes-board.js` copiază cardul din `<template id="new-note-template">` (randat de server, cu textele din .resx și tokenul antiforgery) la începutul lunii curente și mută focusul pe titlu. „Renunță” sau Escape elimină cardul și readuc focusul pe buton; un al doilea click pe „Notă nouă” doar mută focusul pe cardul deschis.
- [x] Cardul nou conține switch-ul de tip cu iconuri (jurnal / articol), fără bordură și fundal: tipul ales are iconul colorat și subliniat, iar numele tipului apare ca popover la hover și la focus (este și eticheta accesibilă a radio-ului). Cardul arată culoarea tipului ales numai prin CSS (`:has`). Urmează titlul editabil (opțional, „Titlu (opțional)”), Salvează și Renunță.
- [x] Salvează trimite formularul (`POST ?handler=CreateNote`) în contextul tablei selectate; după salvare tabla se reîncarcă, iar nota apare prima în luna curentă, cu iconul Open. La erori (de exemplu un titlu prea lung) cardul rămâne primul, cu valorile introduse.
- [x] Fără niciun context, butonul „Notă nouă” este dezactivat nativ, cu linkul „Creează mai întâi un context pentru a adăuga note.” către pagina Contexte.
- [x] Mesajul „Tabla este goală.” (`notes-board-empty`) dispare prin CSS cât timp lista are cel puțin un card.

## Ordonarea prin drag-and-drop

- [x] Proprietarul schimbă locul a două note ale sale din aceeași lună: trage un card peste altul, iar cele două își schimbă locurile. Cardurile altor membri, cardul nou nesalvat și cardurile altor luni nu sunt ținte.
- [x] Zona de drag este numai banda adezivă: `notes-board.js` face elementul `note-card__tape` draggable; are `cursor: pointer`, un grip discret de 2 × 4 puncte (accent #176C65 la hover) și tooltipul „Trage de bandă ca să muți nota” (`Notes_DragHandle`). Conținutul, titlul, datele, linkurile și butoanele nu pornesc drag-ul. Fără JavaScript banda rămâne doar decor.
- [x] În timpul drag-ului, cardul preluat este estompat, cu contur punctat accent (`note-cell--dragging`); celulele eligibile din aceeași lună au o nuanță verde foarte ușoară și un contur fin (`note-cell--drop-eligible`), iar celula de sub cursor o nuanță mai puternică și conturul accent de 2px (`note-cell--drop-target`). Culorile sunt amestecuri ale `--wn-primary` cu transparent (`color-mix`), fără valori noi în paletă. La drop sau la anulare toate clasele sunt eliminate.
- [x] La drop, celulele își schimbă imediat locurile în DOM; `POST /?handler=SwapNotes` salvează schimbul, iar lista urmează ordinea lunii returnată de server. Dacă salvarea eșuează, luna revine la ordinea dinainte și apare mesajul de eroare localizat. Un drop în altă lună sau în afara cardurilor este ignorat, iar cardul rămâne pe loc.
- [x] Imaginea trasă este cardul întreg (`setDragImage`); stările sunt numai în `notes-board.css`. În forced-colors țintele au contururi `CanvasText` / `Highlight`, iar banda-mâner rămâne vizibilă ca o bară.
- [x] Schimbul modifică versiunile notelor: un tab de editor deschis în aceeași pagină pe una dintre ele primește noua versiune și salvează în continuare.
- [~] PR #4: un drag nou nu mai este refuzat cât timp un schimb anterior se salvează; schimburile se aplică la `dragend` și se salvează pe rând.
- [ ] Alternativă de la tastatură pentru reordonare (vezi [ROADMAP.md](ROADMAP.md#îmbunătățiri)).

## Deschiderea unei note

- [x] Iconul Open este un link către `/?note={id}`, deci funcționează și fără JavaScript. Click pe Open și dublu-click pe card apelează `openNoteEditor(card)` din `notes-board.js`: dacă editorul este deja în pagină (de exemplu minimizat), nota se deschide într-un tab al lui (evenimentul `note-editor:open`); altfel se urmează linkul.
- [~] PR #4: fără editor în pagină, nota se deschide peste tabla din pagină, fără reîncărcare: fereastra vine de la `?handler=NoteEditor`, `modal.js` o face modală (evenimentul `modal:open`), iar adresa devine `/?note={id}`, ca o intrare nouă în istoric (Back revine la tablă). Dacă fereastra nu poate fi adusă, se urmează linkul.

## Editorul

### Fereastra

- [x] Editorul (CodeMirror 6, [ADR-002](decisions/ADR-002-note-editor.md)) se deschide peste tablă, în dialogul aplicației (`dialog.modal`), cu panoul `modal-panel--editor`: 90% din lățimea și înălțimea ferestrei. Tabla din spate rămâne pe loc; containerul editorului (`note-sheet__editor`) are scroll propriu. Fără JavaScript dialogul este un overlay CSS cu textul doar pentru citire.
- [x] Pe `main`, închiderea editorului reîncarcă tabla, deci ordinea și datele cardurilor se actualizează după salvările din editor.
- [~] PR #4: tabla din spatele editorului urmează fiecare salvare, fără să fie citită din nou: cardul notei arată titlul (câmpul de redenumire, titlul citit de cititoarele de ecran, etichetele Open și Delete) și previzualizarea numai când s-au schimbat și, la fiecare salvare, data ultimei modificări, din răspunsul salvării (`card`). O notă salvată pentru prima dată într-o lună nouă trece, cu cardul ei, în luna curentă, la locul dat de `Order`, în ordinea lunii returnată de server; luna rămasă fără carduri se ascunde prin CSS.
- [~] PR #4: închiderea editorului — Închide, Escape, click în afara panoului, × pe ultimul tab, Închide din forma minimizată — nu reîncarcă pagina cât timp niciun tab nu are modificări nesalvate: fereastra dispare, adresa devine `/?context={id}` (o intrare nouă în istoric; Back redeschide nota, încărcând pagina), iar focusul trece pe iconul Open al cardului notei active. Cu modificări nesalvate, Închide și Escape încarcă tabla după avertizarea browserului, ca înainte (× pe ultimul tab întreabă cu confirmarea lui, apoi închide pe loc); tabla se încarcă, fără avertizare, și după o salvare pe care nu a putut-o arăta (cardul lipsește din pagină sau pagina, rămasă deschisă dintr-o lună anterioară, nu are luna curentă).
- [x] Panoul are culoarea tipului notei active (`note-sheet--journal` / `note-sheet--article`) și conține headerul (sigla, taburile, Minimizează, Închide) și, pentru fiecare tab, titlul editabil pe loc, editorul și footerul notei (bara de informații; tipul, contextul și vizibilitatea; starea salvării și Salvează). Nu există butoane dublate.
- [~] PR #4: proprietarul schimbă tipul notei (jurnal / articol) din footerul ei, cu același comutator ca pe cardul „Notă nouă” (iconurile, tipul ales colorat și subliniat, numele tipului ca popover și ca etichetă accesibilă), pus în locul numelui tipului. Foaia, punctul tabului și forma minimizată iau imediat tipul ales, starea arată „Modificări nesalvate”, iar Salvează sau Ctrl+S îl salvează odată cu nota; după salvare, cardul de pe tablă își schimbă culoarea. Revenirea la tipul salvat nu lasă nimic nesalvat. Comutatorul apare numai cu JavaScript; fără JavaScript și pentru cei care doar citesc rămâne numele tipului.
- [x] Funcții: text pe paragrafe (un rând gol separă paragrafele), căutare și înlocuire (Ctrl+F, panoul sus), undo/redo, evidențierea rândului activ, salvare cu butonul sau Ctrl+S. Textele editorului, inclusiv frazele panoului de căutare, vin din .resx, randate de server în datele paginii.
- [x] Închide, Escape și click în afara panoului revin la tabla notei (`/?context={id}`); Escape în panoul de căutare închide doar panoul.
- [~] PR #4: Escape închide fereastra și în Firefox; pe `main`, Firefox oprește navigarea pornită din Escape ([CURRENT-STATUS.md](CURRENT-STATUS.md#probleme-cunoscute)).
- [x] Pentru cine poate doar citi (membrii, la o notă `Context` a altcuiva), editorul nu este editabil, butonul Salvează lipsește, iar bara de informații o spune.
- [ ] Salvarea automată.

### Taburile

- [x] Fiecare notă deschisă este un tab (`note-editor-tab`), cu un punct în culoarea tipului și titlul tăiat cu „…” când nu încape (titlul complet este tooltip). Taburile împart lățimea headerului, se micșorează până la un minim lizibil și derulează lateral când sunt multe; tabul activ este mai deschis la culoare și subliniat cu accentul, iar foaia editorului ia culoarea tipului lui.
- [x] Open sau dublu-click pe un card, cât timp editorul este în pagină, deschid nota într-un tab nou, fără reîncărcare (`?handler=NoteTab` randează `_NoteEditorTab`); o notă deja deschisă își activează tabul.
- [x] Fiecare tab își păstrează separat conținutul, titlul, modificările nesalvate, istoricul undo, versiunea și starea salvării; Ctrl+S salvează tabul activ. Săgețile, Home și End mută selecția între taburi.
- [x] × pe un tab închide doar nota lui, cu confirmare dacă are modificări nesalvate; închiderea ultimului tab închide editorul. Închide din header închide editorul cu toate taburile.
- [x] Adresa (`/?note={id}`) urmează tabul activ.
- [!] La reîncărcarea paginii se redeschide doar tabul activ, nu toate taburile; pe telefon bara de taburi arată aproximativ un tab și jumătate, restul se derulează.

### Forma minimizată

- [x] Minimizează schimbă doar afișarea (`note-editor-dialog--minimized`): dialogul nu mai este modal, foaia editorului rămâne în pagină ascunsă — cu textul, titlul, modificările nesalvate și istoricul undo —, iar în colțul din stânga-jos apare forma compactă `note-editor-minimized`: sigla, tipul, titlul, data creării, data ultimei modificări, Maximizează și Închide.
- [x] Tabla se poate folosi între timp; Ctrl+S nu salvează cât timp editorul e minimizat. Minimizarea nu salvează, nu închide și nu reîncarcă nota; pagina primește spațiu în plus jos, ca ultimele carduri să nu rămână sub forma compactă.
- [x] Maximizează sau dublu-click pe forma compactă readuce editorul modal, la aceeași dimensiune și poziție, cu taburile, ordinea și tabul activ. Forma minimizată arată tabul activ din momentul minimizării. Fără JavaScript butonul Minimizează nu apare.

### Bara de informații și starea salvării

- [x] Bara de informații (`note-editor-info`), sub editor, arată pentru paragraful de sub mouse — iar fără mouse pentru cel cu cursorul — data creării și a ultimei modificări (auditul `NoteBlocks`), „modificări nesalvate” sau „Paragraf nou, încă nesalvat”; în dreapta, scurtăturile. Paragraful descris primește o evidențiere discretă (`cm-hoveredParagraph`). Bara nu acoperă textul și funcționează și de la tastatură. Textele sunt compuse de server din .resx (`NoteDates.BlockAudit`) și actualizate după fiecare salvare.
- [x] Starea salvării (Salvat / Modificări nesalvate / Se salvează… / eroare) este un `role=status`; o eroare folosește clasa `note-editor-toolbar__status--error`. După un conflict, o notă negăsită sau o sesiune expirată, salvarea rămâne blocată până la reîncărcare.
- [x] Fiecare salvare afișează și un mesaj de salvare („Nota a fost salvată.” sau eroarea: salvare eșuată, conflict, sesiune expirată) în `status-region` din dialogul editorului, sus, pe centru. Mesajul rămâne până îl închide utilizatorul; închiderea lui nu închide editorul. Zona nu este live region: starea din bară anunță deja salvarea.

## Highlight-uri

- [x] În editor: rândul activ (`--wn-active-line`, cu bara accent în stânga), selecția (`--wn-selection`), rezultatele căutării (`--wn-highlight`, rezultatul curent mai intens), aparițiile textului selectat și paragraful descris de bara de informații. Stilurile WorkNotes sunt în `note-editor.css`, pe clasele CodeMirror (`.cm-editor`, `.cm-content`, `.cm-activeLine`, panoul de căutare); CodeMirror își injectează doar stilurile de bază.
- [~] PR #4: selecția din editor se vede pe ambele foi. CodeMirror desenează selecția într-un strat aflat sub rânduri (`drawSelection`), iar selecția nativă din rânduri este transparentă; fundalul opac al rândului activ, și cel al paragrafului descris de bara de informații, o acopereau. Cât timp există o selecție, cele două își păstrează numai bara din stânga. Selecția este verde (`--wn-selection`) pe foaia jurnalului și galbenă (`--wn-note-paper`) pe foaia salvie a articolului, unde verdele abia se vedea; titlul jurnalului și textul citit fără JavaScript au selecția verde, nu galben pe galben. În forced-colors, stratul CodeMirror este un amestec deschis al culorii de sistem (`color-mix(in srgb, Highlight 40%, Canvas)`), nu culoarea fundalului, la care l-ar reduce modul, iar textul rămâne lizibil peste el; peste text, browserul desenează selecția în culorile sistemului (verificat în Chromium, cu forced-colors emulat).
- [x] Pe tablă: stările de drag-and-drop (card preluat, celule eligibile, țintă) și focusul listei de contexte.
- [x] Componenta generală de evidențiere: `mark` / `text-highlight` (#F5DF93).
- [ ] Evidențierea referințelor de lucru (CR/bug) în text, cu ancore actualizate la editare, și marcajul de paragraf important (`IsImportant`).

## Referințe interne

- [ ] Pe `main` nu există referințe între note.
- [~] PR #4 ([ADR-003](decisions/ADR-003-internal-references.md)): un CR, un bug sau o referință de alt tip configurat în `ReferenceTypes`, scrisă în text (`CR 30080`, `CR-30080`, `CR_30080`, `CR30080`, `bug_1234`, `Bug-1234`…), care are cel puțin o notă destinație apare ca link: toată expresia, cu separatorul și literele așa cum sunt scrise, în culoarea accent, îngroșată și subliniată (`.cm-note-reference`; în modul forced-colors `LinkText`), cu titlul și tipul fiecărei note ca tooltip, câte o notă pe rând. Textul notei rămâne exact cel scris. Fiecare apariție a referinței în paragraf, oricare îi este forma, duce la aceleași note.
- [~] Click pe link (fără selecție și fără taste modificatoare) sau Ctrl+Enter cu cursorul pe el deschide notele lui, în ordinea ID-urilor, fiecare într-un tab nou al editorului sau în tabul ei deja deschis, apoi o arată pe prima; celelalte rămân în taburi, în spatele ei. Un editor minimizat este readus; celelalte taburi își păstrează conținutul și modificările nesalvate. Textul scris în interiorul unui link, ca și o literă sau o cifră scrisă lipit de el, îl elimină până la salvare; un spațiu sau un semn de punctuație scris lângă el îl păstrează, pentru că referința rămâne aceeași. După salvare, linkurile textului salvat apar fără reîncărcare, inclusiv pentru referințele nou scrise.
- [~] O referință fără nicio notă destinație rămâne text simplu, fără niciun semn în textul salvat; cât este scrisă, popup-ul de mai jos spune că nu există.
- [~] Popup-ul referinței abia scrise (`note-reference-lookup`, după designul primei sugestii de referințe): un cuvânt terminat imediat după o cifră — cu unul dintre caracterele din lista `wordEnds` din `note-references.js` (spațiu, spațiu neseparabil, Tab, rând nou, `.`, `,`, `;`, `:`, `-`, `_`, `!`, `?`, paranteze, `/`, `\`, ghilimele, `…`) sau cu Tab — trimite serverului textul rândului dinaintea lui; numai serverul citește referințele. Deasupra referinței apare o foaie mică albă: cât timp se caută, iconul de căutare și „Se caută referința…” (numai dacă răspunsul întârzie peste 250 ms); apoi, pentru o referință cu note, „Referința CR 30080 deschide:”, notele ei (tipul, în culoarea hârtiei lui, și titlul) și butonul „Transformă în referință”, cu „Tab transformă · Esc închide”; pentru o referință fără notă, „Referință inexistentă: nicio notă nu are CR 30083 în titlu.”, care se închide singur după 4 secunde. Un text care nu se termină cu o referință (o dată, o cantitate) nu arată nimic, iar după un link existent nu se caută nimic.
- [~] Butonul sau Tab face referința link imediat, cu notele și tooltipul ei, fără să aștepte salvarea; salvarea o stochează ca pe orice referință (și fără buton, orice referință cu note devine link la salvare). Popup-ul nu ia focusul și nu acoperă textul scris după referință; se închide cu Escape (editorul rămâne deschis), la modificarea textului căutat, când cursorul părăsește rândul pe care s-a terminat cuvântul (rândul nou, după Enter) sau trece înaintea referinței, la un click în text și când editorul pierde focusul. Pe ecranele înguste păstrează 16px față de marginile ferestrei. Pentru cititoarele de ecran, popup-ul este o regiune `role=status`: răspunsul este citit întreg.
- [~] Fără JavaScript, paragrafele din dialogul editorului au aceleași linkuri, ca linkuri HTML (`a.note-reference`, `/?note={id}`): o referință cu mai multe note duce la prima, iar fiecare notă următoare are după ea un link numerotat mic (`sup.note-reference-more`: 2, 3…), cu titlul și tipul notei ca nume accesibil și tooltip.
- [~] Sertarul referințelor (`note-references`), în dreapta textului (`note-sheet__body`): închis, este o bandă îngustă lângă text, cu numele „Referințe” scris vertical și numărul referințelor (gri când nu are niciuna); deschis, o coloană de `clamp(14rem, 30%, 22rem)`, cu scroll propriu sub un antet fix, opac, într-o nuanță mai închisă decât lista. Lista conține fiecare referință a notei care este link o singură dată, în ordinea primei apariții în text, ca un singur link, scris cum îl scrie textul prima dată (`CR_30080`), cu notele pe care le deschide (titlul și tipul) în tooltip, și textul explicativ deasupra; o notă fără linkuri arată „Nota nu are referințe către alte note.”. Sub 700px sertarul trece sub text, ca o bară care se deschide în jos (cel mult 45% din înălțime).
- [~] Click pe o referință din sertar deschide toate notele ei în taburi, ca linkul din text. Cu o tastă modificatoare, browserul urmează linkul către pagina primei ei note. Sertarul este deschis sau închis în toate taburile deodată, iar un tab deschis ulterior îl arată la fel. După fiecare salvare, lista urmează textul salvat, fără reîncărcare (intrările sunt copii ale template-urilor sertarului). Antetul este un `summary` nativ: se deschide și se închide și cu Enter sau Space, cu focusul desenat în interior; fără JavaScript, sertarul funcționează la fel, iar linkurile duc la `/?note={id}`.
- [!] Previzualizarea cardurilor de pe tablă este text simplu, fără linkuri.
- Descrierea se mută aici, ca implementată, la integrarea PR-ului.

### Referința Git

Popup-ul referinței abia scrise are două opțiuni: „Referință aplicație” (butonul primar, Tab) și „Referință Git” (butonul secundar, Shift+Tab; numai în editorul proprietarului). Al doilea deschide în același popup un selector de repository (dintre cele importate; fără ele, mesajul cu linkul spre `/Repositories`) și lista branch-urilor care conțin referința, fiecare un buton cu numele lui; alegerea leagă branch-ul de paragraf, iar nota se salvează întâi dacă are modificări. Starea ocupată, mesajele și erorile sunt în `role="status"`. Clasele `note-reference-lookup__git*`; popup-ul are `max-height: min(24rem, 45dvh)`, iar la înălțimi mici (≤700px) lista nu are derulare proprie. În sertarul referințelor, secțiunea „Referințe Git” listează referința, repository-ul și un link către branch (`target="_blank"`, `rel="noopener noreferrer"`), cu un buton de eliminare (numai proprietarul, numai cu JavaScript); lista urmează fiecare salvare.

Clicul pe o referință din text (sau Ctrl+Enter cu cursorul pe ea) deschide direct notele ei dacă nu are branch-uri legate. Dacă are și notele, și branch-uri Git legate (potrivire după referința normalizată în toată nota), apare un popup deasupra linkului (`note-reference-choice`, aceleași clase de aspect ca popup-ul referinței abia scrise): „Deschide notele” și lista branch-urilor ca linkuri către GitHub (`target="_blank"`, `rel="noopener noreferrer"`). Tab deschide notele, Shift+Tab deschide branch-ul (cu mai multe, dă focusul primului), Escape sau orice modificare/mutare a cursorului/pierderea focusului îl închide.

## Stări: loading, empty, error

- **Loading** — [x] Nu există indicatoare de încărcare: paginile sunt randate de server. În editor starea „Se salvează…” apare în bara de stare, iar lista unei luni primește `aria-busy` cât timp se salvează un schimb. [~] PR #4: popup-ul referinței abia scrise arată iconul de căutare cât timp răspunsul serverului întârzie.
- **Empty** — [x] „Tabla este goală.” pe o tablă fără carduri; „Nu există încă niciun context.” pe pagina Contexte; fără contexte, „Notă nouă” este dezactivat, cu linkul către Contexte; „Versiune neconfigurată” în footer; „Fără titlu” pentru o notă fără titlu; editorul unei note goale arată placeholderul „Scrie aici. Un rând gol separă paragrafele.”.
- **Error** — [x] Erorile de validare rămân în formulare; rezultatele operațiilor apar ca mesaje de salvare (eroare sau avertisment); editorul arată eroarea în bara de stare și ca mesaj; o notă care nu s-a putut deschide într-un tab produce „Nota nu a putut fi deschisă. Încearcă din nou.”.
- [!] Pentru 404, 403 și erorile neașteptate nu există pagini proprii: răspunsul are doar codul de stare, respectiv un răspuns ProblemDetails în afara Development. TODO: Necesită clarificare — dacă se doresc pagini de eroare localizate.

## Confirmări pentru modificări nesalvate

- [x] Părăsirea paginii cu modificări nesalvate în oricare tab al editorului (Închide, Escape, click în afară, navigare) cere confirmarea browserului (`beforeunload`).
- [x] Închiderea unui tab cu modificări nesalvate cere confirmare (`window.confirm`: „Nota „…” are modificări nesalvate. O închizi fără să le salvezi?”). Închide din forma minimizată părăsește pagina, deci cere confirmarea browserului.
- [x] Ștergerea unei note sau a unui context cere confirmare într-un overlay, cu focusul inițial pe „Renunță”.
- [!] Schimbarea limbii reîncarcă pagina fără confirmare, iar datele nesalvate din formulare se pierd; redenumirea pe loc salvează la părăsirea câmpului, fără confirmare. Comportamentul este cel documentat până acum; TODO: Necesită clarificare — dacă este dorit.

## Responsive

- [x] Sub 450px: notele sunt drepte, pe o coloană (fără decalaje și rotații), acțiunile din header trec pe rânduri separate, scurtăturile din bara de informații a editorului sunt ascunse, iar marginile dashboardului se reduc.
- [x] Sub 700px: tabelele devin blocuri, iar selectorul de limbă se aliniază la dreapta.
- [x] Mesajele de salvare ocupă pe mobil toată lățimea, cu margini de 16px; editorul folosește 90% din fereastră (`vw` / `dvh`), taburile derulează lateral, iar forma minimizată are cel mult `min(24rem, 100vw - 2rem)`.
- [!] Cu fonturi foarte late (de exemplu pe unele sisteme Linux), data modificării de pe card trece pe al doilea rând.

## Accesibilitate

- [x] Focus vizibil pentru tastatură (contur accent de 3px), ținte de minimum 44px pentru controalele principale, reducerea animațiilor la `prefers-reduced-motion` și contururi în `forced-colors`.
- [x] Etichete pentru cititoarele de ecran (`visually-hidden`) acolo unde informația este vizuală: tipul notei, „Creată” / „Modificată”, titlul tablei; iconurile au `aria-label` și `title`; elementele decorative au `aria-hidden`.
- [x] Semantică: dialoguri native cu focus captiv, `role=tablist` / `tab` / `tabpanel` în editor, `aria-current` în meniu, `role=status` și `role=alert` pentru stări și erori, `aria-live` pentru zona de mesaje a paginii, `aria-busy` în timpul unui schimb.
- [x] Tastatura: Escape închide dialogurile și meniul, Enter salvează titlul, Ctrl+S salvează, Ctrl+F caută, săgețile navighează între taburi; cu PR #4, Ctrl+Enter deschide referința internă de la cursor, iar Tab, după un număr, caută referința și, cu popup-ul deschis, o face link (Escape închide popup-ul).
- [!] Reordonarea se face numai cu mouse-ul (drag-and-drop HTML5); pe ecranele tactile depinde de suportul browserului.
