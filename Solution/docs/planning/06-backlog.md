# 06 — Backlog

## Pașii rămași din planul inițial

1. **WorkReferences**: catalogul de CR-uri și buguri pe context (unic pe `ContextId + ReferenceType + Code`), cu pagina lui.
2. **Asocieri**: `NoteWorkReferences` (notă ↔ referințe) și `NoteBlockWorkReferences` (paragraf ↔ referințe); căutarea tuturor explicațiilor după codul CR-ului.
3. **NoteLinks**: legături externe pe notă sau paragraf.
4. **Platforme** și `NotePlatforms`, dacă intră în prima interfață.
5. În editor: evidențierea referințelor în text (ancore actualizate la editare), autocomplete și popup-uri pentru referințe.
6. Ulterior, când există modulele: clienți, proiecte, branch-uri, evenimente, release-uri și publish-uri.
7. Lista „Referințe către această notă” în editor, din `NoteReferences` (după `TargetNoteId`), cu sursele pe care cititorul le poate vedea.

## Funcționalități încă neimplementate

- Salvarea automată în editor.
- Schimbarea vizibilității unei note (`Private` / `Context`) din interfață.
- Arhivarea (`ArchivedAtUtc`) și afișarea notelor arhivate.
- Marcajul de paragraf important (`IsImportant`) și data activității (`ActivityDate`).
- Editarea unei note partajate de către colegi (după stabilirea permisiunilor).
- Lista de contexte cu stil propriu pentru opțiunile deschise (acum este `select` nativ, evidențiat de browser).
- Un tip de mesaj „info” separat, dacă apare un mesaj care are nevoie de el.

## Limitări cunoscute

- La reîncărcarea paginii editorului se redeschide doar tabul activ (adresa `/?note={id}`), nu toate taburile.
- Mutarea unui paragraf prin tăiere și lipire creează un paragraf nou (id nou).
- Pe telefon, bara de taburi arată aproximativ un tab și jumătate; restul se derulează.
- Cu fonturi foarte late (de exemplu pe unele sisteme Linux), data modificării de pe card trece pe al doilea rând.
- Redenumirea pe loc nu mută cardul imediat; ordinea se actualizează la următoarea încărcare a tablei.
- După un schimb pe tablă, un editor deschis pe aceeași notă în alt tab sau în altă fereastră primește conflict la următoarea salvare (fără să suprascrie ceva); editorul din aceeași pagină primește noua versiune.
- Ordonarea se face numai cu mouse-ul (drag-and-drop HTML5); nu există încă o alternativă de la tastatură, iar pe ecranele tactile depinde de suportul browserului.
- Două note cu aceeași valoare `Order` (posibil numai prin inserări directe în SQL) nu își schimbă locurile prin drag-and-drop; tabla le ordonează după date.
- Tooltipul unei referințe (titlul destinației) se actualizează la următoarea deschidere a notei, nu imediat după redenumirea destinației în altă fereastră.
- Căutarea din editor (Ctrl+F) găsește și cifrele din forma păstrată a unei referințe; o înlocuire în interiorul ei o transformă în text simplu.
- Textul copiat în afara aplicației păstrează forma `[[note:{id}|{număr}]]`; lipit în altă notă, redevine referință.
- Referința se creează numai dintr-un număr tastat: un număr lipit nu aduce sugestii, iar numerele de 1–2 cifre nu sunt propuse.
- Într-o notă read-only (a unui coleg), referințele se deschid cu mouse-ul; de la tastatură nu, pentru că editorul read-only nu primește focus.

## Întrebări deschise

- Platformele intră în prima interfață a notelor?
- Cine poate adăuga referințe în catalog: orice membru al contextului sau numai proprietarul?
- Se păstrează un istoric al textului paragrafelor (versiuni), separat de `RowVersion`?
