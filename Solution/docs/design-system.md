# WorkNotes — Hârtie & salvie

Design aprobat: varianta 1. Temă luminoasă, fond crem, verde închis, suprafețe de hârtie și post-it-uri pastelate. Nu este implementată o temă întunecată.

## Fișiere și ordine de încărcare

1. wwwroot/css/tokens.css — paletă, font, raze, umbre, tranziții.
2. wwwroot/css/site.css — layout, text, butoane, formulare, mesaje.
3. wwwroot/css/notes-board.css — tabla și post-it-urile.
4. wwwroot/js/notes-board.js — modul ES cu initializeNotesBoards(root).

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
- Tabele: data-table; numele rândului este th scope=row, iar acțiunile sunt aliniate la dreapta. Sub 700px rândurile devin blocuri. Etichetele doar pentru cititoare de ecran folosesc visually-hidden.

Focus vizibil pentru tastatură, ținte de minimum 44px pentru controalele principale, reducerea animației când utilizatorul solicită acest lucru și contururi în forced-colors.

## Contractul viitoarei table

Nu există încă persistența Notes sau un dashboard cu note reale. Stilurile și poziționarea sunt pregătite; pagina principală nu afișează date fictive.

Structura de integrare Razor (valorile provin din viitorul view model; etichetele fixe din resurse):

```html
<ul class="notes-board">
    <li class="note-cell" data-note-id="@note.Id">
        <a class="note-card note-card--sage" href="@note.LocalUrl">
            <span class="note-card__meta">@note.TypeLabel <time datetime="@note.IsoCreatedAt">@note.DisplayCreatedAt</time></span>
            <h2 class="note-card__title">@note.Title</h2>
            <p class="note-card__excerpt">@note.Excerpt</p>
            <span class="note-card__footer">@note.ContextLabel</span>
        </a>
    </li>
</ul>
```

- Serviciul livrează notele ordonate după creare descrescător, cu departajare stabilă. Ordinea DOM este ordinea cronologică și cea de navigare cu tastatura.
- Fiecare celulă este pătrată pe desktop, cu o singură notă. Hashul ID-ului stabilește decalaje între -6 și +6px și rotație între -0.8 și +0.8 grade.
- Identitatea, nu poziția în listă, determină decalajul: filtrarea și reîncărcarea nu schimbă aspectul notei. Nu se persistă coordonate în baza de date.
- Marginile de 16px rezervă spațiu pentru decalaj, rotație, hover și focus. Nu introduceți suprapuneri între celule.
- Pe ecrane de maximum 450px notele sunt drepte, fără decalaje, pe o coloană.
- Cardul poate fi link (navigare) sau button (selecție); nu puneți controale interactive în interiorul unui alt control. Pentru selecție folosiți aria-pressed pe button, iar pentru pagina curentă aria-current=page pe link.
- După randarea dinamică, importați initializeNotesBoards din /js/notes-board.js și apelați funcția cu rădăcina noului conținut.
- Culorile și selecția nu sunt atribuite de script. Scriptul nu schimbă textul, ordinea sau datele.
- Exemplul nu definește modelul final al notelor și nu introduce drag-and-drop.

## Verificare vizuală

Verificați ecranele de cont și headerul în ro/en/pl, la 320px și desktop, cu tastatură și erori de validare. Pentru tabla viitoare verificați titluri lungi, metadate, ordine, selectare și încadrarea în celule înainte de conectarea datelor.

## Navigare
Headerul păstrează brandul, numele utilizatorului și selectorul de limbă. Titlul WorkNotes (buton cu indicator de meniu) deschide un sertar modal în stânga, stilizat în navigation.css. Dialogul nativ păstrează focusul în meniu; Escape, fundalul și butonul de închidere îl închid și restabilesc focusul.
Dashboard duce la pagina principală: pentru utilizatorul autentificat aceasta este tabla: secțiunea dashboard ocupă toată lățimea și înălțimea zonei principale, cu o margine mică; titlul, descrierea și butonul Notă nouă stau în partea de sus a tablei, iar notele (ul.notes-board) se vor afișa sub ele, în locul mesajului notes-board-empty; vizitatorii văd panoul de bun venit cu autentificare și înregistrare. Butonul Notă nouă este dezactivat nativ până la implementarea modulului Notes. Lângă brand, headerul afișează subtitlul secțiunii din meniul curent (Spațiul meu pentru Dashboard, Contul meu pentru paginile de cont), calculat de NavigationSections din ruta paginii; aceeași regulă deschide grupul Contul meu în meniu. Grupul Contul meu conține datele contului, schimbarea parolei și deconectarea POST; pentru vizitatori conține autentificarea și înregistrarea. Pagina curentă are aria-current. Fără JavaScript navigarea rămâne vizibilă ca secțiune obișnuită.

## Panouri pe tabla crem
Fundalul principal folosește tokenul --wn-bg, crem cald. Toate panourile de conținut (bun venit pentru vizitatori, autentificare, înregistrare, cont și schimbare parolă) folosesc clasa postit-panel: hârtie verde salvie (#DDEADB), bandă superioară discretă și umbră de post-it. Panourile noi din body trebuie să reutilizeze această clasă. Containerele interne pentru câmpuri și butoane rămân parte din aceeași foaie, fără umbre suprapuse. Formularele nu se rotesc, pentru lizibilitate.

### Hârtie lipită cu bandă
postit.css aplică panourilor culoarea plină #DDEADB (--wn-note-sage), iar cardurilor tablei galbenul notelor #FFF0B7 (--wn-note-paper), fără gradient. Tot aspectul hârtiei (fundal, margine, rază, umbră, bandă) este definit numai în postit.css; notes-board.css păstrează doar dispunerea, tipografia și starea hover a cardurilor. Imaginea decorativă images/postit-tape.svg este suprapusă central peste marginea de sus. Umbra inferioară este un singur strat box-shadow (--wn-shadow-paper) aplicat direct elementului, fără pseudo-element la bază și fără un al doilea strat decalat (ambele produc o margine dublată). Aspectul hârtiei este integral CSS static prin clasele postit-panel / note-card, fără JavaScript sau stiluri injectate dinamic. Pseudo-elementele nu interceptează clickuri și nu adaugă text accesibil. Păstrați spațiu deasupra panourilor și overflow vizibil; nu aplicați efectul fiecărui container intern. Stilurile pentru contrast forțat elimină decorațiile.
