# Cerințe funcționale

Statusurile sunt stabilite după codul de pe `main` (commit `504c01e`, 2026-09-25), nu numai după cerințele scrise. Comportamentul detaliat al interfeței este în [UI-UX.md](UI-UX.md), regulile de business în [DOMAIN-MODEL.md](DOMAIN-MODEL.md).

| Marcaj | Sens |
| --- | --- |
| [x] | Implementat |
| [ ] | Neimplementat |
| [~] | În dezvoltare (de exemplu într-un pull request neintegrat) |
| [!] | Necesită remediere sau clarificare |

## Dashboard

- [x] Pagina principală este tabla contextului selectat, pentru utilizatorul autentificat; vizitatorii văd panoul de bun venit, cu Autentificare și Înregistrare.
- [x] O tablă pentru fiecare context; lista de contexte înlocuiește titlul tablei (`/?context={id}`, implicit primul context); fără JavaScript, butonul „Afișează”.
- [x] Subtitlul „Spațiul meu” în header, dedus din secțiunea meniului.
- [x] Starea goală („Tabla este goală.”) și butonul „Notă nouă” dezactivat când utilizatorul nu are contexte, cu link către pagina Contexte.
- [!] Textul panoului de bun venit, „Aplicația este pregătită pentru dezvoltare.”, pare provizoriu. TODO: Necesită clarificare.

## Post-it-uri

- [x] Fiecare notă este un post-it: galben #FFF0B7 pentru jurnal, salvie #DDEADB pentru articol, cu bandă adezivă în culoarea hârtiei și înclinări deterministe alese de server din ID.
- [x] Headerul cardului: data creării (stânga) și a ultimei modificări (dreapta), pe același rând, cu anul, fără etichete vizibile; data modificării lipsește cât timp nota nu a fost modificată.
- [x] Previzualizarea primelor paragrafe; footerul cu vizibilitatea, Open și, pentru proprietar, Delete.
- [x] Ștergerea unei note, numai de proprietar, cu confirmare peste tablă; paragrafele se șterg odată cu nota.

## Grupuri

- [x] Notele sunt grupate pe luni după data locală a ultimei modificări, cele mai noi luni primele; luna curentă există mereu ca țintă pentru nota nouă.
- [x] În fiecare lună: `Order` crescător, apoi ultima modificare, crearea și `Id`, cele mai recente primele.
- [!] În cod și în documentele existente „grupul” este luna. TODO: Necesită clarificare — dacă termenul trebuie să acopere și alte grupări (de exemplu grupuri definite de utilizator).

## Jurnal / articol

- [x] Două tipuri de notă: jurnal (datat cu ziua locală) și articol (fără dată).
- [x] Mai multe jurnale pe zi.
- [x] Titlul este opțional, cel mult 200 de caractere; o notă fără titlu apare „Fără titlu”.
- [x] Notele noi sunt private; o notă `Context` este vizibilă membrilor contextului, numai pentru citire.

## Creare și editare inline

- [x] „Notă nouă” direct pe tablă: cardul apare primul în luna curentă, cu switch jurnal/articol, titlu opțional, Salvează / Renunță, Escape; fără JavaScript prin `/?new=true`.
- [x] Redenumirea titlului pe loc (Enter sau părăsirea câmpului salvează, Escape anulează), numai de proprietar; fără JavaScript, prin formular.
- [x] Mesajele de salvare (succes, avertisment, eroare) fixe sus pe centru, până le închide utilizatorul, și în dialoguri.

## Editor

- [x] Editor CodeMirror 6 peste tablă, într-un dialog de 90% din fereastră, deschis cu Open sau dublu-click (`/?note={id}`).
- [x] Paragrafe cu identitate stabilă și audit propriu; bara de informații arată data creării și a modificării paragrafului de sub mouse sau de la cursor.
- [x] Căutare/înlocuire, undo/redo, rândul activ, Ctrl+S, avertizare la părăsirea paginii cu modificări nesalvate.
- [x] Detectarea salvărilor concurente (`RowVersion`): o salvare dintr-un editor învechit este refuzată, fără să suprascrie.
- [x] Note partajate numai pentru citire pentru ceilalți membri; fără JavaScript, conținutul se afișează numai pentru citire.
- [x] Contextul se alege pe tablă, înainte de deschiderea editorului; editorul îl afișează, nu îl schimbă.
- [ ] Salvarea automată.
- [ ] Marcajul de paragraf important (`IsImportant`) și data activității (`ActivityDate`).

## Editor cu mai multe taburi

- [x] Mai multe note deschise simultan, în taburi independente (conținut, titlu, modificări nesalvate, undo, versiune, stare).
- [x] O notă deschisă de pe tablă cât timp editorul este în pagină se adaugă ca tab nou, fără reîncărcare; o notă deja deschisă își activează tabul.
- [x] × pe un tab închide doar nota lui (cu confirmare dacă are modificări nesalvate); Închide din header închide toate taburile.
- [!] La reîncărcarea paginii se redeschide doar tabul activ (limitare cunoscută).

## Minimizare și maximizare

- [x] Minimizarea ascunde editorul fără să salveze sau să piardă ceva și afișează în stânga-jos forma compactă (tip, titlu, data creării și a ultimei modificări, Maximizează, Închide); tabla se poate folosi între timp.
- [x] Maximizează sau dublu-click pe forma compactă readuce editorul, cu toate taburile.

## Ordonare drag-and-drop în cadrul grupului

- [x] Proprietarul schimbă locul a două note ale sale din aceeași lună, prinzând cardul de bandă; schimbul se salvează imediat (`?handler=SwapNotes`), iar la eșec luna revine la ordinea anterioară, cu mesaj localizat.
- [x] Drop-ul în altă lună, pe nota altui membru sau în afara cardurilor este ignorat; editorul deschis în aceeași pagină salvează în continuare după un schimb.
- [~] Un drag nou nu mai este blocat cât timp un schimb anterior se salvează (corecție în PR #4).
- [!] Reordonarea funcționează numai cu mouse-ul; nu există alternativă de la tastatură (limitare cunoscută).

## Referințe interne între note

- [~] Referințe dintr-o notă către altă notă a aceluiași context, create dintr-un număr prezent în titlul destinației, deschise în editor și în previzualizarea cardurilor — implementate în PR #4 (branch `main_task_02`), neintegrate în `main`.
- [ ] Lista „Referințe către această notă” (backlog-ul PR #4).
- [ ] Catalogul de referințe de lucru CR/bug pe context (`WorkReferences`) și asocierile cu notele și paragrafele (`NoteWorkReferences`, `NoteBlockWorkReferences`), cu căutarea explicațiilor după codul CR-ului.
- [ ] Legături externe pe notă sau pe paragraf (`NoteLinks`).
- [ ] Evidențierea referințelor în text (ancore actualizate la editare), autocomplete și popup-uri pentru referințe.

## Contexte și membri

- [x] Listarea contextelor în care utilizatorul este membru, ordonate după nume; adăugare, editare și ștergere în overlay pe aceeași pagină; linkul „Contexte” din meniu.
- [x] Creatorul devine proprietar (`Owner`); numai proprietarul editează, șterge și gestionează membrii; un membru care încearcă direct adresa primește 403, iar un context străin răspunde 404.
- [x] Adăugarea unui membru după e-mail (rolul `Member`) și eliminarea membrilor, cu mesaje distincte pentru e-mail invalid, cont inexistent și membru existent.
- [x] Un context care are note nu poate fi șters (avertisment); numele contextului este unic global.
- [!] Contextele create înainte de scriptul `005_CreateContextMembers.sql` nu au membri, deci nu sunt vizibile nimănui în aplicație, iar numele lor rămân ocupate. TODO: Necesită clarificare — dacă există astfel de contexte și cum se tratează.

## Autentificare și cont utilizator

- [x] Înregistrare (prenume, nume, e-mail unic, parolă și confirmare); după creare se deschide autentificarea.
- [x] Autentificare cu „Ține-mă minte”; mesaj unic de eșec; blocare 15 minute după 5 încercări eșuate.
- [x] „Contul meu”: modificarea numelui și prenumelui; e-mailul este afișat și nu se poate modifica.
- [x] Schimbarea parolei, cu verificarea parolei actuale și reîmprospătarea sesiunii.
- [x] Deconectare numai prin POST.
- [ ] Roluri, confirmarea e-mailului, recuperarea parolei — excluse explicit până la o cerință nouă.

## Sistemul multilingv

- [x] Română (implicită și fallback), engleză și poloneză pentru toate textele interfeței, prin .resx.
- [x] Selectorul de limbă din header, aplicat imediat, cu cookie păstrat un an; funcționează și fără JavaScript.
- [x] Mesajele de validare, erorile Identity și textele editorului sunt localizate.
- [!] Datele sunt afișate în același format în toate limbile. TODO: Necesită clarificare ([LOCALIZATION.md](LOCALIZATION.md#formatarea-datelor)).

## Alte cerințe

- [x] Versiunea aplicației, citită din `DatabaseVersion`, în footer.
- [x] Designul „Hârtie & salvie” și sigla WN în fereastra editorului și ca favicon.
- [x] Funcționarea de bază fără JavaScript.
- [ ] Schimbarea vizibilității unei note (`Private` / `Context`) din interfață.
- [ ] Arhivarea (`ArchivedAtUtc`) și afișarea notelor arhivate.
- [ ] Editarea unei note partajate de către colegi (după stabilirea permisiunilor).
- [ ] Lista de contexte cu stil propriu pentru opțiunile deschise (acum este `select` nativ).
- [ ] Un tip de mesaj „info” separat, dacă apare un mesaj care are nevoie de el.
- [ ] Platformele (`NotePlatforms`) și, ulterior, clienții, proiectele, branch-urile, evenimentele, release-urile și publish-urile.
- [ ] Înlocuirea jurnalelor TXT: TODO: Necesită clarificare — dacă este nevoie de importul conținutului existent ([PROJECT-CONTEXT.md](PROJECT-CONTEXT.md#originea-proiectului)).

## Sinteză

**Implementate** (version_0.01 și version_0.02): conturile, localizarea ro/en/pl, designul, contextele și membrii, tabla pe contexte și luni, post-it-urile cu creare, redenumire și ștergere pe loc, editorul cu paragrafe auditate, taburi și minimizare, mesajele de salvare, ordonarea prin drag-and-drop, versiunea în footer.

**În lucru**: PR #4 — schimburi succesive prin drag-and-drop fără blocare și referințele interne între note.

**Planificate**: referințele de lucru CR/bug și asocierile lor, legăturile externe, evidențierea referințelor, salvarea automată, vizibilitatea și arhivarea din interfață, paragraful important, editarea notelor partajate, platformele și modulele ulterioare. Ordinea și dependențele sunt în [ROADMAP.md](ROADMAP.md).
