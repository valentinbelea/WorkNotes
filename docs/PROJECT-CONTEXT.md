# Contextul proiectului

## Originea proiectului

- Repository-ul a început pe 2026-09-21 („Initial Commit”, „first web page”), autor Valentin Belea. Dezvoltarea se face pe taskuri numerotate, fiecare într-un branch propriu integrat în `main` prin pull request: task 00 — conturi și localizare (PR #1); task 01 — designul „Hârtie & salvie”, contextele, notele pe tablă și editorul, adică version_0.01 (PR #2); task 02 — version_0.02, ordonarea post-it-urilor (PR #3; PR #4 deschis). O parte din implementare este realizată cu asistenți AI (Claude Code), după regulile din [AGENTS.md](../AGENTS.md).
- Funcționalitatea urmează un plan inițial de produs (concepte, tabele, pași), citat în documentele de planificare („din planul inițial”), dar care nu se află în repository. TODO: Necesită clarificare — dacă planul inițial trebuie adăugat în repository.
- Conform cererii de documentare din 2026-09-25, WorkNotes înlocuiește jurnalele de lucru ținute în fișiere TXT. TODO: Necesită clarificare — formatul și volumul acestor jurnale și dacă, respectiv cum, se importă conținutul existent.

## Scopul aplicației

WorkNotes este un caiet de lucru pentru munca de zi cu zi pe proiecte: jurnalul cronologic al zilei și fișe de lucru (articole) despre subiecte precise — de exemplu un CR, un bug sau o livrare. Captura trebuie să fie rapidă (o notă se creează direct pe tablă, titlul este opțional), iar textul trebuie să poată fi regăsit ulterior după context și, în etapele următoare, după referințele de lucru (CR-uri, buguri).

## Jurnalul zilnic

- Jurnalul este o notă de tip `Journal`, datată cu ziua locală în care a fost creat; se pot crea oricâte jurnale pe zi, în orice context în care utilizatorul este membru.
- Jurnalul păstrează cronologia muncii, iar articolele adună explicațiile despre un subiect.
- Fiecare paragraf are propriul audit (când a fost creat și când a fost modificat ultima dată), vizibil în bara de informații a editorului.

## Evidența CR-urilor și a bugurilor

- Astăzi, un CR sau un bug se documentează într-un articol (de exemplu articolul „CR 30042”), iar jurnalele îl menționează în text.
- Planificat: catalogul de referințe de lucru pe context (`WorkReferences`, tipurile `CR` și `Bug`) și asocierea lor cu notele și paragrafele, pentru căutarea tuturor explicațiilor după codul CR-ului ([DOMAIN-MODEL.md](DOMAIN-MODEL.md#entități-planificate)).
- În dezvoltare (PR #4): referințe interne între note, create dintr-un număr care apare în titlul altei note a aceluiași context (de exemplu `30080` către post-it-ul „CR 30080”).

## Branch-uri, versiuni și publish-uri

Termenii au două sensuri, care nu trebuie confundate:

- **În domeniul urmărit** (proiectele clienților): branch-urile, versiunile (de exemplu „SQL pentru versiunea 2.2.13.4” din exemplul planului), release-urile și publish-urile sunt module planificate (`NoteBranches`, `NoteReleases`, `NotePublishes`, `NoteEvents`). Un eveniment real, precum „Publish efectuat”, va avea propria înregistrare; simpla menționare într-un paragraf nu confirmă nimic. TODO: Necesită clarificare — conținutul acestor module.
- **Pentru WorkNotes însuși**: branch-urile Git sunt descrise în [GIT-WORKFLOW.md](GIT-WORKFLOW.md), versiunile aplicației în [VERSIONING.md](VERSIONING.md), publicarea în [DEPLOYMENT.md](DEPLOYMENT.md).

## Clienți și servere

- Clienții și firmele sunt reprezentați astăzi prin contexte (de exemplu SD Worx, TopDev); un modul dedicat de clienți (`NoteClients`) și unul de proiecte (`NoteProjects`) sunt planificate „când există modulele respective”.
- Platformele (`NotePlatforms`) sunt planificate, cu întrebarea deschisă dacă intră în prima interfață.
- TODO: Necesită clarificare — evidența serverelor clienților (ce se înregistrează și cum se leagă de note) nu apare în cod sau în documentele existente.

## Principii

- **Contextul este granița de acces.** Fiecare citire și modificare verifică apartenența la context; un context străin răspunde „nu există” (404), fără a-i confirma existența.
- **Notele sunt private implicit.** O notă `Context` este vizibilă membrilor, dar editabilă numai de proprietar. Rolul de administrator nu dă acces la notele private.
- **Captura nu se blochează.** Titlul este opțional; se pot crea oricâte jurnale pe zi.
- **Textul utilizatorului rămâne cum a fost scris.** Interfața este tradusă (ro/en/pl, prin .resx); conținutul nu.
- **Auditul este pe paragraf.** Redimensionarea ferestrei sau mutarea textului nu schimbă auditul; numai editarea textului îl actualizează.
- **Aplicația funcționează și fără JavaScript** pentru operațiile de bază; JavaScript adaugă confortul (editare pe loc, editor, taburi).

## Terminologie

| Termen | Sens |
| --- | --- |
| Context (de lucru) | Spațiul de lucru al notelor: un client sau o firmă; tabela `WorkContexts` |
| Membru, Owner / Member | Utilizator cu acces la un context; `Owner` (proprietarul, creatorul) sau `Member` |
| Notă | Unitatea de lucru: jurnal sau articol |
| Jurnal (`Journal`) | Notă legată de o zi (`JournalDate`), cronologică |
| Articol (`Article`) | Fișă tematică, fără dată; planul inițial o numea „Document” |
| Paragraf / bloc | Textul dintre rânduri goale, cu identitate și audit propriu; tabela `NoteBlocks` |
| Tabla / Dashboard | Afișarea notelor unui context ca post-it-uri, pagina principală |
| Post-it / card | O notă pe tablă |
| Grup | Luna de pe tablă: notele sunt grupate după luna ultimei modificări; drag-and-drop-ul funcționează numai în interiorul lunii |
| Bandă adezivă („fâșia de lipici”) | Banda din partea de sus a post-it-ului și a panourilor; pe cardurile proprietarului este mânerul de drag-and-drop |
| Vizibilitate | `Private` (numai proprietarul) sau `Context` (membrii contextului, numai citire) |
| Arhivare | `ArchivedAtUtc`; pregătită, fără interfață |
| Audit | Momentele și autorii creării și ultimei modificări |
| Ultima modificare | `ISNULL(ModifiedAtUtc, CreatedAtUtc)` |
| `Order` | Locul notei în luna ei pe tablă |
| `RowVersion` / versiune | Tokenul de concurență al unei note |
| Referință de lucru | CR sau bug dintr-un sistem extern, catalogat o singură dată pe context (planificat) |
| CR | Cerere de modificare (change request) dintr-un sistem extern |
| Referință internă | Legătură dintr-o notă către altă notă a aceluiași context (în dezvoltare, PR #4) |
| Legătură | URL extern atașat unei note sau unui paragraf (planificat, `NoteLinks`) |
| Versiune | Versiunea bazei de date (`v.0.0x`), afișată în footer; folderul `Scripts/version_0.0x` |
| Task | Unitate de dezvoltare numerotată (task 00, 01, 02), lucrată într-un branch propriu |
| Mesaj de salvare | Mesajul fix din partea de sus a ferestrei: succes, avertisment sau eroare |
