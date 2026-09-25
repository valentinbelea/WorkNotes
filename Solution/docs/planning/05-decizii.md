# 05 — Jurnalul deciziilor

| # | Decizie | Motiv |
| --- | --- | --- |
| 1 | Database First pentru toate modulele, scripturi în `Scripts/version_0.01`, fără migrări EF | Cerință explicită; schema SQL este sursa de adevăr |
| 2 | Nota are două niveluri: `Notes` (organizare) și `NoteBlocks` (text + audit pe paragraf) | Jurnal cronologic, fișe de lucru și audit per paragraf |
| 3 | Tipurile sunt `Journal` și `Article` (planul spunea „Document”) | Denumirea folosită în interfață |
| 4 | Note private implicit; editare numai de proprietar; nota `Context` e vizibilă membrilor | Recomandarea planului, confirmată |
| 5 | Mai multe jurnale pe zi (nu unul singur) | Cerință ulterioară; indexul unic a fost eliminat prin 007 |
| 6 | Starea în URL pentru overlay-uri (`?add`, `?edit`, `?note`, `?delete`) | Funcționează fără JavaScript, adresele se pot deschide direct |
| 7 | Stil numai prin clase CSS; înclinările alese de server din id | Cerința „fără stiluri din JavaScript”; ordinea DOM rămâne cea a serviciului |
| 8 | CodeMirror 6 ca editor | Fără cost de licență (MIT), inclus local |
| 9 | Paragraful = text între rânduri goale, id GUID generat de editor | Auditul nu depinde de lățimea ferestrei; regulile de împărțire/unire din plan |
| 10 | `RowVersion` al notei verifică fiecare salvare; UPDATE-ul notei rulează mereu | Evită suprascrierea din alt tab, inclusiv la două salvări în aceeași secundă |
| 11 | Ordinea tablei după `ISNULL(ModifiedAtUtc, CreatedAtUtc)`; nemodificat = `ModifiedAtUtc` egal cu crearea | `ModifiedAtUtc` este NOT NULL în schemă |
| 12 | Datele de pe card fără etichete vizibile; etichete doar pentru cititoarele de ecran | Cerința „fără texte explicative” și accesibilitate |
| 13 | Mesajele de salvare sunt dialoguri nemodale deschise, închise prin `form method="dialog"` | Butonul de închidere funcționează fără JavaScript, fără reîncărcare |
| 14 | Mesajele unui dialog modal se randează în dialog | Altfel ar fi sub overlay și inerte |
| 15 | Erorile de validare rămân în formulare, nu devin mesaje fixe | Țin de câmpurile de corectat |
| 16 | Starea din bara editorului rămâne lângă mesajele de salvare | Arată starea permanent (de exemplu salvarea blocată după un conflict) |
| 17 | Minimizarea transformă dialogul în nemodal, fără a distruge editorul | Nu se pierde nimic și tabla se poate folosi |
| 18 | Taburile noi se încarcă ca HTML randat de server (`?handler=NoteTab`) | Textele din `.resx` și markup-ul rămân pe server; fără reîncărcarea paginii |
| 19 | Confirmarea la închiderea unui tab este `window.confirm` | Avertizarea browserului funcționează doar la părăsirea paginii |
| 20 | Contextul se alege pe tablă, înainte de editor; editorul doar îl afișează | Confirmat de utilizator |
| 21 | Sigla WN numai în fereastra editorului; favicon păstrat | Cerința utilizatorului |
| 22 | Taskul 02 trece la versiunea 0.02: scripturile noi merg în `Scripts/version_0.02`, iar `v.0.02` se adaugă ca rând nou în `DatabaseVersion`, fără a modifica `v.0.01` | Cerința utilizatorului; tabela nu are dată de instalare, versiunea curentă este cea mai mare înregistrată, iar scripturile de versiune păstrează datele existente |
| 23 | Coloana de ordonare se numește `Order`, scrisă `[Order]` în SQL | Numele cerut; EF Core delimitează singur identificatorii, iar regulile proiectului nu interzic cuvintele rezervate |
| 24 | Notele existente sunt numerotate pe context: jurnalele, apoi articolele, fiecare de la ultima modificare | Reproduce exact ordinea de până atunci în fiecare lună, fără calculul lunilor în SQL |
| 25 | În lună: `Order` crescător, apoi ultima modificare, crearea și `Id` descrescător | Cerința utilizatorului; direcțiile descrescătoare păstrează „cele mai recente primele”, iar `Id` face rezultatul determinist |
| 26 | O notă nouă primește minimul contextului minus 1, citit cu `UPDLOCK, HOLDLOCK` | Apare prima în luna curentă, unde a fost cardul nou; notele create simultan primesc valori diferite |
| 27 | Schimbul este permis numai proprietarului ambelor note, în același context și aceeași lună locală; se verifică și pe server | Doar proprietarul modifică o notă; grupul tablei este luna |
| 28 | Schimbul interschimbă cele două valori într-un singur `UPDATE`, cu verificarea `RowVersion`, fără să schimbe auditul | Atomic și sigur la modificări concurente; o notă nu își schimbă luna când este mutată |
| 29 | Serverul răspunde cu ordinea lunii, iar lista o urmează; la eșec lista revine la ordinea dinainte | Interfața rămâne la fel ca baza de date, și când tabla din pagină era veche |
| 30 | Banda este un element real (`note-card__tape`) doar pe cardurile proprietarului; drag-and-drop HTML5 nativ | Zona de drag este exact banda; browserul desenează imaginea trasă, deci nu sunt necesare stiluri inline |
| 31 | După un schimb, taburile editorului din pagină primesc noile versiuni ale notelor (`note-board:versions`) | Ordinea nu este conținut: editorul deschis nu trebuie să raporteze un conflict fals |
| 32 | Un drag nou nu se blochează cât timp se salvează un schimb: schimburile se aplică imediat, salvările pleacă pe rând, iar lista urmează ordinea serverului după ultimul răspuns; celulele se mută abia la `dragend` | Blocarea din `dragstart` anula a doua reordonare până la răspunsul primei salvări (lentă, de exemplu la primele interogări EF); un element tras mutat în `drop` își poate pierde `dragend` (Firefox) |
| 33 | O referință internă se păstrează în textul paragrafului, ca `[[note:{id}\|{număr}]]`; `NoteReferences` este evidența lor, refăcută la fiecare salvare a textului | Se mută, se copiază și se anulează odată cu textul, fără ancore de actualizat; tabela permite lista „Referințe către această notă” |
| 34 | Legătura se face prin ID-ul notei destinație, nu prin titlu sau număr | Referința rămâne validă după redenumirea destinației; tooltipul arată titlul actual |
| 35 | Sunt propuse numai celelalte note ale aceluiași context pe care utilizatorul le poate vedea, numai proprietarului notei | Asocierile respectă contextul notei (02-model-de-date); notele private ale colegilor nu se dezvăluie; numai proprietarul scrie în notă |
| 36 | Numărul este un cuvânt numai din cifre (3–18), găsit întreg în titlu (nu lângă alte cifre) | Potrivire exactă: `30080` nu se găsește în `130080`; numerele de 1–2 cifre (zile, cantități) ar aduce sugestii la tot pasul |
| 37 | Nimic nu se transformă automat: sugestia nu ia focusul, se alege prin click sau Tab, săgeți și Enter; Escape, click în afară sau tastarea în altă zonă o închid | Cerința utilizatorului; scrisul nu este întrerupt, iar Enter își păstrează rolul de rând nou |
| 38 | O referință care nu se mai poate deschide își păstrează textul, marcat discret, fără titlul destinației | Cerința utilizatorului; niciun titlu inaccesibil nu apare; dacă nota redevine accesibilă, referința funcționează din nou |
| 39 | `TargetNoteId` fără cascadă: ștergerea unei note șterge întâi referințele către ea, în aceeași tranzacție serializabilă | SQL Server nu acceptă două căi de cascadă din `Notes`; nicio referință nouă nu apare între cele două instrucțiuni |
| 40 | Referințele din previzualizarea cardurilor sunt linkuri, deschise prin `note-editor:open` | Cu editorul minimizat, click pe una îl readuce și arată tabul notei, cum cere deschiderea unei referințe |
| 41 | Crearea unei referințe este un pas propriu în istoricul undo (`isolateHistory`) | Ctrl+Z ia înapoi întâi textul scris după ea, apoi referința |
