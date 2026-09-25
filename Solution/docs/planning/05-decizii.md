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
