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
- **Referințe interne între note** (docs/decisions/ADR-003-internal-references.md): un CR, un bug sau alt tip activ din tabela de configurare `ReferenceTypes`, scris în text — `CR 30080`, `CR-30080`, `CR_30080`, `CR30080`, `cr 30080`, `bug 1234`, `bug-1234`, `bug_1234`, `bug1234` — este link către toate notele tablei, vizibile proprietarului paragrafului, care au aceeași referință în titlu, în afară de nota paragrafului (comparată după tip și număr: `CR_30080` → „CR 30080”, `bug1234` → „Rezolvare Bug-1234”); fără nicio altă notă, rămâne text. Toată expresia, așa cum e scrisă, este linkul; click sau Ctrl+Enter deschide toate notele lui, fiecare într-un tab nou sau în tabul ei existent, o arată pe prima, readuce editorul minimizat și păstrează modificările nesalvate ale celorlalte taburi. Textul nu se schimbă: relațiile sunt păstrate pe paragraf în `NoteReferences`, cu notele fiecărei referințe în `NoteReferenceTargets`, și recalculate la salvare, la schimbarea titlului și la crearea notelor, iar ștergerea unei note își scoate rândurile (`INoteReferenceService`). Primul model (legătura în text `[[note:{id}|{număr}]]`, sugestiile din editor, comanda `create-note-references`) a fost înlocuit.
- **Reindexarea conținutului existent**: scriptul `004_ReplaceNoteReferences.sql` transformă legăturile vechi din text în numere, recreează `NoteReferences`, citește toate paragrafele, inclusiv jurnalul „CRs”, și listează referințele fără destinație și pe cele ambigue; cu `@Save = 0` doar le arată. `005_CreateNoteReferenceTargets.sql` creează `NoteReferenceTargets`, mută în ea legăturile din `004` și reindexează conținutul cu mai multe note pe referință, listând referințele fără notă și pe cele cu mai multe note. `006`–`008` creează catalogul `WorkReferences`, îl completează cu referințele stocate și adaugă ID-ul referinței în fiecare rând din `NoteReferences`. `009`–`011` adaugă tabela de configurare a tipurilor, `ReferenceTypes` (`CR` și `BUG`), cu cheile externe ale tipului, iar `012` citește din nou toate textele cu tipurile active. Verificate prin parserul T-SQL și prin comparația logicii lor cu regulile aplicației; încă neaplicate pe SQL Server.
- **Sertarul referințelor din editor**: în dreapta textului, un sertar (`details`) cu fiecare referință a notei care este link, o singură dată, în ordinea din text, cu notele pe care le deschide; închis, o bandă îngustă cu numărul referințelor. Click pe o referință deschide toate notele ei în taburi, click pe o notă numai pe ea; starea este aceeași în toate taburile, iar lista urmează fiecare salvare. Funcționează și fără JavaScript; sub 700px este sub text.
- **Corecție — selecția din editor**: selecția textului nu se vedea pe rândul activ (CodeMirror o desenează sub rânduri, iar fundalul rândului activ o acoperea) și abia se vedea pe foaia salvie a articolelor; în titlul jurnalului era galbenă pe galben. Acum rândul activ își păstrează numai bara cât timp există o selecție, iar selecția este verde pe jurnal și galbenă pe articol.
- **Deschiderea mai rapidă a notelor**: o notă deschisă de pe tablă apare peste tabla din pagină, fără reîncărcare (`?handler=NoteEditor`); înainte, fiecare deschidere recitea și redesena toată tabla (contextele, toate cardurile cu previzualizările, versiunea din footer). Previzualizarea cardurilor numerotează acum numai paragrafele notelor citite (ale tablei sau ale unui card), nu toate paragrafele din bază; câștigă și schimbul prin drag-and-drop, redenumirea și ștergerea, care citesc câte un card. Măsurat fără SQL Server, pe o tablă de 300 de note, în Chromium: de la click la editorul gata 0,8–1,1 s înainte, 0,4–0,5 s acum (în pagină, circa 0,2 s). Neverificat încă pe SQL Server.
- **Corecție — Escape în Firefox**: Escape nu închidea fereastra editorului (nici deschisă ca pagină): `modal.js` pornea navigarea din evenimentul `cancel`, iar Firefox o oprea, pentru că Escape oprește o pagină care se încarcă. Acum dialogul tratează Escape la `keydown`, cu tasta prevenită, apoi merge la adresa de închidere; Escape folosit de conținut (panoul de căutare, sugestia de referință, o selecție în editor) nu închide, nici pe editorul minimizat. Verificat în Firefox 136 și Chromium cu tastatură reală și pe celelalte overlay-uri (ștergerea notei, contextele).
