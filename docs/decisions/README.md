# Decizii

Deciziile de arhitectură și de produs ale WorkNotes: ADR-uri (Architecture Decision Records) pentru deciziile cu impact larg și jurnalul deciziilor pentru cele punctuale.

## Ce este un ADR

Un ADR descrie o singură decizie importantă: contextul în care a fost luată, decizia, motivele, alternativele cunoscute și consecințele. Scrieți un ADR când decizia:

- schimbă structura soluției, direcția dependențelor sau strategia de persistență;
- introduce sau înlocuiește o bibliotecă, un format de date ori un mecanism transversal (securitate, localizare, concurență);
- este greu de inversat sau afectează mai multe module.

Deciziile punctuale (un detaliu de interfață, o regulă de ordonare) se adaugă ca rând nou în [jurnalul deciziilor](#jurnalul-deciziilor).

## Format

- Fișierul se numește `ADR-NNN-titlu-scurt.md` (număr de trei cifre, consecutiv, titlu în engleză kebab-case) și este scris în română.
- Secțiunile: Status, Context, Decizie, Motive, Alternative cunoscute, Consecințe; la început data și sursele.
- Un ADR acceptat nu se rescrie: o schimbare de decizie se face printr-un ADR nou, iar cel vechi primește statusul „Superseded by ADR-NNN”.
- Nu se inventează decizii: tot ce nu rezultă din cod, documentație sau cerințele utilizatorului se marchează „TODO: Necesită clarificare”.

## Statusuri

| Status | Sens |
| --- | --- |
| Proposed | Propusă sau documentată fără suficiente informații; încă neconfirmată de proprietarul proiectului |
| Accepted | Confirmată și în vigoare; codul o respectă |
| Superseded by ADR-NNN | Înlocuită de altă decizie |
| Deprecated | Nu mai este valabilă și nu are înlocuitor |

## Index

| ADR | Titlu | Status |
| --- | --- | --- |
| [ADR-001](ADR-001-solution-architecture.md) | Arhitectura soluției: straturi, Database First, compunere, prezentare progresivă | Accepted |
| [ADR-002](ADR-002-note-editor.md) | Editorul de note: CodeMirror 6, paragrafe cu identitate, salvare cu verificarea versiunii, taburi | Accepted |

## Jurnalul deciziilor

Deciziile înregistrate în version_0.01 și version_0.02, cu motivele lor. Coloana ADR arată decizia de ansamblu care le cuprinde. Deciziile 32–41, propuse în PR #4 (ordonarea fără blocare și referințele interne), se adaugă aici la integrarea PR-ului.

| # | Decizie | Motiv | ADR |
| --- | --- | --- | --- |
| 1 | Database First pentru toate modulele, scripturi în `Scripts/version_0.01`, fără migrări EF | Cerință explicită; schema SQL este sursa de adevăr | [001](ADR-001-solution-architecture.md) |
| 2 | Nota are două niveluri: `Notes` (organizare) și `NoteBlocks` (text + audit pe paragraf) | Jurnal cronologic, fișe de lucru și audit per paragraf | [002](ADR-002-note-editor.md) |
| 3 | Tipurile sunt `Journal` și `Article` (planul spunea „Document”) | Denumirea folosită în interfață | — |
| 4 | Note private implicit; editare numai de proprietar; nota `Context` e vizibilă membrilor | Recomandarea planului, confirmată | — |
| 5 | Mai multe jurnale pe zi (nu unul singur) | Cerință ulterioară; indexul unic a fost eliminat prin 007 | — |
| 6 | Starea în URL pentru overlay-uri (`?add`, `?edit`, `?note`, `?delete`) | Funcționează fără JavaScript, adresele se pot deschide direct | [001](ADR-001-solution-architecture.md) |
| 7 | Stil numai prin clase CSS; înclinările alese de server din ID | Cerința „fără stiluri din JavaScript”; ordinea DOM rămâne cea a serviciului | [001](ADR-001-solution-architecture.md) |
| 8 | CodeMirror 6 ca editor | Fără cost de licență (MIT), inclus local | [002](ADR-002-note-editor.md) |
| 9 | Paragraful = text între rânduri goale, ID GUID generat de editor | Auditul nu depinde de lățimea ferestrei; regulile de împărțire/unire din plan | [002](ADR-002-note-editor.md) |
| 10 | `RowVersion` al notei verifică fiecare salvare; UPDATE-ul notei rulează mereu | Evită suprascrierea din alt tab, inclusiv la două salvări în aceeași secundă | [002](ADR-002-note-editor.md) |
| 11 | Ordinea tablei după `ISNULL(ModifiedAtUtc, CreatedAtUtc)`; nemodificat = `ModifiedAtUtc` egal cu crearea | `ModifiedAtUtc` este NOT NULL în schemă | — |
| 12 | Datele de pe card fără etichete vizibile; etichete doar pentru cititoarele de ecran | Cerința „fără texte explicative” și accesibilitate | — |
| 13 | Mesajele de salvare sunt dialoguri nemodale deschise, închise prin `form method="dialog"` | Butonul de închidere funcționează fără JavaScript, fără reîncărcare | — |
| 14 | Mesajele unui dialog modal se randează în dialog | Altfel ar fi sub overlay și inerte | — |
| 15 | Erorile de validare rămân în formulare, nu devin mesaje fixe | Țin de câmpurile de corectat | — |
| 16 | Starea din bara editorului rămâne lângă mesajele de salvare | Arată starea permanent (de exemplu salvarea blocată după un conflict) | [002](ADR-002-note-editor.md) |
| 17 | Minimizarea transformă dialogul în nemodal, fără a distruge editorul | Nu se pierde nimic și tabla se poate folosi | [002](ADR-002-note-editor.md) |
| 18 | Taburile noi se încarcă ca HTML randat de server (`?handler=NoteTab`) | Textele din .resx și markup-ul rămân pe server; fără reîncărcarea paginii | [002](ADR-002-note-editor.md) |
| 19 | Confirmarea la închiderea unui tab este `window.confirm` | Avertizarea browserului funcționează doar la părăsirea paginii | [002](ADR-002-note-editor.md) |
| 20 | Contextul se alege pe tablă, înainte de editor; editorul doar îl afișează | Confirmat de utilizator | [002](ADR-002-note-editor.md) |
| 21 | Sigla WN numai în fereastra editorului; favicon păstrat | Cerința utilizatorului | — |
| 22 | Taskul 02 trece la versiunea 0.02: scripturile noi merg în `Scripts/version_0.02`, iar `v.0.02` se adaugă ca rând nou în `DatabaseVersion`, fără a modifica `v.0.01` | Cerința utilizatorului; tabela nu are dată de instalare, versiunea curentă este cea mai mare înregistrată, iar scripturile de versiune păstrează datele existente | — |
| 23 | Coloana de ordonare se numește `Order`, scrisă `[Order]` în SQL | Numele cerut; EF Core delimitează singur identificatorii, iar regulile proiectului nu interzic cuvintele rezervate | — |
| 24 | Notele existente sunt numerotate pe context: jurnalele, apoi articolele, fiecare de la ultima modificare | Reproduce exact ordinea de până atunci în fiecare lună, fără calculul lunilor în SQL | — |
| 25 | În lună: `Order` crescător, apoi ultima modificare, crearea și `Id` descrescător | Cerința utilizatorului; direcțiile descrescătoare păstrează „cele mai recente primele”, iar `Id` face rezultatul determinist | — |
| 26 | O notă nouă primește minimul contextului minus 1, citit cu `UPDLOCK, HOLDLOCK` | Apare prima în luna curentă, unde a fost cardul nou; notele create simultan primesc valori diferite | — |
| 27 | Schimbul este permis numai proprietarului ambelor note, în același context și aceeași lună locală; se verifică și pe server | Doar proprietarul modifică o notă; grupul tablei este luna | — |
| 28 | Schimbul interschimbă cele două valori într-un singur `UPDATE`, cu verificarea `RowVersion`, fără să schimbe auditul | Atomic și sigur la modificări concurente; o notă nu își schimbă luna când este mutată | — |
| 29 | Serverul răspunde cu ordinea lunii, iar lista o urmează; la eșec lista revine la ordinea dinainte | Interfața rămâne la fel ca baza de date, și când tabla din pagină era veche | — |
| 30 | Banda este un element real (`note-card__tape`) doar pe cardurile proprietarului; drag-and-drop HTML5 nativ | Zona de drag este exact banda; browserul desenează imaginea trasă, deci nu sunt necesare stiluri inline | — |
| 31 | După un schimb, taburile editorului din pagină primesc noile versiuni ale notelor (`note-board:versions`) | Ordinea nu este conținut: editorul deschis nu trebuie să raporteze un conflict fals | [002](ADR-002-note-editor.md) |
