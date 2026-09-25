# 04 — Funcționalități realizate (version_0.01, version_0.02)

Pașii sunt în ordinea în care au fost făcuți. Fiecare pas a fost compilat, testat (teste Business, verificarea resurselor) și verificat în browser pe SQL Server, cu și fără JavaScript și pe mobil.

## Fundația (existentă la preluare)
- Soluția pe straturi, Database First, versiunea aplicației citită din `DatabaseVersion` și afișată în footer.
- Conturi cu ASP.NET Core Identity: înregistrare, autentificare, datele contului, schimbarea parolei, deconectare; politica de parolă și blocarea după 5 încercări.
- Localizare ro/en/pl; designul „Hârtie & salvie”.

## Designul post-it
- Post-it galben #FFF0B7 cu o singură umbră, stilat numai prin clase CSS.
- Panourile de conținut pe hârtie salvie #DDEADB, cu bandă adezivă; paleta reținută: accent #176C65, hover #10564F, fond #F5F3EB, note #FFF0B7, selecție #CCE6DF.

## Dashboard și contexte
- Dashboardul este o tablă care ocupă toată zona principală, cu subtitlul „Spațiul meu” dedus din meniu.
- **Pasul 1 — contexte**: tabela `WorkContexts`, pagina de listare și adăugare/editare/ștergere în overlay pe aceeași pagină, link în meniu.
- **Pasul 2 — membri**: `ContextMembers`, creatorul devine `Owner`; lista arată doar contextele proprii; numai proprietarul editează, șterge, adaugă membri după e-mail și îi elimină.
- O tablă pentru fiecare context: lista de contexte (cu bordură întreruptă) înlocuiește titlul tablei; la focus bordura devine verde plin.

## Note pe tablă
- **Notă nouă** direct pe tablă: cardul apare primul, cu titlu opțional și switch jurnal/articol (iconuri cu popover), Salvează / Renunță; fără JavaScript prin `/?new=true`.
- Mai multe jurnale pe zi (scriptul 007).
- Grupare pe luni; în fiecare lună întâi jurnalele, apoi articolele. Ulterior: gruparea și ordinea după **ultima modificare** (`ISNULL(modificare, creare)`).
- Înclinări și decalaje deterministe (12 variante, ±12px, ±2°), alese de server din id.
- Cardul: previzualizarea primelor paragrafe, redenumirea titlului pe loc (Enter salvează, Escape anulează), ștergere cu confirmare, data cu anul; tipul nu mai are etichetă (culoarea îl arată); banda verde pe hârtia salvie.
- În headerul cardului: data creării în stânga și a ultimei modificări în dreapta, pe același rând, fără etichete vizibile; datele se scalează cu lățimea cardului.

## Editorul de note
- **CodeMirror 6** peste tablă, dialog de 90% cu scroll propriu, deschis cu Open sau dublu-click (`/?note={id}`).
- Paragrafe cu identitate stabilă (`NoteBlocks`) și audit propriu; bara de informații arată pentru paragraful de sub mouse data creării și a modificării.
- Căutare/înlocuire, undo/redo, Ctrl+S, avertizare la părăsirea paginii cu modificări nesalvate; `RowVersion` refuză salvările dintr-un editor învechit.
- Header simplificat (sigla WN, Minimizează, Închide); toate acțiunile notei în footer.
- **Minimizare**: forma compactă în stânga-jos (tip, titlu, data creării și a modificării, Maximizează, Închide); nu salvează, nu închide și nu pierde nimic; tabla se poate folosi între timp.
- **Taburi**: mai multe note deschise simultan; o notă deja deschisă își activează tabul; fiecare tab are starea lui; × pe tab închide doar nota (cu confirmare), Închide din header închide tot (cu avertizare).

## Mesaje de salvare
- Succes, avertisment și eroare, cu icon și culoare, fixe sus pe centru, cu buton de închidere (funcționează și fără JavaScript), până le închide utilizatorul.
- Și în dialoguri (membri, editor), deasupra overlay-ului; mesajele identice nu se adună.
- Tipul corect pentru fiecare mesaj: de exemplu „Contextul are note și nu poate fi șters.” este avertisment.

## Sigla
- Sigla WN (pătrat verde, litere albe, banda post-it) în fereastra editorului, maximizat și minimizat; favicon SVG și `favicon.ico`.

## Documentație
- README, `design-system.md` și aceste documente de planificare, actualizate la fiecare pas.

## version_0.02 (taskul 02)
- **Versiunea 0.02**: `Scripts/version_0.02/000_UpdateDatabaseVersion.sql` înregistrează `v.0.02` în `DatabaseVersion`, ca rând nou, numai dacă lipsește; `v.0.01` rămâne. După executare footerul afișează `v.0.02`.
- **Ordonarea post-it-urilor prin drag-and-drop**: coloana `Notes.[Order]` (scriptul 001, inițializată fără să schimbe aranjarea), ordinea în lună după `Order`, apoi ultima modificare și crearea (cele mai recente primele). Proprietarul prinde un card de bandă și îl lasă peste altă notă a lui din aceeași lună: cele două își schimbă locurile imediat ce drag-ul se încheie, schimbul se salvează în fundal, iar lista urmează ordinea returnată. Drop-ul în altă lună este ignorat; la eșec schimbul respectiv se anulează și apare un mesaj localizat. Evidențiere discretă pentru cardul preluat, celulele eligibile și ținta. Notele noi apar primele în luna curentă. Editorul deschis în aceeași pagină salvează în continuare după un schimb.
- **Corecție — a doua reordonare**: după primul schimb, al doilea drag nu mai pornea: `dragstart` era anulat până la răspunsul primei salvări, fără niciun semn vizibil. Acum un drag nou pornește oricând, schimburile se aplică imediat și se salvează pe rând, în ordinea lor, iar celulele se mută abia după `dragend`, niciodată în timpul drag-ului.
